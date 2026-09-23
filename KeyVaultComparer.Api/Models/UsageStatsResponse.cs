using System;
using System.Collections.Generic;

namespace KeyVaultComparer.Api.Models
{
    public class UsageStatsResponse
    {
        public bool IsAuditingEnabled { get; set; } = true;
        public List<AuditMissingVaultInfo> AuditMissingVaults { get; set; } = new List<AuditMissingVaultInfo>();
        public Dictionary<string, DateTime> UsageData { get; set; } = new Dictionary<string, DateTime>();
    }

    public class AuditMissingVaultInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ArmId { get; set; } = string.Empty;
        public string VaultUri { get; set; } = string.Empty;
    }
}
