using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultComparer.Api.Services.SecretStore
{
    public interface ISecureSecretStore
    {
        string ProviderName { get; }
        string DisplayName { get; }
        string Description { get; }
        bool IsSupported { get; }
        Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
        Task SetSecretAsync(string key, string value, CancellationToken ct = default);
        Task DeleteSecretAsync(string key, CancellationToken ct = default);
    }
}
