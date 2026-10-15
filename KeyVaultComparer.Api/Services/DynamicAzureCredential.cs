using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using KeyVaultComparer.Api.Services.SecretStore;

namespace KeyVaultComparer.Api.Services
{
    public class DynamicAzureCredential : TokenCredential
    {
        private readonly SecretStoreManager _secretStoreManager;
        private readonly AppSettingsService _appSettingsService;
        private readonly TokenCredential _defaultCliCredential;
        private static TokenCredential? _interactiveCredential;

        public DynamicAzureCredential(
            SecretStoreManager secretStoreManager,
            AppSettingsService appSettingsService,
            TokenCredential defaultCliCredential)
        {
            _secretStoreManager = secretStoreManager;
            _appSettingsService = appSettingsService;
            _defaultCliCredential = defaultCliCredential;
        }

        public static void ClearInteractiveCredential()
        {
            _interactiveCredential = null;
        }

        public async Task<string> LoginInteractiveAsync(CancellationToken ct = default)
        {
            // 1. If Azure CLI is installed, az login is the gold standard for developer workstations
            if (IsAzureCliAvailable())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "az",
                    Arguments = OperatingSystem.IsWindows() ? "/c az login" : "login",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromMinutes(2));

                    await proc.WaitForExitAsync(cts.Token);
                    if (proc.ExitCode == 0)
                    {
                        return "Successfully authenticated via Azure CLI.";
                    }
                    else
                    {
                        var err = await proc.StandardError.ReadToEndAsync(ct);
                        throw new Exception($"Azure CLI login exited with code {proc.ExitCode}: {err}");
                    }
                }
            }

            // 2. Fallback to in-process interactive browser login
            var options = new InteractiveBrowserCredentialOptions
            {
                ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46" // Standard Azure CLI public client ID
            };
            var interactive = new InteractiveBrowserCredential(options);
            await interactive.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), ct);
            _interactiveCredential = interactive;
            return "Successfully authenticated via browser.";
        }

        private static bool IsAzureCliAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "where.exe" : "which",
                    Arguments = "az",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit();
                    return proc.ExitCode == 0;
                }
            }
            catch {}
            return false;
        }

        public async Task<TokenCredential> ResolveCredentialAsync(CancellationToken ct = default)
        {
            var settings = await _appSettingsService.GetSettingsAsync(ct);
            string? tenantId = settings.AzureTenantId;
            string? clientId = settings.AzureClientId;
            string? clientSecret = await _secretStoreManager.GetSecretAsync(SecretStoreManager.AzureClientSecretKey, ct);

            if (!string.IsNullOrWhiteSpace(tenantId) &&
                !string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(clientSecret))
            {
                return new ClientSecretCredential(tenantId.Trim(), clientId.Trim(), clientSecret.Trim());
            }

            if (_interactiveCredential != null)
            {
                return _interactiveCredential;
            }

            return _defaultCliCredential;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var cred = ResolveCredentialAsync(cancellationToken).GetAwaiter().GetResult();
            return cred.GetToken(requestContext, cancellationToken);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var cred = await ResolveCredentialAsync(cancellationToken);
            return await cred.GetTokenAsync(requestContext, cancellationToken);
        }
    }
}
