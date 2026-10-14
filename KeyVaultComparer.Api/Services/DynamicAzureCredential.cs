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
            // 1. If Azure CLI is installed, launch 'az login' via system shell.
            // This stores the tokens and refresh tokens in the Azure CLI local profile (~/.azure),
            // allowing DefaultAzureCredential (AzureCliCredential) to seamlessly serve all subscriptions,
            // tenants, and services without opening extra browser windows or deadlocking on loopback listeners.
            if (IsAzureCliAvailable())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "az",
                    Arguments = OperatingSystem.IsWindows() ? "/c az login" : "login",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromMinutes(3));
                    await proc.WaitForExitAsync(cts.Token);
                    if (proc.ExitCode == 0)
                    {
                        _interactiveCredential = null; // Clear so DefaultAzureCredential (AzureCliCredential) takes precedence
                        return "Successfully authenticated via Azure CLI.";
                    }
                }
            }

            // 2. Fallback: In-process interactive browser login (launches default system browser with Microsoft SSO)
            try
            {
                var options = new InteractiveBrowserCredentialOptions
                {
                    ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46" // Microsoft Azure CLI public client ID
                };
                var interactive = new InteractiveBrowserCredential(options);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMinutes(3));

                await interactive.GetTokenAsync(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), cts.Token);
                _interactiveCredential = interactive;
                return "Successfully authenticated via browser SSO.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Interactive browser credential error: {ex.Message}");
                throw new Exception($"Authentication was cancelled or failed: {ex.Message}. Please run 'az login' in your terminal.");
            }
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
