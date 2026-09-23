using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Azure.Core;
using KeyVaultComparer.Api.Models;

namespace KeyVaultComparer.Api.Services
{
    public class KeyVaultManagementService
    {
        private readonly Azure.Core.TokenCredential _credential;

        public KeyVaultManagementService(Azure.Core.TokenCredential credential)
        {
            _credential = credential;
        }
        public async Task<List<DiscoveredVault>> GetAvailableVaultsAsync(string? query, string? subscriptionId = null)
        {
            var vaults = new List<DiscoveredVault>();
            
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                // Safety guard: Never query Azure if the search string is too short or empty
                return vaults;
            }

            var client = new ArmClient(_credential);

            try
            {
                var tenant = client.GetTenants().First();

                // Build KQL Query for Azure Resource Graph
                var queryBuilder = new System.Text.StringBuilder();
                queryBuilder.AppendLine("Resources");
                queryBuilder.AppendLine("| where type =~ 'microsoft.keyvault/vaults'");
                
                // Server-side text filtering
                if (!string.IsNullOrWhiteSpace(query))
                {
                    // Escape single quotes for safety
                    var safeQuery = query.Replace("'", @"\'");
                    queryBuilder.AppendLine($"| where name contains '{safeQuery}'");
                }
                
                queryBuilder.AppendLine("| project name, properties.vaultUri");
                queryBuilder.AppendLine("| take 100");

                var queryContent = new ResourceQueryContent(queryBuilder.ToString());

                // Apply subscription filter natively to ARG
                if (!string.IsNullOrWhiteSpace(subscriptionId))
                {
                    queryContent.Subscriptions.Add(subscriptionId);
                }

                var response = await tenant.GetResourcesAsync(queryContent);
                
                if (response.Value != null && response.Value.Data != null)
                {
                    var rawJson = response.Value.Data.ToString();
                    Console.WriteLine("RAW ARG JSON:");
                    Console.WriteLine(rawJson);
                    
                    using var doc = System.Text.Json.JsonDocument.Parse(response.Value.Data);
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                        var vaultUri = item.TryGetProperty("properties_vaultUri", out var uriProp) ? uriProp.GetString() : null;

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(vaultUri))
                        {
                            vaults.Add(new DiscoveredVault
                            {
                                Name = name,
                                Uri = vaultUri
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching vaults from Resource Graph: {ex.Message}");
                if (ex is AuthenticationFailedException || ex is CredentialUnavailableException || 
                    (ex is Azure.RequestFailedException rfe && (rfe.Status == 401 || rfe.Status == 403)) ||
                    ex.ToString().Contains("AADSTS") || ex.ToString().Contains("az login") ||
                    ex.ToString().Contains("No subscriptions found"))
                {
                    throw;
                }
            }

            return vaults;
        }

        public async Task<List<AzureSubscription>> GetSubscriptionsAsync()
        {
            var subs = new List<AzureSubscription>();
            try
            {
                var client = new ArmClient(_credential);
                await foreach (var sub in client.GetSubscriptions().GetAllAsync())
                {
                    subs.Add(new AzureSubscription
                    {
                        Id = sub.Data.SubscriptionId,
                        Name = sub.Data.DisplayName
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching subscriptions: {ex.Message}");
                if (ex is AuthenticationFailedException || ex is CredentialUnavailableException || 
                    (ex is Azure.RequestFailedException rfe && (rfe.Status == 401 || rfe.Status == 403)) ||
                    ex.ToString().Contains("AADSTS") || ex.ToString().Contains("az login") ||
                    ex.ToString().Contains("No subscriptions found"))
                {
                    throw;
                }
            }
            return subs;
        }

        public async Task<UsageStatsResponse> GetVaultUsageStatsAsync(List<string> vaultUris)
        {
            var response = new UsageStatsResponse();
            if (vaultUris == null || !vaultUris.Any()) return response;

            var armClient = new ArmClient(_credential);
            var tenant = armClient.GetTenants().First();

            var queryBuilder = new System.Text.StringBuilder();
            queryBuilder.AppendLine("Resources");
            queryBuilder.AppendLine("| where type =~ 'microsoft.keyvault/vaults'");
            var uriList = string.Join(",", vaultUris.Select(u => $"'{u}'"));
            queryBuilder.AppendLine($"| where properties.vaultUri in ({uriList})");
            queryBuilder.AppendLine("| project id, name, vaultUri = tostring(properties.vaultUri)");

            var queryContent = new ResourceQueryContent(queryBuilder.ToString());
            var argResponse = await tenant.GetResourcesAsync(queryContent);
            
            var vaultInfos = new List<AuditMissingVaultInfo>();
            if (argResponse.Value != null && argResponse.Value.Data != null)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(argResponse.Value.Data);
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    vaultInfos.Add(new AuditMissingVaultInfo
                    {
                        ArmId = item.GetProperty("id").GetString()!,
                        Name = item.GetProperty("name").GetString()!,
                        VaultUri = item.GetProperty("vaultUri").GetString()!
                    });
                }
            }

            var logsClient = new LogsQueryClient(_credential);
            var timeRange = new QueryTimeRange(TimeSpan.FromDays(90));
            
            var kql = @"
AzureDiagnostics
| where ResourceProvider == 'MICROSOFT.KEYVAULT'
| where OperationName == 'SecretGet'
| where clientInfo_s !contains 'KeyVaultComparerApp'
| summarize LastUsed = max(TimeGenerated) by id_s
";

            var tasks = vaultInfos.Select(async vault =>
            {
                try
                {
                    var result = await logsClient.QueryResourceAsync(new ResourceIdentifier(vault.ArmId), kql, timeRange);
                    
                    if (result.Value != null && result.Value.Table != null)
                    {
                        foreach (var row in result.Value.Table.Rows)
                        {
                            var secretId = row.GetString("id_s");
                            var lastUsed = row.GetDateTimeOffset("LastUsed");
                            if (!string.IsNullOrEmpty(secretId) && lastUsed.HasValue)
                            {
                                var uriParts = new Uri(secretId);
                                var secretName = uriParts.Segments.Length > 2 ? uriParts.Segments[2].TrimEnd('/') : "";
                                
                                if (!string.IsNullOrEmpty(secretName))
                                {
                                    var key = $"{vault.VaultUri}_{secretName}".ToLowerInvariant();
                                    lock (response.UsageData)
                                    {
                                        if (!response.UsageData.ContainsKey(key) || response.UsageData[key] < lastUsed.Value.DateTime)
                                        {
                                            response.UsageData[key] = lastUsed.Value.DateTime;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error querying logs for {vault.Name}: {ex.Message}");
                    lock (response.AuditMissingVaults)
                    {
                        response.IsAuditingEnabled = false;
                        response.AuditMissingVaults.Add(vault);
                    }
                }
            });

            await Task.WhenAll(tasks);

            return response;
        }
    }
}
