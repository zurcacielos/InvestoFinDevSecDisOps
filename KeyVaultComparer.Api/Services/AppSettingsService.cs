using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultComparer.Api.Services
{
    public class LocalAppSettings
    {
        public string? AdoOrganization { get; set; }
        public string? AdoProject { get; set; }
        public string? GithubBaseUrl { get; set; } = "https://github.com";
        public string? GithubOrganization { get; set; }
        public string? AzureTenantId { get; set; }
        public string? AzureClientId { get; set; }
        public string? SecretStorageProvider { get; set; }
        
        // Option 1 Encrypted Cache Settings
        public bool CacheEnabled { get; set; } = true;
        public int CachePipelinesTtlMinutes { get; set; } = 60;
        public int CachePipelineRunsTtlMinutes { get; set; } = 15;
        public int CacheVaultKeysTtlMinutes { get; set; } = 120;
        public int CacheVariableGroupsTtlMinutes { get; set; } = 30;
    }

    public class AppSettingsService
    {
        private readonly string _configFilePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private LocalAppSettings? _cachedSettings;

        public AppSettingsService()
        {
            string baseDir = Environment.GetEnvironmentVariable("KEYVAULTCOMPARER_DATA_DIR") ?? "";
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                if (OperatingSystem.IsWindows())
                {
                    baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyVaultComparer");
                }
                else
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    baseDir = Path.Combine(string.IsNullOrWhiteSpace(home) ? "/tmp" : home, ".keyvaultcomparer");
                }
            }

            Directory.CreateDirectory(baseDir);
            _configFilePath = Path.Combine(baseDir, "appsettings.local.json");
        }

        public async Task<LocalAppSettings> GetSettingsAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_cachedSettings != null) return _cachedSettings;

                if (File.Exists(_configFilePath))
                {
                    var json = await File.ReadAllTextAsync(_configFilePath, ct);
                    _cachedSettings = JsonSerializer.Deserialize<LocalAppSettings>(json) ?? new LocalAppSettings();
                }
                else
                {
                    _cachedSettings = new LocalAppSettings();
                }

                return _cachedSettings;
            }
            catch
            {
                return new LocalAppSettings();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveSettingsAsync(LocalAppSettings settings, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                _cachedSettings = settings;
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configFilePath, json, ct);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
