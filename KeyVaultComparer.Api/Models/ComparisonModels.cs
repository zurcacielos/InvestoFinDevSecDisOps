using System.Collections.Generic;
using System.Linq;

namespace KeyVaultComparer.Api.Models
{
    public class VaultComparisonRequest
    {
        public List<string>? VaultUris { get; set; }
        public string? NameFilter { get; set; }
        public int Limit { get; set; } = 10;
    }

    public class SecretValuesRequest
    {
        public string VaultUri { get; set; } = string.Empty;
        public List<string> SecretNames { get; set; } = new();
    }

    public class SecretComparisonRow
    {
        public string SecretName { get; set; } = string.Empty;
        
        // Key is Vault URI, Value is the status/value from that vault
        public Dictionary<string, SecretValueStatus> VaultValues { get; set; } = new();
        
        public string GlobalStatus { get; set; } = "Missing"; // Match, Mismatch, Missing
    }

    public class SecretValueStatus
    {
        public string? Value { get; set; }
        
        /// <summary>
        /// "Match", "Mismatch", "Missing"
        /// </summary>
        public string Status { get; set; } = "Missing";
        public int ColorIndex { get; set; } = 0;
        public string? ErrorMessage { get; set; }
    }

    public class DiscoveredVault
    {
        public string Name { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
    }

    public class SecretMetadata
    {
        public string Name { get; set; } = string.Empty;
        public System.DateTimeOffset? CreatedOn { get; set; }
        public System.DateTimeOffset? UpdatedOn { get; set; }
        public System.DateTimeOffset? ExpiresOn { get; set; }
    }

    public class VaultSyncResult
    {
        public List<SecretMetadata> Secrets { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class StagedChangeRequest
    {
        public string VaultUri { get; set; } = string.Empty;
        public string SecretName { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
    }
}
