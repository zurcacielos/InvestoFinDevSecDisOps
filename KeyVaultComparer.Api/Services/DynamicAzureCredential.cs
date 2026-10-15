using System;
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

        public DynamicAzureCredential(
            SecretStoreManager secretStoreManager,
            AppSettingsService appSettingsService,
            TokenCredential defaultCliCredential)
        {
            _secretStoreManager = secretStoreManager;
            _appSettingsService = appSettingsService;
            _defaultCliCredential = defaultCliCredential;
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
