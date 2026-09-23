using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using KeyVaultComparer.Api.Models;

namespace KeyVaultComparer.Api.Services
{
    public class KeyVaultService
    {
        private readonly Azure.Core.TokenCredential _credential;
        private readonly SecretClientOptions _options;

        public KeyVaultService(Azure.Core.TokenCredential credential)
        {
            _credential = credential;
            _options = new SecretClientOptions();
            _options.Diagnostics.ApplicationId = "KeyVaultComparerApp";
        }

        public async Task<Dictionary<string, VaultSyncResult>> GetAllSecretNamesAsync(List<string> vaultUris)
        {
            var results = new ConcurrentDictionary<string, VaultSyncResult>();

            if (vaultUris == null || !vaultUris.Any())
            {
                return new Dictionary<string, VaultSyncResult>();
            }

            var propTasks = vaultUris.Select(async uri =>
            {
                var vaultNames = new List<SecretMetadata>();
                try
                {
                    var client = new SecretClient(new Uri(uri), _credential, _options);
                    await foreach (var secretProp in client.GetPropertiesOfSecretsAsync())
                    {
                        if (secretProp.Enabled.GetValueOrDefault())
                        {
                            vaultNames.Add(new SecretMetadata
                            {
                                Name = secretProp.Name,
                                CreatedOn = secretProp.CreatedOn,
                                UpdatedOn = secretProp.UpdatedOn,
                                ExpiresOn = secretProp.ExpiresOn
                            });
                        }
                    }
                    results[uri] = new VaultSyncResult { Secrets = vaultNames };
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 403)
                {
                    results[uri] = new VaultSyncResult 
                    { 
                        Secrets = new List<SecretMetadata>(),
                        ErrorMessage = ex.Message.Contains("RBAC", StringComparison.OrdinalIgnoreCase) 
                            ? "Missing RBAC configuration (Key Vault Secrets User role)" 
                            : "Missing Get/List Vault Access Policies"
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching properties from {uri}: {ex.Message}");
                    if (ex is AuthenticationFailedException || ex is CredentialUnavailableException || 
                        ex.ToString().Contains("AADSTS") || ex.ToString().Contains("az login"))
                    {
                        throw;
                    }
                    results[uri] = new VaultSyncResult { Secrets = vaultNames, ErrorMessage = ex.Message };
                }
            });

            await Task.WhenAll(propTasks);
            
            return results.ToDictionary(kvp => kvp.Key, kvp => {
                kvp.Value.Secrets = kvp.Value.Secrets.OrderBy(n => n.Name).ToList();
                return kvp.Value;
            });
        }

        public async Task<Dictionary<string, SecretValueStatus>> GetSecretValuesAsync(string vaultUri, List<string> secretNames)
        {
            var results = new ConcurrentDictionary<string, SecretValueStatus>();

            if (string.IsNullOrWhiteSpace(vaultUri) || secretNames == null || !secretNames.Any())
            {
                return new Dictionary<string, SecretValueStatus>();
            }

            try
            {
                var client = new SecretClient(new Uri(vaultUri), _credential, _options);
                var fetchTasks = secretNames.Select(async name =>
                {
                    try
                    {
                        KeyVaultSecret secret = await client.GetSecretAsync(name);
                        results[name] = new SecretValueStatus
                        {
                            Value = secret.Value,
                            Status = "Present"
                        };
                    }
                    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                    {
                        // Secret not found in this specific vault
                        results[name] = new SecretValueStatus
                        {
                            Value = null,
                            Status = "Missing"
                        };
                    }
                    catch (Azure.RequestFailedException ex) when (ex.Status == 403)
                    {
                        // Missing RBAC or Access Policies
                        results[name] = new SecretValueStatus
                        {
                            Value = null,
                            Status = "Forbidden",
                            ErrorMessage = ex.Message.Contains("RBAC", StringComparison.OrdinalIgnoreCase) 
                                ? "Missing RBAC configuration (Key Vault Secrets User role)" 
                                : "Missing Get/List Vault Access Policies"
                        };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching secret {name} from {vaultUri}: {ex.Message}");
                        results[name] = new SecretValueStatus
                        {
                            Value = null,
                            Status = "Error"
                        };
                    }
                });

                await Task.WhenAll(fetchTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating client for {vaultUri}: {ex.Message}");
                if (ex is AuthenticationFailedException || ex is CredentialUnavailableException || 
                    ex.ToString().Contains("AADSTS") || ex.ToString().Contains("az login"))
                {
                    throw;
                }
            }

            return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public async Task<List<string>> ApplyChangesAsync(List<StagedChangeRequest> changes)
        {
            var errors = new ConcurrentBag<string>();
            var tasks = changes.Select(async change => 
            {
                try {
                    var client = new SecretClient(new Uri(change.VaultUri), _credential, _options);
                    await client.SetSecretAsync(change.SecretName, change.NewValue);
                } catch(Exception ex) {
                    errors.Add($"Failed to update {change.SecretName} in {change.VaultUri}: {ex.Message}");
                }
            });
            await Task.WhenAll(tasks);
            return errors.ToList();
        }
    }
}
