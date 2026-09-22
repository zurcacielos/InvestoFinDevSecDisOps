namespace KeyVaultComparer.Api.Models
{
    public class UserProfile
    {
        public string Email { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
    }

    public class AzureSubscription
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
