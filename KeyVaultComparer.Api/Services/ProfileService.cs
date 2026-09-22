using System;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        public ProfileService(TokenCredential credential)
        {
            _credential = credential;
        }

        public async Task<UserProfile> GetProfileAsync()
        {
            var profile = new UserProfile
            {
                Email = "Unknown User",
                SubscriptionName = "Unknown Subscription",
                Initials = "??"
            };

            try
            {
                // 1. Get the default subscription name using ArmClient
                var armClient = new ArmClient(_credential);
                var defaultSub = await armClient.GetDefaultSubscriptionAsync();
                profile.SubscriptionName = defaultSub.Data.DisplayName ?? profile.SubscriptionName;

                // 2. Extract email from Token JWT
                var tokenRequest = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var token = await _credential.GetTokenAsync(tokenRequest, new System.Threading.CancellationToken());
                
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
