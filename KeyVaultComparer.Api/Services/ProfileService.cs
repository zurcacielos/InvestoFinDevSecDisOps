using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using KeyVaultComparer.Api.Models;

namespace KeyVaultComparer.Api.Services
{
    public class ProfileService
    {
        private readonly TokenCredential _credential;
        private readonly IHttpClientFactory _httpClientFactory;

        public ProfileService(TokenCredential credential, IHttpClientFactory httpClientFactory)
        {
            _credential = credential;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<UserProfile> GetProfileAsync(CancellationToken ct = default)
        {
            var profile = new UserProfile
            {
                Email = "Unknown User",
                SubscriptionName = "Unknown Subscription",
                Initials = "??"
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            try
            {
                // 1. Get the default subscription name using ArmClient (best effort)
                try
                {
                    var armClient = new ArmClient(_credential);
                    var defaultSub = await armClient.GetDefaultSubscriptionAsync(cts.Token);
                    profile.SubscriptionName = defaultSub.Data.DisplayName ?? profile.SubscriptionName;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Default subscription discovery note: {ex.Message}");
                }

                // 2. Extract email from Token JWT
                var tokenRequest = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var token = await _credential.GetTokenAsync(tokenRequest, cts.Token);
                
                var jwtParts = token.Token.Split('.');
                if (jwtParts.Length >= 2)
                {
                    // Fix Base64Url padding for the payload
                    var payloadStr = jwtParts[1];
                    payloadStr = payloadStr.Replace('-', '+').Replace('_', '/');
                    switch (payloadStr.Length % 4)
                    {
                        case 2: payloadStr += "=="; break;
                        case 3: payloadStr += "="; break;
                    }

                    var payloadBytes = Convert.FromBase64String(payloadStr);
                    var payloadJson = Encoding.UTF8.GetString(payloadBytes);

                    using var doc = JsonDocument.Parse(payloadJson);
                    var root = doc.RootElement;
                    
                    string? email = null;
                    if (root.TryGetProperty("upn", out var upnProp))
                        email = upnProp.GetString();
                    else if (root.TryGetProperty("email", out var emailProp))
                        email = emailProp.GetString();
                    else if (root.TryGetProperty("unique_name", out var uniqueNameProp))
                        email = uniqueNameProp.GetString();

                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        profile.Email = email;
                        profile.Initials = GetInitials(email);
                    }
                }

                // 3. Attempt to fetch profile photo (Microsoft Graph primary, ADO custom avatar fallback)
                profile.PhotoUrl = await TryFetchUserPhotoAsync(cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching profile via injected credential: {ex.Message}");
                if (ex is AuthenticationFailedException || ex is CredentialUnavailableException || 
                    (ex is Azure.RequestFailedException rfe && (rfe.Status == 401 || rfe.Status == 403)) ||
                    ex.ToString().Contains("AADSTS") || ex.ToString().Contains("az login") ||
                    ex.ToString().Contains("No subscriptions found"))
                {
                    throw; // Bubble up to trigger 401 in middleware
                }
            }

            return profile;
        }

        private async Task<string?> TryFetchUserPhotoAsync(CancellationToken ct)
        {
            // 1. Primary: Microsoft Graph (Corporate Entra ID / Office 365 headshot)
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                var graphContext = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
                var graphToken = await _credential.GetTokenAsync(graphContext, timeoutCts.Token);

                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", graphToken.Token);

                var graphResp = await client.GetAsync("https://graph.microsoft.com/v1.0/me/photo/$value", timeoutCts.Token);
                if (graphResp.IsSuccessStatusCode)
                {
                    var bytes = await graphResp.Content.ReadAsByteArrayAsync(timeoutCts.Token);
                    if (bytes.Length > 0)
                    {
                        var contentType = graphResp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Microsoft Graph photo note: {ex.Message}");
            }

            // 2. Fallback: Azure DevOps user profile avatar (if a custom non-autogenerated photo was uploaded)
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                var adoContext = new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" });
                var adoToken = await _credential.GetTokenAsync(adoContext, timeoutCts.Token);

                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adoToken.Token);

                var adoResp = await client.GetAsync("https://app.vssps.visualstudio.com/_apis/profile/profiles/me?api-version=7.1-preview.1", timeoutCts.Token);
                if (adoResp.IsSuccessStatusCode)
                {
                    var json = await adoResp.Content.ReadAsStringAsync(timeoutCts.Token);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("coreAttributes", out var attrs) &&
                        attrs.TryGetProperty("Avatar", out var avatarProp) &&
                        avatarProp.TryGetProperty("value", out var valProp))
                    {
                        bool isAutoGenerated = false;
                        if (valProp.TryGetProperty("isAutoGenerated", out var autoProp) && autoProp.ValueKind == JsonValueKind.True)
                        {
                            isAutoGenerated = true;
                        }

                        if (!isAutoGenerated && valProp.TryGetProperty("value", out var base64Prop))
                        {
                            var base64 = base64Prop.GetString();
                            if (!string.IsNullOrWhiteSpace(base64))
                            {
                                return $"data:image/png;base64,{base64}";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Azure DevOps profile avatar note: {ex.Message}");
            }

            return null;
        }

        private string GetInitials(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "??";
            
            var namePart = email.Split('@')[0];
            var parts = namePart.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            else if (namePart.Length >= 2)
            {
                return namePart.Substring(0, 2).ToUpper();
            }
            
            return namePart.Substring(0, 1).ToUpper();
        }
    }
}
