export type UsageFilterMode = 'None' | 'Unused' | 'UsedInLast' | 'NotUsedInLast' | 'UsedBetween';
export type UsageFilterUnit = 'days' | 'months' | 'years';

import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useUsageStore = defineStore('usage', () => {
  const usageData = ref<Record<string, string>>({});
  const isAuditingEnabled = ref(true);
  const auditMissingVaults = ref<any[]>([]);
  const isFetchingUsage = ref(false);
  const insightCount = ref<number | null>(null);

  const filterMode = ref<UsageFilterMode>('None');
  const filterValue = ref<number>(30);
  const filterUnit = ref<UsageFilterUnit>('days');
  const filterStartDate = ref<string>('');
  const filterEndDate = ref<string>('');

  const fetchUsageStats = async (vaultUris: string[]) => {
    isFetchingUsage.value = true;
    insightCount.value = null;
    try {
      const response = await fetch('http://localhost:5065/api/vaults/usage', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(vaultUris)
      });

      if (!response.ok) {
        throw new Error(`API error: ${response.statusText}`);
      }

      const data = await response.json();
      usageData.value = data.usageData || {};
      isAuditingEnabled.value = data.isAuditingEnabled;
      auditMissingVaults.value = data.auditMissingVaults || [];
      
      insightCount.value = Object.keys(usageData.value).length;
    } catch (error) {
      console.error('Error fetching usage stats:', error);
      isAuditingEnabled.value = false;
    } finally {
      isFetchingUsage.value = false;
    }
  };

  const clearFilters = () => {
    filterMode.value = 'None';
    filterValue.value = 30;
    filterUnit.value = 'days';
    filterStartDate.value = '';
    filterEndDate.value = '';
  };

  const downloadAuditScript = () => {
    if (auditMissingVaults.value.length === 0) return;

    let script = `# Azure Monitor Diagnostic Settings Configuration Script\n`;
    script += `# Please fill in the target Log Analytics Workspace ARM ID before executing.\n\n`;
    script += `$WorkspaceId = "<YOUR_LOG_ANALYTICS_WORKSPACE_ARM_ID>"\n\n`;

    auditMissingVaults.value.forEach(v => {
      const vaultName = v.name || 'kv';
      script += `# Configure audit for ${vaultName}\n`;
      script += `$output = az monitor diagnostic-settings create --name "KV-Audit-Comparer" \`\n`;
      script += `  --resource "${v.armId}" \`\n`;
      script += `  --workspace $WorkspaceId \`\n`;
      script += `  --logs "[{categoryGroup:audit,enabled:true}]" 2>&1\n\n`;
      script += `if ($LASTEXITCODE -ne 0) {\n`;
      script += `  if ($output -match 'Microsoft.Insights') {\n`;
      script += `    Write-Host "ERROR: Microsoft.Insights resource provider is not registered." -ForegroundColor Red\n`;
      script += `    Write-Host "Please run the following command to register it and then try again:" -ForegroundColor Yellow\n`;
      script += `    Write-Host "az provider register --namespace 'Microsoft.Insights' --wait" -ForegroundColor Cyan\n`;
      script += `  } else {\n`;
      script += `    Write-Host "An error occurred:" -ForegroundColor Red\n`;
      script += `    Write-Host $output\n`;
      script += `  }\n`;
      script += `  exit 1\n`;
      script += `}\n\n`;
    });

    const blob = new Blob([script], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'enable-kv-audit.ps1';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  return {
    usageData,
    isAuditingEnabled,
    auditMissingVaults,
    isFetchingUsage,
    fetchUsageStats,
    insightCount,
    downloadAuditScript,
    filterMode,
    filterValue,
    filterUnit,
    filterStartDate,
    filterEndDate,
    clearFilters
  };
});
