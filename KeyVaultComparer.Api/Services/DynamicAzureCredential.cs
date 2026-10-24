using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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

        // In-memory token cache to prevent redundant CLI executions across concurrent requests
        private static readonly ConcurrentDictionary<string, AccessToken> _tokenCache = new();

        // Concurrency lock to serialize CLI token acquisition and prevent MSAL lockfile collisions on Windows
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);

        static DynamicAzureCredential()
        {
            TryCleanStaleLockFile();
        }

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
            ClearTokenCache();
        }

        public static void ClearTokenCache()
        {
            _tokenCache.Clear();
        }

        public async Task<string> LoginInteractiveAsync(CancellationToken ct = default)
        {
            // Clean any dangling lockfile from a prior aborted session before invoking az login
            TryCleanStaleLockFile();
            ClearTokenCache();

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
                        // Allow OS file handles to release
                        await Task.Delay(250, ct);
                        TryCleanStaleLockFile();
                        _interactiveCredential = null; // Clear so DefaultAzureCredential (AzureCliCredential) takes precedence
                        ClearTokenCache();
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
                ClearTokenCache();
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
            var cacheKey = BuildCacheKey(requestContext);
            if (TryGetValidCachedToken(cacheKey, out var cachedToken))
            {
                return cachedToken;
            }

            _tokenLock.Wait(cancellationToken);
            try
            {
                if (TryGetValidCachedToken(cacheKey, out cachedToken))
                {
                    return cachedToken;
                }

                var cred = ResolveCredentialAsync(cancellationToken).GetAwaiter().GetResult();
                var token = AcquireTokenWithRetry(cred, requestContext, cancellationToken);
                _tokenCache[cacheKey] = token;
                return token;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var cacheKey = BuildCacheKey(requestContext);
            if (TryGetValidCachedToken(cacheKey, out var cachedToken))
            {
                return cachedToken;
            }

            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                if (TryGetValidCachedToken(cacheKey, out cachedToken))
                {
                    return cachedToken;
                }

                var cred = await ResolveCredentialAsync(cancellationToken);
                var token = await AcquireTokenWithRetryAsync(cred, requestContext, cancellationToken);
                _tokenCache[cacheKey] = token;
                return token;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static string BuildCacheKey(TokenRequestContext requestContext)
        {
            var scopes = requestContext.Scopes != null ? string.Join(" ", requestContext.Scopes) : string.Empty;
            var tenant = requestContext.TenantId ?? string.Empty;
            return $"{scopes}|{tenant}";
        }

        private static bool TryGetValidCachedToken(string cacheKey, out AccessToken token)
        {
            if (_tokenCache.TryGetValue(cacheKey, out token))
            {
                // Refresh if within 5 minutes of expiration
                if (token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    return true;
                }
                _tokenCache.TryRemove(cacheKey, out _);
            }
            return false;
        }

        private static bool IsLockFileException(Exception ex)
        {
            var msg = ex.ToString();
            return msg.Contains("lockfile", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Errno 13", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("msal_token_cache", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("Permission denied", StringComparison.OrdinalIgnoreCase);
        }

        private static void TryCleanStaleLockFile()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var lockFilePath = Path.Combine(userProfile, ".azure", "msal_token_cache.bin.lockfile");
                if (File.Exists(lockFilePath))
                {
                    File.Delete(lockFilePath);
                }
            }
            catch
            {
                // File may be locked by an active process or inaccessible; suppress safely
            }
        }

        private async ValueTask<AccessToken> AcquireTokenWithRetryAsync(
            TokenCredential cred,
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await cred.GetTokenAsync(requestContext, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxRetries && IsLockFileException(ex))
                {
                    TryCleanStaleLockFile();
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken);
                }
            }

            return await cred.GetTokenAsync(requestContext, cancellationToken);
        }

        private AccessToken AcquireTokenWithRetry(
            TokenCredential cred,
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return cred.GetToken(requestContext, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxRetries && IsLockFileException(ex))
                {
                    TryCleanStaleLockFile();
                    Thread.Sleep(300 * attempt);
                }
            }

            return cred.GetToken(requestContext, cancellationToken);
        }
    }
}

