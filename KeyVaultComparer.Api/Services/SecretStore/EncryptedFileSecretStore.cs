using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultComparer.Api.Services.SecretStore
{
    public class EncryptedFileSecretStore : ISecureSecretStore
    {
        public string ProviderName => "EncryptedFileStore";
        public string DisplayName => "Encrypted File Store (AES-256-GCM)";
        public string Description => "Encrypted with AES-256-GCM. Works cross-platform on Windows and Linux/Docker with zero plaintext.";
        public bool IsSupported => true;

        private readonly string _dataDir;
        private readonly string _secretsFilePath;
        private readonly string _masterKeyFilePath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public EncryptedFileSecretStore()
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

            _dataDir = baseDir;
            Directory.CreateDirectory(_dataDir);
            _secretsFilePath = Path.Combine(_dataDir, "secrets.enc");
            _masterKeyFilePath = Path.Combine(_dataDir, ".master.key");
        }

        private byte[] GetOrCreateMasterKey()
        {
            // 1. Check environment variable override (useful for Docker secrets / CI/CD)
            var envKey = Environment.GetEnvironmentVariable("KEYVAULTCOMPARER_MASTER_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                try
                {
                    var decoded = Convert.FromBase64String(envKey.Trim());
                    if (decoded.Length == 32) return decoded;
                }
                catch { }

                // Fallback: derive 32 bytes from string using SHA256
                return SHA256.HashData(Encoding.UTF8.GetBytes(envKey));
            }

            // 2. Check local persistent master key file
            if (File.Exists(_masterKeyFilePath))
            {
                var bytes = File.ReadAllBytes(_masterKeyFilePath);
                if (bytes.Length == 32) return bytes;
            }

            // 3. Generate a new cryptographically random 256-bit key
            var newKey = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(_masterKeyFilePath, newKey);

            // On Linux/Unix, set restrictive 0600 permissions if supported
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(_masterKeyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch { }
            }

            return newKey;
        }

        private Dictionary<string, string> ReadAllSecretsInternal()
        {
            if (!File.Exists(_secretsFilePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            byte[] encryptedData = File.ReadAllBytes(_secretsFilePath);
            if (encryptedData.Length < 1 + 12 + 16)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            byte version = encryptedData[0];
            if (version != 1)
            {
                throw new InvalidOperationException($"Unsupported encrypted file version: {version}");
            }

            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            int cipherTextLen = encryptedData.Length - 1 - 12 - 16;
            byte[] cipherText = new byte[cipherTextLen];

            Buffer.BlockCopy(encryptedData, 1, nonce, 0, 12);
            Buffer.BlockCopy(encryptedData, 1 + 12, tag, 0, 16);
            Buffer.BlockCopy(encryptedData, 1 + 12 + 16, cipherText, 0, cipherTextLen);

            byte[] masterKey = GetOrCreateMasterKey();
            byte[] plainBytes = new byte[cipherTextLen];

            using var aesGcm = new AesGcm(masterKey, 16);
            aesGcm.Decrypt(nonce, cipherText, tag, plainBytes);

            string json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private void WriteAllSecretsInternal(Dictionary<string, string> dict)
        {
            string json = JsonSerializer.Serialize(dict);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            byte[] masterKey = GetOrCreateMasterKey();
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] cipherText = new byte[plainBytes.Length];

            using var aesGcm = new AesGcm(masterKey, 16);
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);

            byte[] output = new byte[1 + 12 + 16 + cipherText.Length];
            output[0] = 1; // version
            Buffer.BlockCopy(nonce, 0, output, 1, 12);
            Buffer.BlockCopy(tag, 0, output, 1 + 12, 16);
            Buffer.BlockCopy(cipherText, 0, output, 1 + 12 + 16, cipherText.Length);

            File.WriteAllBytes(_secretsFilePath, output);

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(_secretsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch { }
            }
        }

        public async Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var dict = ReadAllSecretsInternal();
                return dict.TryGetValue(key, out var val) ? val : null;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SetSecretAsync(string key, string value, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var dict = ReadAllSecretsInternal();
                dict[key] = value;
                WriteAllSecretsInternal(dict);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DeleteSecretAsync(string key, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var dict = ReadAllSecretsInternal();
                if (dict.Remove(key))
                {
                    WriteAllSecretsInternal(dict);
                }
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
