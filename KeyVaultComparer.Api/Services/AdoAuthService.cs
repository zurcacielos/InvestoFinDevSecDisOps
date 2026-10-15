using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using KeyVaultComparer.Api.Services.SecretStore;

namespace KeyVaultComparer.Api.Services
{
    public class AdoAuthService
    {
        private readonly TokenCredential _credential;
        private readonly SecretStoreManager _secretStoreManager;
        private readonly IHttpClientFactory _httpClientFactory;

        public AdoAuthService(TokenCredential credential, SecretStoreManager secretStoreManager, IHttpClientFactory httpClientFactory)
        {
            _credential = credential;
            _secretStoreManager = secretStoreManager;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<AuthenticationHeaderValue> GetAdoAuthHeaderAsync(CancellationToken ct = default)
        {
            var pat = await _secretStoreManager.GetSecretAsync(SecretStoreManager.AdoPatKey, ct);
            if (!string.IsNullOrWhiteSpace(pat))
            {
                var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
                return new AuthenticationHeaderValue("Basic", basicToken);
            }

            var tokenContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
            var token = await _credential.GetTokenAsync(tokenContext, ct);
            return new AuthenticationHeaderValue("Bearer", token.Token);
        }

        public async Task<(bool Success, string Message, string AuthMethod)> TestAdoConnectionAsync(string organization, string project, string? explicitPat = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(organization))
            {
                return (false, "Organization name cannot be empty.", "None");
            }

            string authMethod = "AzureSSO";
            try
            {
                using var client = _httpClientFactory.CreateClient();
                
                if (!string.IsNullOrWhiteSpace(explicitPat))
                {
                    authMethod = "PAT";
                    var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{explicitPat}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
                }
                else
                {
                    var storedPat = await _secretStoreManager.GetSecretAsync(SecretStoreManager.AdoPatKey, ct);
                    if (!string.IsNullOrWhiteSpace(storedPat))
                    {
                        authMethod = "PAT";
                        var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{storedPat}"));
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
                    }
                    else
                    {
                        authMethod = "AzureSSO";
                        var tokenContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
                        var token = await _credential.GetTokenAsync(tokenContext, ct);
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                    }
                }

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                string url = string.IsNullOrWhiteSpace(project)
                    ? $"https://dev.azure.com/{organization}/_apis/projects?api-version=7.1&$top=1"
                    : $"https://dev.azure.com/{organization}/_apis/projects/{Uri.EscapeDataString(project)}?api-version=7.1";

                var resp = await client.GetAsync(url, ct);
                var content = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                {
                    if (content.TrimStart().StartsWith("<"))
                    {
                        return (false, "ADO returned an HTML login page. Your Azure SSO account or PAT lacks authorization for this organization.", authMethod);
                    }

                    var root = JsonNode.Parse(content);
                    string projName = root?["name"]?.ToString() ?? project;
                    string methodLabel = authMethod == "PAT" ? "Personal Access Token (PAT)" : "Azure SSO Identity";
                    return (true, string.IsNullOrWhiteSpace(project) 
                        ? $"Connected to Azure DevOps organization '{organization}' using {methodLabel}." 
                        : $"Connected to project '{projName}' in '{organization}' using {methodLabel}.", authMethod);
                }
                else
                {
                    return (false, $"ADO API returned {resp.StatusCode}: {content.Substring(0, Math.Min(content.Length, 200))}", authMethod);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection test failed: {ex.Message}", authMethod);
            }
        }

        public async Task<(bool Success, string Message)> TestGithubConnectionAsync(string? baseUrl, string? organization, string? explicitToken = null, CancellationToken ct = default)
        {
            string urlBase = !string.IsNullOrWhiteSpace(baseUrl) ? baseUrl.TrimEnd('/') : "https://github.com";
            string token = explicitToken ?? (await _secretStoreManager.GetSecretAsync(SecretStoreManager.GithubTokenKey, ct) ?? "");

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KeyVaultComparer", "1.0"));

                if (!string.IsNullOrWhiteSpace(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Determine API endpoint depending on GitHub.com vs GitHub Enterprise
                string apiRoot;
                if (urlBase.Contains("github.com", StringComparison.OrdinalIgnoreCase))
                {
                    apiRoot = "https://api.github.com";
                }
                else
                {
                    apiRoot = $"{urlBase}/api/v3";
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    var userResp = await client.GetAsync($"{apiRoot}/user", ct);
                    if (userResp.IsSuccessStatusCode)
                    {
                        var json = await userResp.Content.ReadAsStringAsync(ct);
                        var node = JsonNode.Parse(json);
                        string login = node?["login"]?.ToString() ?? "authenticated user";
                        return (true, $"Successfully authenticated to GitHub as @{login}.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(organization))
                {
                    var orgResp = await client.GetAsync($"{apiRoot}/orgs/{Uri.EscapeDataString(organization)}", ct);
                    if (orgResp.IsSuccessStatusCode)
                    {
                        return (true, $"Successfully reached GitHub organization '{organization}'.");
                    }
                }

                // Fallback check
                var pingResp = await client.GetAsync($"{apiRoot}/zen", ct);
                if (pingResp.IsSuccessStatusCode)
                {
                    return (true, "Successfully connected to GitHub.");
                }

                return (false, $"Could not verify GitHub connection (Status {pingResp.StatusCode}).");
            }
            catch (Exception ex)
            {
                return (false, $"GitHub connection test failed: {ex.Message}");
            }
        }
    }
}
