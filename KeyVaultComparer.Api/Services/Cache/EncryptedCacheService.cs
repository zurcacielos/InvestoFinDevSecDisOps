using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using KeyVaultComparer.Api.Services;

namespace KeyVaultComparer.Api.Services.Cache
{
    public class CacheItem
    {
        public string Key { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ValueJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class CacheStats
    {
        public bool Enabled { get; set; }
        public bool IsSessionUnlocked { get; set; }
        public int ItemCount { get; set; }
        public long FileSizeBytes { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime? OldestCreatedAt { get; set; }
        public DateTime? NewestCreatedAt { get; set; }
        public CacheTtlSettings Settings { get; set; } = new();
    }

    public class CacheTtlSettings
    {
        public bool Enabled { get; set; } = true;
        public int PipelinesTtlMinutes { get; set; } = 60;
        public int PipelineRunsTtlMinutes { get; set; } = 15;
        public int VaultKeysTtlMinutes { get; set; } = 120;
        public int VariableGroupsTtlMinutes { get; set; } = 30;
    }

    public class EncryptedCacheService
    {
        private readonly string _cacheFilePath;
        private readonly AppSettingsService _appSettingsService;
        private readonly TokenCredential _credential;
        private readonly SemaphoreSlim _keyLock = new(1, 1);
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        private byte[]? _sessionKey;
        private bool _isInitialized = false;
        private readonly ConcurrentDictionary<string, CacheItem> _inMemoryCache = new(StringComparer.OrdinalIgnoreCase);

        public EncryptedCacheService(AppSettingsService appSettingsService, TokenCredential credential)
        {
            _appSettingsService = appSettingsService;
            _credential = credential;

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
            _cacheFilePath = Path.Combine(baseDir, "cache.enc");
        }

        /// <summary>
        /// Derives the 256-bit AES-GCM session key in RAM using the active Azure authentication token.
        /// If the user is not authenticated with Azure, this returns null and cache cannot be accessed.
        /// </summary>
        public async Task<byte[]?> GetOrCreateSessionKeyAsync(CancellationToken ct = default)
        {
            if (_sessionKey != null && _sessionKey.Length == 32)
            {
                return _sessionKey;
            }

            await _keyLock.WaitAsync(ct);
            try
            {
                if (_sessionKey != null && _sessionKey.Length == 32)
                {
                    return _sessionKey;
                }

                // Request active Azure Token to act as the primary Input Keying Material (IKM)
                AccessToken token;
                try
                {
                    var tokenContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                    token = await _credential.GetTokenAsync(tokenContext, ct);
                }
                catch (Exception ex)
                {
                    // User has not authenticated or token is unavailable
                    Console.WriteLine($"[EncryptedCacheService] Azure token unavailable for key derivation: {ex.Message}");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(token.Token))
                {
                    return null;
                }

                // 1. IKM derived from verified immutable Azure Entra user identity (TenantId + ObjectId)
                string identityKey = ExtractIdentityKeyFromJwt(token.Token) ?? "AuthenticatedAzureUser";
                byte[] ikm = SHA256.HashData(Encoding.UTF8.GetBytes(identityKey));

                // 2. Salt derived from Windows DPAPI for machine/user boundary
                byte[] salt;
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        salt = ProtectedData.Protect(
                            Encoding.UTF8.GetBytes("KeyVaultComparer-Option1-Entropy-v1"), 
                            null, 
                            DataProtectionScope.CurrentUser
                        );
                    }
                    catch
                    {
                        salt = Encoding.UTF8.GetBytes("KeyVaultComparer-Option1-Entropy-v1");
                    }
                }
                else
                {
                    salt = Encoding.UTF8.GetBytes("KeyVaultComparer-Option1-Entropy-v1");
                }

                // 3. Info label for HKDF context
                byte[] info = Encoding.UTF8.GetBytes("KeyVaultComparer:Option1:AES256GCM:EncryptedCache");

                // 4. Derive the 32-byte (256-bit) AES key strictly in RAM
                _sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt, info);

                // Load and decrypt existing cache file from disk if not yet initialized
                if (!_isInitialized)
                {
                    await LoadAndDecryptCacheFromDiskAsync(_sessionKey);
                    _isInitialized = true;
                }

                return _sessionKey;
            }
            finally
            {
                _keyLock.Release();
            }
        }

        private static string? ExtractIdentityKeyFromJwt(string jwtToken)
        {
            try
            {
                var parts = jwtToken.Split('.');
                if (parts.Length < 2) return null;

                var payloadStr = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payloadStr.Length % 4)
                {
                    case 2: payloadStr += "=="; break;
                    case 3: payloadStr += "="; break;
                }

                var payloadBytes = Convert.FromBase64String(payloadStr);
                using var doc = JsonDocument.Parse(payloadBytes);
                var root = doc.RootElement;

                string? tid = root.TryGetProperty("tid", out var t) ? t.GetString() : null;
                string? oid = root.TryGetProperty("oid", out var o) ? o.GetString() : null;
                string? sub = root.TryGetProperty("sub", out var s) ? s.GetString() : null;
                string? upn = root.TryGetProperty("upn", out var u) ? u.GetString() : null;
                string? email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
                string? appid = root.TryGetProperty("appid", out var a) ? a.GetString() : null;

                string userIdentifier = oid ?? sub ?? upn ?? email ?? appid ?? "default_user";
                string tenantIdentifier = tid ?? "default_tenant";

                return $"{tenantIdentifier}:{userIdentifier}";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clears the in-memory session key (zero-filling RAM) and empties cache memory upon logout.
        /// </summary>
        public void ClearSessionKey()
        {
            if (_sessionKey != null)
            {
                CryptographicOperations.ZeroMemory(_sessionKey);
                _sessionKey = null;
            }
            _inMemoryCache.Clear();
            _isInitialized = false;
        }

        private async Task LoadAndDecryptCacheFromDiskAsync(byte[] key)
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    return;
                }

                byte[] encryptedData = await File.ReadAllBytesAsync(_cacheFilePath);
                if (encryptedData.Length < 1 + 12 + 16)
                {
                    return;
                }

                byte version = encryptedData[0];
                if (version != 1)
                {
                    Console.WriteLine($"[EncryptedCacheService] Unknown cache version {version}, starting fresh.");
                    return;
                }

                byte[] nonce = new byte[12];
                byte[] tag = new byte[16];
                int cipherTextLen = encryptedData.Length - 1 - 12 - 16;
                byte[] cipherText = new byte[cipherTextLen];

                Buffer.BlockCopy(encryptedData, 1, nonce, 0, 12);
                Buffer.BlockCopy(encryptedData, 1 + 12, tag, 0, 16);
                Buffer.BlockCopy(encryptedData, 1 + 12 + 16, cipherText, 0, cipherTextLen);

                byte[] plainBytes = new byte[cipherTextLen];
                try
                {
                    using var aesGcm = new AesGcm(key, 16);
                    aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

                    string json = Encoding.UTF8.GetString(plainBytes);
                    var items = JsonSerializer.Deserialize<List<CacheItem>>(json);
                    if (items != null)
                    {
                        var now = DateTime.UtcNow;
                        foreach (var item in items)
                        {
                            if (item.ExpiresAt > now)
                            {
                                _inMemoryCache[item.Key] = item;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EncryptedCacheService] Note: Existing cache file could not be decrypted ({ex.Message}). Starting with fresh cache.");
                    try
                    {
                        if (File.Exists(_cacheFilePath))
                        {
                            File.Delete(_cacheFilePath);
                        }
                    }
                    catch {}
                    _inMemoryCache.Clear();
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task SaveCacheToDiskAsync()
        {
            byte[]? key = _sessionKey;
            if (key == null || key.Length != 32)
            {
                return;
            }

            await _fileLock.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var activeItems = _inMemoryCache.Values.Where(i => i.ExpiresAt > now).ToList();

                string json = JsonSerializer.Serialize(activeItems);
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);

                byte[] nonce = RandomNumberGenerator.GetBytes(12);
                byte[] tag = new byte[16];
                byte[] cipherText = new byte[plainBytes.Length];

                using var aesGcm = new AesGcm(key, 16);
                aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

                byte[] output = new byte[1 + 12 + 16 + cipherText.Length];
                output[0] = 1; // version 1
                Buffer.BlockCopy(nonce, 0, output, 1, 12);
                Buffer.BlockCopy(tag, 0, output, 1 + 12, 16);
                Buffer.BlockCopy(cipherText, 0, output, 1 + 12 + 16, cipherText.Length);

                await File.WriteAllBytesAsync(_cacheFilePath, output);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EncryptedCacheService] Error saving encrypted cache to disk: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<T?> GetAsync<T>(string key, string category, CancellationToken cancellationToken = default)
        {
            var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
            if (!settings.CacheEnabled)
            {
                return default;
            }

            var keyBytes = await GetOrCreateSessionKeyAsync(cancellationToken);
            if (keyBytes == null)
            {
                return default;
            }

            if (_inMemoryCache.TryGetValue(key, out var item))
            {
                if (DateTime.UtcNow > item.ExpiresAt)
                {
                    _inMemoryCache.TryRemove(key, out _);
                    return default;
                }

                try
                {
                    return JsonSerializer.Deserialize<T>(item.ValueJson);
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }

        public async Task SetAsync<T>(string key, T value, string category, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var settings = await _appSettingsService.GetSettingsAsync(cancellationToken);
            if (!settings.CacheEnabled)
            {
                return;
            }

            var keyBytes = await GetOrCreateSessionKeyAsync(cancellationToken);
            if (keyBytes == null)
            {
                return;
            }

            if (!ttl.HasValue)
            {
                int minutes = category.ToLowerInvariant() switch
                {
                    "pipelines" => settings.CachePipelinesTtlMinutes,
                    "pipelineruns" => settings.CachePipelineRunsTtlMinutes,
                    "vaultkeys" => settings.CacheVaultKeysTtlMinutes,
                    "variablegroups" => settings.CacheVariableGroupsTtlMinutes,
                    _ => 60
                };
                ttl = TimeSpan.FromMinutes(Math.Max(1, minutes));
            }

            var now = DateTime.UtcNow;
            var item = new CacheItem
            {
                Key = key,
                Category = category,
                ValueJson = JsonSerializer.Serialize(value),
                CreatedAt = now,
                ExpiresAt = now.Add(ttl.Value)
            };

            _inMemoryCache[key] = item;

            // Persist encrypted cache to disk in background
            _ = Task.Run(async () => await SaveCacheToDiskAsync(), CancellationToken.None);
        }

        public async Task ClearAllAsync(CancellationToken ct = default)
        {
            _inMemoryCache.Clear();

            await _fileLock.WaitAsync(ct);
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task ClearCategoryAsync(string category, CancellationToken ct = default)
        {
            var keysToRemove = _inMemoryCache.Values
                .Where(v => string.Equals(v.Category, category, StringComparison.OrdinalIgnoreCase))
                .Select(v => v.Key)
                .ToList();

            foreach (var k in keysToRemove)
            {
                _inMemoryCache.TryRemove(k, out _);
            }

            await SaveCacheToDiskAsync();
        }

        public async Task<CacheStats> GetStatsAsync(CancellationToken ct = default)
        {
            var settings = await _appSettingsService.GetSettingsAsync(ct);
            long fileSize = 0;
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    fileSize = new FileInfo(_cacheFilePath).Length;
                }
            }
            catch { }

            var items = _inMemoryCache.Values.ToList();
            var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var cat = string.IsNullOrWhiteSpace(item.Category) ? "Other" : item.Category;
                categoryCounts[cat] = categoryCounts.TryGetValue(cat, out int c) ? c + 1 : 1;
            }

            return new CacheStats
            {
                Enabled = settings.CacheEnabled,
                IsSessionUnlocked = _sessionKey != null && _sessionKey.Length == 32,
                ItemCount = items.Count,
                FileSizeBytes = fileSize,
                CategoryCounts = categoryCounts,
                OldestCreatedAt = items.Any() ? items.Min(i => i.CreatedAt) : null,
                NewestCreatedAt = items.Any() ? items.Max(i => i.CreatedAt) : null,
                Settings = new CacheTtlSettings
                {
                    Enabled = settings.CacheEnabled,
                    PipelinesTtlMinutes = settings.CachePipelinesTtlMinutes,
                    PipelineRunsTtlMinutes = settings.CachePipelineRunsTtlMinutes,
                    VaultKeysTtlMinutes = settings.CacheVaultKeysTtlMinutes,
                    VariableGroupsTtlMinutes = settings.CacheVariableGroupsTtlMinutes
                }
            };
        }
    }
}
