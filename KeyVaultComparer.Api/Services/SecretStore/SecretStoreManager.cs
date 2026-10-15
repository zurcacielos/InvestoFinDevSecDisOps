using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultComparer.Api.Services.SecretStore
{
    public class SecretStoreProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSupported { get; set; }
        public bool IsActive { get; set; }
    }

    public class SecretStoreManager
    {
        private readonly IEnumerable<ISecureSecretStore> _stores;
        private readonly AppSettingsService _appSettingsService;

        public const string AdoPatKey = "AdoPat";
        public const string GithubTokenKey = "GitHubToken";

        public SecretStoreManager(IEnumerable<ISecureSecretStore> stores, AppSettingsService appSettingsService)
        {
            _stores = stores;
            _appSettingsService = appSettingsService;
        }

        public async Task<ISecureSecretStore> GetActiveStoreAsync(CancellationToken ct = default)
        {
            var settings = await _appSettingsService.GetSettingsAsync(ct);
            string? preferred = settings.SecretStorageProvider;

            if (!string.IsNullOrWhiteSpace(preferred))
            {
                var candidate = _stores.FirstOrDefault(s => string.Equals(s.ProviderName, preferred, StringComparison.OrdinalIgnoreCase));
                if (candidate != null && candidate.IsSupported)
                {
                    return candidate;
                }
            }

            // Default order: WindowsCredentialManager on Windows, EncryptedFileStore on Linux/Docker
            var winStore = _stores.FirstOrDefault(s => s.ProviderName == "WindowsCredentialManager");
            if (winStore != null && winStore.IsSupported)
            {
                return winStore;
            }

            var fileStore = _stores.FirstOrDefault(s => s.ProviderName == "EncryptedFileStore");
            if (fileStore != null && fileStore.IsSupported)
            {
                return fileStore;
            }

            throw new InvalidOperationException("No supported secure secret store found.");
        }

        public async Task<List<SecretStoreProviderInfo>> GetProvidersInfoAsync(CancellationToken ct = default)
        {
            var active = await GetActiveStoreAsync(ct);
            return _stores.Select(s => new SecretStoreProviderInfo
            {
                Name = s.ProviderName,
                DisplayName = s.DisplayName,
                Description = s.Description,
                IsSupported = s.IsSupported,
                IsActive = s.ProviderName == active.ProviderName
            }).ToList();
        }

        public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
        {
            var store = await GetActiveStoreAsync(ct);
            var val = await store.GetSecretAsync(key, ct);
            if (!string.IsNullOrWhiteSpace(val)) return val;

            // Fallback check in other supported stores in case user just switched
            foreach (var other in _stores.Where(s => s.IsSupported && s.ProviderName != store.ProviderName))
            {
                var fallbackVal = await other.GetSecretAsync(key, ct);
                if (!string.IsNullOrWhiteSpace(fallbackVal))
                {
                    // Silently migrate to active store
                    await store.SetSecretAsync(key, fallbackVal, ct);
                    return fallbackVal;
                }
            }

            return null;
        }

        public async Task<bool> HasSecretAsync(string key, CancellationToken ct = default)
        {
            var val = await GetSecretAsync(key, ct);
            return !string.IsNullOrWhiteSpace(val);
        }

        public async Task SetSecretAsync(string key, string value, CancellationToken ct = default)
        {
            var store = await GetActiveStoreAsync(ct);
            await store.SetSecretAsync(key, value, ct);
        }

        public async Task DeleteSecretAsync(string key, CancellationToken ct = default)
        {
            foreach (var store in _stores.Where(s => s.IsSupported))
            {
                await store.DeleteSecretAsync(key, ct);
            }
        }

        public async Task SwitchProviderAsync(string targetProviderName, CancellationToken ct = default)
        {
            var targetStore = _stores.FirstOrDefault(s => string.Equals(s.ProviderName, targetProviderName, StringComparison.OrdinalIgnoreCase));
            if (targetStore == null || !targetStore.IsSupported)
            {
                throw new NotSupportedException($"Secret store provider '{targetProviderName}' is not supported on this platform.");
            }

            var currentStore = await GetActiveStoreAsync(ct);
            if (currentStore.ProviderName != targetStore.ProviderName)
            {
                // Migrate known secrets
                var knownKeys = new[] { AdoPatKey, GithubTokenKey };
                foreach (var k in knownKeys)
                {
                    var val = await currentStore.GetSecretAsync(k, ct);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        await targetStore.SetSecretAsync(k, val, ct);
                    }
                }

                var settings = await _appSettingsService.GetSettingsAsync(ct);
                settings.SecretStorageProvider = targetStore.ProviderName;
                await _appSettingsService.SaveSettingsAsync(settings, ct);
            }
        }
    }
}
