<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { storeToRefs } from 'pinia';

import AppHeader from './components/Layout/AppHeader.vue';
import VaultSelector from './components/VaultManagement/VaultSelector.vue';
import FilterSection from './components/Filters/FilterSection.vue';
import InspectionsToolSection from './components/Filters/InspectionsToolSection.vue';
import GridTable from './components/ComparisonGrid/GridTable.vue';
import StatusBar from './components/UI/StatusBar.vue';
import AuthErrorModal from './components/Modals/AuthErrorModal.vue';
import RegexHelpModal from './components/Modals/RegexHelpModal.vue';
import QueryModal from './components/Modals/QueryModal.vue';
import HelpModal from './components/Modals/HelpModal.vue';
import GrantAccessModal from './components/Modals/GrantAccessModal.vue';
import InfoTooltip from './components/UI/InfoTooltip.vue';
import { useAuthStore } from './stores/authStore';
import { useVaultStore } from './stores/vaultStore';
import { useSettingsStore } from './stores/settingsStore';
import { useFilterStore } from './stores/filterStore';
import { useStagedStore } from './stores/stagedStore';
import { useUiStateStore } from './stores/uiStateStore';
import { useUsageStore } from './stores/usageStore';
import { useDevopsDataStore } from './stores/devopsDataStore';
import { useInspectionStore } from './stores/inspectionStore';
import { useSecurityAnalysis } from './composables/useSecurityAnalysis';



const authStore = useAuthStore();
const { showAuthError } = storeToRefs(authStore);

const vaultStore = useVaultStore();
const { vaultUris, vaultData } = storeToRefs(vaultStore);

const settingsStore = useSettingsStore();
const { uiSettings } = storeToRefs(settingsStore);

const filterStore = useFilterStore();
const { allSortedNames } = storeToRefs(filterStore);

const stagedStore = useStagedStore();
const { stagedChanges } = storeToRefs(stagedStore);

const visibleStagedSecrets = ref(new Set<string>());
const toggleStagedSecretVisibility = (vaultUri: string, secretName: string) => {
  const key = `${vaultUri}-${secretName}`;
  if (visibleStagedSecrets.value.has(key)) {
    visibleStagedSecrets.value.delete(key);
  } else {
    visibleStagedSecrets.value.add(key);
  }
};

const usageStore = useUsageStore();
const devopsDataStore = useDevopsDataStore();
const inspectionStore = useInspectionStore();

const { results, rowUsageCount, colUsageCount, vulnerableValuesMap } = useSecurityAnalysis();

// DevOps Search State
const devopsSearchQuery = ref('');
const devopsShowDropdown = ref(false);

const filteredVariableGroups = computed(() => {
  const query = devopsSearchQuery.value.trim().toLowerCase();
  let groups = devopsDataStore.variableGroups;
  if (query) {
    groups = groups.filter(g => {
      const gName = g.name.toLowerCase();
      const vName = (g.providerData?.vault || '').toLowerCase();
      return gName.includes(query) || vName.includes(query);
    });
  }
  return groups.filter(g => !devopsDataStore.selectedGroupIds.includes(g.id));
});

const selectVariableGroup = (groupId: number) => {
  if (!devopsDataStore.selectedGroupIds.includes(groupId)) {
    devopsDataStore.toggleGroupSelection(groupId);
  }
};

const hideDevopsDropdown = () => {
  setTimeout(() => { devopsShowDropdown.value = false; }, 200);
};

const devopsContextMenu = ref<{ show: boolean, x: number, y: number, group: import('./stores/devopsDataStore').AdoVariableGroup | null }>({ show: false, x: 0, y: 0, group: null });

const hideDevopsContextMenu = () => {
  devopsContextMenu.value.show = false;
  window.removeEventListener('click', hideDevopsContextMenu);
  window.removeEventListener('keydown', handleDevopsContextMenuEsc);
};

const handleDevopsContextMenuEsc = (e: KeyboardEvent) => {
  if (e.key === 'Escape') hideDevopsContextMenu();
};

const showDevopsContextMenu = (e: MouseEvent, group: import('./stores/devopsDataStore').AdoVariableGroup) => {
  devopsContextMenu.value = { show: true, x: e.clientX, y: e.clientY, group };
  setTimeout(() => {
    window.addEventListener('click', hideDevopsContextMenu);
    window.addEventListener('keydown', handleDevopsContextMenuEsc);
  }, 0);
};

const uiStateStore = useUiStateStore();
const { currentTab } = storeToRefs(uiStateStore);
const showHelpDialog = ref(false);
const showRegexHelpDialog = ref(false);
const showQueryModal = ref(false);
const showGrantAccessModal = ref(false);

const useGithubOrgInput = ref(settingsStore.uiSettings.useGithubOrg);
const githubOrgInput = ref(settingsStore.uiSettings.githubOrg);
const githubBaseUrlInput = ref(settingsStore.uiSettings.githubBaseUrl);
const saveGithubSettings = () => {
  settingsStore.uiSettings.useGithubOrg = useGithubOrgInput.value;
  settingsStore.uiSettings.githubOrg = githubOrgInput.value;
  settingsStore.uiSettings.githubBaseUrl = githubBaseUrlInput.value;
  settingsStore.saveUiSettings();
};

const hasFetchedValues = computed(() => {
  return Object.values(vaultData.value).some(vault => 
    Object.values(vault).some(v => v.status !== 'Missing' && v.status !== 'Not Retrieved' && v.status !== 'Loading')
  );
});

// Inspections
const runInspections = () => inspectionStore.runInspectionsOnVisible(results.value, vulnerableValuesMap.value);

const inspectionCounts = computed(() => {
  const counts = { Any: 0, Critical: 0, High: 0, Medium: 0, Low: 0 };
  if (!inspectionStore.hasInspectionsRun) return counts;

  let baseRes = results.value;
  if (uiSettings.value.showStagedOnly) {
    baseRes = baseRes.filter(row => 
      stagedChanges.value.some(s => s.secretName === row.secretName && vaultUris.value.includes(s.vaultUri))
    );
  }

  baseRes.forEach(row => {
    vaultUris.value.forEach(uri => {
      const val = row.vaultValues[uri];
      if (val && val.inspections && val.inspections.length > 0) {
        counts.Any++;
        counts[val.highestSeverity || 'Low']++;
      }
    });
  });
  return counts;
});

const inspectionReportData = computed(() => {
  const report: Array<{vault: string, secret: string, rule: string, message: string, severity: 'Low'|'Medium'|'High'|'Critical'}> = [];
  if (!inspectionStore.hasInspectionsRun) return report;
  
  let baseRes = results.value;
  if (uiSettings.value.showStagedOnly) {
    baseRes = baseRes.filter(row => 
      stagedChanges.value.some(s => s.secretName === row.secretName && vaultUris.value.includes(s.vaultUri))
    );
  }

  baseRes.forEach(row => {
    vaultUris.value.forEach(uri => {
      const val = row.vaultValues[uri];
      if (val && val.inspections && val.inspections.length > 0) {
        let vaultName = uri;
        try { vaultName = new URL(uri).hostname.split('.')[0]; } catch {}
        val.inspections.forEach(i => {
          report.push({
            vault: vaultName,
            secret: row.secretName,
            rule: i.ruleName,
            message: i.message,
            severity: i.severity
          });
        });
      }
    });
  });
  
  return report.sort((a, b) => {
    const levels = { Critical: 4, High: 3, Medium: 2, Low: 1 };
    return levels[b.severity] - levels[a.severity];
  });
});

const filteredInspectionReportData = computed(() => {
  return inspectionReportData.value.filter(f => inspectionStore.inspectionSeverities[f.severity]);
});

const filteredResultsForGrid = computed(() => {
  let base = results.value;
  
  if (filterStore.inspectionFilter !== 'None' && inspectionStore.hasInspectionsRun) {
    base = base.filter(row => {
      return vaultUris.value.some(uri => {
        const val = row.vaultValues[uri];
        if (!val || !val.inspections || val.inspections.length === 0) return false;
        if (filterStore.inspectionFilter === 'Any') return true;
        return val.highestSeverity === filterStore.inspectionFilter;
      });
    });
  }

  if (uiSettings.value.statusFilter !== 'Any') {
    if (uiSettings.value.statusFilter === 'Missing') {
      base = base.filter(row => Object.values(row.vaultValues).some(v => v.status === 'Missing' || v.status === 'Not Retrieved'));
    } else if (uiSettings.value.statusFilter === '=') {
      base = base.filter(row => row.globalStatus === 'Match' || Object.values(row.vaultValues).some(v => v.status === 'Not Retrieved'));
    } else if (uiSettings.value.statusFilter === '≠') {
      base = base.filter(row => row.globalStatus === 'Mismatch' || Object.values(row.vaultValues).some(v => v.status === 'Not Retrieved'));
    }
  }

  if (uiSettings.value.securityByRow) {
    base = base.filter(row => {
      return vaultUris.value.some(uri => {
        const val = row.vaultValues[uri];
        if (!val || val.status === 'Not Retrieved') return true;
        if (val && val.status === 'Present' && val.value) {
          const key = row.secretName + val.value.toLowerCase();
          return (rowUsageCount.value.get(key) || 0) > 1;
        }
        return false;
      });
    });
  }

  if (uiSettings.value.securityByCol) {
    base = base.filter(row => {
      return vaultUris.value.some(uri => {
        const val = row.vaultValues[uri];
        if (!val || val.status === 'Not Retrieved') return true;
        if (val && val.status === 'Present' && val.value) {
          const key = uri + val.value.toLowerCase();
          return (colUsageCount.value.get(key) || 0) > 1;
        }
        return false;
      });
    });
  }

  if (uiSettings.value.showStagedOnly) {
    base = base.filter(row => 
      stagedChanges.value.some(s => s.secretName === row.secretName && vaultUris.value.includes(s.vaultUri))
    );
  }

  if (usageStore.filterMode !== 'None') {
    base = base.filter(row => {
      if (usageStore.filterMode === 'UnusedAll') {
        const presentVaults = vaultUris.value.filter(uri => {
          const val = row.vaultValues[uri];
          return val && val.status !== 'Missing' && val.status !== 'Not Retrieved';
        });
        
        if (presentVaults.length === 0) return false;
        
        return presentVaults.every(uri => {
          const key = `${uri}_${row.secretName}`.toLowerCase();
          const d = usageStore.usageData[key];
          return !d;
        });
      }

      return vaultUris.value.some(uri => {
        const val = row.vaultValues[uri];
        if (!val || val.status === 'Missing' || val.status === 'Not Retrieved') return false;

        const key = `${uri}_${row.secretName}`.toLowerCase();
        const d = usageStore.usageData[key];
        const cellDate = d ? new Date(d).getTime() : 0;
        
        if (usageStore.filterMode === 'Unused') {
          return cellDate === 0;
        }
        
        if (cellDate === 0) return false;
        
        const now = Date.now();
        const diffInMs = Math.max(0, now - cellDate);
        const diffInDays = diffInMs / (1000 * 60 * 60 * 24);
        
        if (usageStore.filterMode === 'UsedInLast' || usageStore.filterMode === 'NotUsedInLast') {
          let thresholdDays = usageStore.filterValue;
          if (usageStore.filterUnit === 'months') thresholdDays *= 30;
          if (usageStore.filterUnit === 'years') thresholdDays *= 365;
          
          if (usageStore.filterMode === 'UsedInLast') {
            return diffInDays <= thresholdDays;
          } else {
            return diffInDays > thresholdDays;
          }
        }
        
        if (usageStore.filterMode === 'UsedBetween') {
          const start = usageStore.filterStartDate ? new Date(usageStore.filterStartDate).getTime() : 0;
          const end = usageStore.filterEndDate ? new Date(usageStore.filterEndDate).getTime() : Infinity;
          const adjustedEnd = end !== Infinity ? end + 86400000 - 1 : Infinity;
          return cellDate >= start && cellDate <= adjustedEnd;
        }
        
        return true;
      });
    });
  }

  return base;
});

const clearFilters = () => {
  filterStore.setNameFilter('');
  filterStore.setInspectionFilter('None');
  uiSettings.value.statusFilter = 'Any';
  uiSettings.value.showReusedValues = false;
  uiSettings.value.showStagedOnly = false;
  uiSettings.value.securityByRow = false;
  uiSettings.value.securityByCol = false;
  usageStore.clearFilters();
};

const getVaultName = (uri: string) => {
  try {
    return new URL(uri).hostname.split('.')[0];
  } catch {
    return uri;
  }
};

const downloadGrantScript = () => {
  let scriptContent = '# Grant Access to Vaults\n\n';
  scriptContent += '$userObjectId = az ad signed-in-user show --query id -o tsv\n';
  scriptContent += 'if ([string]::IsNullOrWhiteSpace($userObjectId)) { Write-Host "Failed to retrieve your Object ID. Ensure you are logged in with az login."; exit }\n\n';
  
  vaultUris.value.forEach(uri => {
    if (vaultStore.knownSecretNames[uri]?.errorMessage) {
      let vaultName = uri;
      try { vaultName = new URL(uri).hostname.split('.')[0]; } catch {}
      scriptContent += `Write-Host "Checking authorization model for ${vaultName}..."\n`;
      scriptContent += `$vault = az keyvault show --name "${vaultName}" | ConvertFrom-Json\n`;
      scriptContent += `if ($vault.properties.enableRbacAuthorization) {\n`;
      scriptContent += `    Write-Host "Vault uses RBAC. Assigning Key Vault Secrets Officer role..."\n`;
      scriptContent += `    az role assignment create --role "Key Vault Secrets Officer" --assignee-object-id $userObjectId --assignee-principal-type User --scope $vault.id\n`;
      scriptContent += `} else {\n`;
      scriptContent += `    Write-Host "Vault uses Access Policies. Granting get, list, set permissions..."\n`;
      scriptContent += `    az keyvault set-policy --name "${vaultName}" --object-id $userObjectId --secret-permissions get list set\n`;
      scriptContent += `}\n\n`;
    }
  });

  const blob = new Blob([scriptContent], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'grant-vault-permissions.ps1';
  a.click();
  URL.revokeObjectURL(url);

  showGrantAccessModal.value = false;
};

const downloadStagedScript = () => {
  if (stagedChanges.value.length === 0) return;
  let scriptContent = '# Apply Staged Changes locally\n\n';
  stagedChanges.value.forEach(change => {
    let vaultName = change.vaultUri;
    try { vaultName = new URL(change.vaultUri).hostname.split('.')[0]; } catch {}
    
    if (change.type === 'DELETE') {
      scriptContent += `az keyvault secret delete --vault-name "${vaultName}" --name "${change.secretName}"\n`;
    } else {
      scriptContent += `az keyvault secret set --vault-name "${vaultName}" --name "${change.secretName}" --value "${change.newValue}"\n`;
    }
  });
  
  const blob = new Blob([scriptContent], { type: 'text/plain' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'apply-staged-changes.ps1';
  a.click();
  URL.revokeObjectURL(url);
};

const copyInspectionsMarkdown = async () => {
  let md = '# Security Inspections Report\n\n';
  if (filteredInspectionReportData.value.length === 0) {
    md += 'No vulnerabilities found for the active filters.\n';
  } else {
    filteredInspectionReportData.value.forEach(f => {
      md += `### [${f.severity}] ${f.secret} @ ${f.vault}\n`;
      md += `**${f.rule}**: ${f.message}\n\n`;
    });
  }
  try {
    await navigator.clipboard.writeText(md);
    alert('Markdown copied to clipboard!');
  } catch (err) {
    alert('Failed to copy: ' + err);
  }
};

const downloadInspectionsCSV = () => {
  if (filteredInspectionReportData.value.length === 0) return;
  
  const headers = ['Severity', 'Vault', 'Secret Name', 'Rule', 'Message'];
  const rows = filteredInspectionReportData.value.map(f => [
    f.severity,
    f.vault,
    f.secret,
    f.rule,
    f.message.replace(/"/g, '""')
  ]);
  
  let csvContent = headers.join(',') + '\n';
  rows.forEach(row => {
    csvContent += row.map(cell => `"${cell}"`).join(',') + '\n';
  });
  
  const blob = new Blob([csvContent], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'inspections-report.csv';
  a.click();
  URL.revokeObjectURL(url);
};

const linkedVariableGroups = computed(() => {
  if (!devopsDataStore.variableGroups.length || !vaultStore.vaultUris.length) return [];
  const visibleVaultNames = vaultStore.vaultUris.map(uri => getVaultName(uri).toLowerCase());
  return devopsDataStore.variableGroups.filter(g => {
    return g.providerData?.vault && visibleVaultNames.includes(g.providerData.vault.toLowerCase());
  });
});

const hasLinkedGroups = computed(() => linkedVariableGroups.value.length > 0);

const areLinkedGroupsShown = computed(() => {
  if (!hasLinkedGroups.value) return false;
  return devopsDataStore.selectedGroupIds.length > 0;
});

const toggleLinkedGroups = () => {
  if (areLinkedGroupsShown.value) {
    devopsDataStore.selectedGroupIds = [];
  } else {
    devopsDataStore.selectedGroupIds = linkedVariableGroups.value.map(g => g.id);
  }
};

onMounted(async () => {
  await authStore.connectToAzure(); 
});
</script>

<template>
  <div class="h-screen w-screen flex flex-col bg-slate-50 text-slate-900 font-sans overflow-hidden">
    
    <AppHeader v-model="currentTab" @show-help="showHelpDialog = true">
      <template #select-vaults>
        <VaultSelector @grant-access="showGrantAccessModal = true" />
      </template>
      <template #analyze-data>
        <div class="flex flex-col lg:flex-row h-full">
          <FilterSection 
            :hasFetchedValues="hasFetchedValues"
            :filteredResultsLength="filteredResultsForGrid.length"
            :allSortedNamesLength="allSortedNames.length"
            :hasInspectionsRun="inspectionStore.hasInspectionsRun"
            :inspectionCounts="inspectionCounts"
            @fetch-comparison="vaultStore.fetchComparison()"
            @clear-filters="clearFilters"
            @show-regex-help="showRegexHelpDialog = true"
          />

          <!-- Vertical Divider -->
          <div class="hidden lg:block w-px bg-slate-200 self-stretch mx-6 lg:mx-8 shrink-0"></div>

          <!-- Usage Stats Section (Merged) -->
          <div class="grid grid-cols-[auto_1fr] gap-x-6 gap-y-3 py-1 min-w-max">
            <!-- Row 1, Col 1: Query Limit -->
            <div class="flex items-center gap-2 whitespace-nowrap">
              <span class="text-sm font-medium text-slate-600">Query Last:</span>
              <input type="number" v-model="usageStore.queryLimitValue" min="1" class="w-16 text-sm bg-slate-100 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 h-8" />
              <select v-model="usageStore.queryLimitUnit" class="text-sm bg-slate-100 hover:bg-slate-200 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer transition-colors h-8">
                <option value="days">Days</option>
                <option value="months">Months</option>
                <option value="years">Years</option>
              </select>
            </div>

            <!-- Row 1, Col 2: Fetch Button & Notifications -->
            <div class="flex items-center gap-4 relative">
              <button 
                @click="usageStore.fetchUsageStats(vaultUris, filterStore.filteredNames, vaultStore.knownSecretNames.length)" 
                class="px-5 py-2 font-medium text-sm rounded-lg transition-colors shadow-sm flex items-center justify-center gap-2 whitespace-nowrap"
                :class="usageStore.isFetchingUsage ? 'bg-slate-100 text-slate-500 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700 text-white'"
                :disabled="usageStore.isFetchingUsage || vaultUris.length === 0"
              >
                <svg v-if="usageStore.isFetchingUsage" class="animate-spin -ml-1 h-4 w-4 text-slate-500" fill="none" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                </svg>
                {{ usageStore.isFetchingUsage ? 'Querying Azure Monitor...' : 'Fetch Usage Stats' }}
              </button>

              <div v-if="!usageStore.isAuditingEnabled" class="flex items-center ml-auto pl-4">
                <div class="text-amber-600 bg-amber-50 px-2.5 py-1 rounded-lg text-xs font-medium border border-amber-200">
                  Audit logs missing
                </div>
                <button @click="usageStore.downloadAuditScript()" class="px-2.5 py-1 text-xs font-medium bg-white border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 flex items-center gap-1.5 shadow-sm transition-colors ml-2" title="Download PowerShell script to enable audit logs">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" />
                  </svg>
                  Setup Script
                </button>
              </div>

              <!-- Notifications -->
              <div v-if="typeof usageStore.insightCount === 'number' && !usageStore.isFetchingUsage" class="text-sm font-medium text-slate-500 flex items-center gap-1.5 animate-fade-in pl-2">
                <svg class="w-4 h-4 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7" />
                </svg>
                Insights: <span class="text-emerald-600">{{ usageStore.insightCount }}</span>
              </div>
            </div>

            <!-- Row 2, Col 1: Show Filter -->
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-slate-600 whitespace-nowrap">Show:</span>
              <select v-model="usageStore.filterMode" class="text-sm bg-slate-100 hover:bg-slate-200 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer transition-colors h-8">
                <option value="None">All Secrets</option>
                <option value="UnusedAll">Unused in all vaults</option>
                <option value="Unused">Unused in any vault</option>
                <option value="UsedInLast">Used in the last...</option>
                <option value="NotUsedInLast">Not used in the last...</option>
                <option value="UsedBetween">Used between...</option>
              </select>

              <!-- Dynamic Filter Controls -->
              <div v-if="usageStore.filterMode === 'UsedInLast' || usageStore.filterMode === 'NotUsedInLast'" class="flex items-center gap-2 animate-fade-in">
                <input type="number" v-model="usageStore.filterValue" min="1" class="w-16 text-sm bg-slate-100 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 h-8" />
                <select v-model="usageStore.filterUnit" class="text-sm bg-slate-100 hover:bg-slate-200 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 cursor-pointer transition-colors h-8">
                  <option value="days">Days</option>
                  <option value="months">Months</option>
                  <option value="years">Years</option>
                </select>
              </div>
              <div v-if="usageStore.filterMode === 'UsedBetween'" class="flex items-center gap-2 animate-fade-in">
                <input type="date" v-model="usageStore.filterStartDate" class="text-sm bg-slate-100 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 h-8" />
                <span class="text-sm text-slate-500 font-medium">and</span>
                <input type="date" v-model="usageStore.filterEndDate" class="text-sm bg-slate-100 border-none rounded-md px-3 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 h-8" />
              </div>
            </div>

            <!-- Row 2, Col 2: See Query -->
            <div class="flex items-center">
              <button @click="showQueryModal = true" class="text-xs font-medium text-slate-400 hover:text-blue-500 transition-colors flex items-center gap-1 whitespace-nowrap">
                <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
                </svg>
                See Query
              </button>
            </div>
          </div>
        </div>
      </template>

      <template #inspections-tool>
        <div class="flex items-start gap-8 h-full pb-1 px-1 -mx-1 overflow-x-auto">
          <InspectionsToolSection 
            :hasFetchedValues="hasFetchedValues"
            :filteredResultsLength="filteredResultsForGrid.length"
            :hasInspectionsRun="inspectionStore.hasInspectionsRun"
            :inspectionCounts="inspectionCounts"
            :isShowingReport="inspectionStore.showInspectionReport"
            @run-inspections="runInspections"
            @clear-inspections="inspectionStore.clearInspections()"
            @toggle-report="inspectionStore.showInspectionReport = !inspectionStore.showInspectionReport"
          />

          <!-- Vertical Divider -->
          <div class="w-px bg-slate-200 self-stretch my-2 shrink-0"></div>

          <!-- Checkboxes and Actions from Inspection Report Tab -->
          <div class="flex flex-col gap-3 h-full min-w-max">
            <div class="font-bold text-slate-800 text-sm">Report Filters & Actions</div>
            <div class="flex items-center gap-6 mt-auto pb-0.5">
              <div class="flex items-center gap-4">
                <span class="text-sm font-medium text-slate-700" :class="!inspectionStore.showInspectionReport ? 'opacity-50' : ''">Severity:</span>
                <label class="flex items-center gap-1.5 text-sm font-medium text-slate-700 transition-colors" :class="inspectionStore.showInspectionReport ? 'cursor-pointer hover:text-rose-600' : 'opacity-50 cursor-not-allowed'">
                  <input type="checkbox" v-model="inspectionStore.inspectionSeverities.Critical" :disabled="!inspectionStore.showInspectionReport" class="rounded text-rose-600 focus:ring-rose-500" :class="inspectionStore.showInspectionReport ? 'cursor-pointer' : 'cursor-not-allowed disabled:bg-slate-100 disabled:border-slate-300'" />
                  Critical
                </label>
                <label class="flex items-center gap-1.5 text-sm font-medium text-slate-700 transition-colors" :class="inspectionStore.showInspectionReport ? 'cursor-pointer hover:text-orange-600' : 'opacity-50 cursor-not-allowed'">
                  <input type="checkbox" v-model="inspectionStore.inspectionSeverities.High" :disabled="!inspectionStore.showInspectionReport" class="rounded text-orange-600 focus:ring-orange-500" :class="inspectionStore.showInspectionReport ? 'cursor-pointer' : 'cursor-not-allowed disabled:bg-slate-100 disabled:border-slate-300'" />
                  High
                </label>
                <label class="flex items-center gap-1.5 text-sm font-medium text-slate-700 transition-colors" :class="inspectionStore.showInspectionReport ? 'cursor-pointer hover:text-amber-600' : 'opacity-50 cursor-not-allowed'">
                  <input type="checkbox" v-model="inspectionStore.inspectionSeverities.Medium" :disabled="!inspectionStore.showInspectionReport" class="rounded text-amber-600 focus:ring-amber-500" :class="inspectionStore.showInspectionReport ? 'cursor-pointer' : 'cursor-not-allowed disabled:bg-slate-100 disabled:border-slate-300'" />
                  Medium
                </label>
                <label class="flex items-center gap-1.5 text-sm font-medium text-slate-700 transition-colors" :class="inspectionStore.showInspectionReport ? 'cursor-pointer hover:text-slate-900' : 'opacity-50 cursor-not-allowed'">
                  <input type="checkbox" v-model="inspectionStore.inspectionSeverities.Low" :disabled="!inspectionStore.showInspectionReport" class="rounded text-slate-600 focus:ring-slate-500" :class="inspectionStore.showInspectionReport ? 'cursor-pointer' : 'cursor-not-allowed disabled:bg-slate-100 disabled:border-slate-300'" />
                  Low
                </label>
              </div>
              <div class="flex items-center gap-2">
                <button @click="copyInspectionsMarkdown" :disabled="!inspectionStore.showInspectionReport" class="px-3 py-1.5 text-sm font-medium bg-white border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 flex items-center gap-2 shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3" />
                  </svg>
                  Copy Markdown
                </button>
                <button @click="downloadInspectionsCSV" :disabled="!inspectionStore.showInspectionReport" class="px-3 py-1.5 text-sm font-medium bg-white border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 flex items-center gap-2 shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                  </svg>
                  Download CSV
                </button>
              </div>
            </div>
          </div>
        </div>
      </template>

      <template #staged>
        <div class="flex items-center justify-center gap-12">
          <div class="flex items-center">
            <span class="text-sm font-medium text-slate-700">Pending Actions: <span class="text-blue-600">{{ stagedChanges.length }}</span></span>
          </div>
          <div class="flex items-center gap-2">
            <button @click="downloadStagedScript" class="px-3 py-1.5 text-sm font-medium bg-white border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 flex items-center gap-2 shadow-sm transition-colors">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
              Download script (PS1)
            </button>
            <button 
              @click="stagedStore.applyStagedChanges(vaultStore.fetchComparison)" 
              class="px-3 py-1.5 text-sm font-medium bg-blue-600 text-white rounded-lg hover:bg-blue-700 flex items-center gap-2 shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              :disabled="stagedChanges.length === 0"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
              </svg>
              Apply {{ Math.min(stagedChanges.length, 5) }} changes to Azure
            </button>
          </div>
        </div>
      </template>



      <template #code>
        <div class="flex flex-col w-full h-full min-h-[84px] px-2 py-1 relative">
          <div class="flex items-center gap-6 pt-1">
            <div class="flex items-center gap-2">
              <label class="flex items-center gap-1.5 text-sm font-medium text-slate-600 cursor-pointer select-none hover:text-slate-800 transition-colors">
                <input type="checkbox" v-model="useGithubOrgInput" @change="saveGithubSettings" class="rounded text-blue-600 focus:ring-blue-500 cursor-pointer border-slate-300" />
                Organization:
              </label>
              <input type="text" v-model="githubOrgInput" @blur="saveGithubSettings" @keyup.enter="saveGithubSettings" :disabled="!useGithubOrgInput" :class="{'opacity-50 cursor-not-allowed': !useGithubOrgInput}" placeholder="e.g. microsoft" class="w-48 text-sm bg-slate-100 border border-slate-200 rounded-md px-3 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" />
            </div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-slate-600">Base URL:</span>
              <input type="text" v-model="githubBaseUrlInput" @blur="saveGithubSettings" @keyup.enter="saveGithubSettings" placeholder="https://github.com" class="w-64 text-sm bg-slate-100 border border-slate-200 rounded-md px-3 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" />
            </div>
          </div>
          <div class="text-sm text-slate-500 mt-3 flex items-center gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Click the GitHub icon in any cell below to open a new tab and search for the key across your entire organization.
          </div>
        </div>
      </template>

      <template #devops>
        <div class="flex flex-col w-full h-full min-h-[84px] px-2 py-1 relative">
          <div class="flex items-center gap-4 pt-1 flex-wrap">
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-slate-600">Org:</span>
              <InfoTooltip align="left">
                <span>Enter your Azure DevOps organization name.</span>
              </InfoTooltip>
              <input v-model="devopsDataStore.organization" @keyup.enter="devopsDataStore.fetchVariableGroups" type="text" placeholder="e.g. MyFintechBank" class="w-48 text-sm bg-slate-100 border border-slate-200 rounded-md px-3 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" />
            </div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-slate-600">Project:</span>
              <InfoTooltip align="left">
                <span>Enter your Azure DevOps project name.</span>
              </InfoTooltip>
              <input v-model="devopsDataStore.project" @keyup.enter="devopsDataStore.fetchVariableGroups" type="text" placeholder="e.g. MyProject" class="w-64 text-sm bg-slate-100 border border-slate-200 rounded-md px-3 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" />
            </div>
            <button 
              @click="devopsDataStore.fetchVariableGroups" 
              class="px-4 py-1.5 font-medium text-sm rounded-lg transition-colors shadow-sm flex items-center gap-2"
              :class="devopsDataStore.isLoading ? 'bg-slate-100 text-slate-500 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700 text-white'"
              :disabled="devopsDataStore.isLoading || !devopsDataStore.organization || !devopsDataStore.project"
            >
              <svg v-if="devopsDataStore.isLoading" class="animate-spin -ml-1 mr-1 h-4 w-4 text-slate-500" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              {{ devopsDataStore.isLoading ? 'Fetching...' : 'Fetch Libraries' }}
            </button>
            <button 
              v-if="hasLinkedGroups && !devopsDataStore.isLoading"
              @click="toggleLinkedGroups" 
              class="px-4 py-1.5 font-medium text-sm rounded-lg transition-colors shadow-sm border"
              :class="areLinkedGroupsShown ? 'bg-slate-100 hover:bg-slate-200 text-slate-700 border-slate-300' : 'bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border-indigo-200'"
            >
              {{ areLinkedGroupsShown ? 'Hide all lib var groups' : 'Show all linked lib var groups' }}
            </button>
            <div v-if="devopsDataStore.error" class="text-rose-500 text-xs font-semibold ml-2">
              {{ devopsDataStore.error }}
            </div>
            
            <label class="flex items-center gap-1.5 ml-auto text-sm font-medium text-slate-700 cursor-pointer select-none transition-colors hover:text-slate-900">
              <input type="checkbox" v-model="settingsStore.uiSettings.groupDevOpsColumns" @change="settingsStore.saveUiSettings()" class="rounded border-slate-300 text-blue-600 focus:ring-blue-500" />
              Group Library &amp; Vault Columns
            </label>
          </div>
          
          <div class="mt-3 flex gap-4 items-center" v-if="devopsDataStore.variableGroups.length > 0">
            <div class="flex flex-col gap-1 w-72 shrink-0 relative">
              <div class="flex items-center gap-1">
                <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">Search Libraries</span>
                <InfoTooltip>
                  <span class="leading-relaxed">
                    Search and select a library to add it as a column, or click the
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 inline-block align-baseline mx-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <rect x="4" y="5" width="2" height="14" rx="0.5" />
                      <rect x="11" y="5" width="2" height="14" rx="0.5" />
                      <rect x="18" y="5" width="2" height="14" rx="0.5" transform="rotate(-15 19 12)" />
                    </svg>
                    icon on the vault headers which have an associated library, to show it.
                  </span>
                </InfoTooltip>
                <span class="text-xs font-semibold text-slate-500 uppercase tracking-wider">:</span>
              </div>
              <div class="relative w-full">
                <input 
                  type="text"
                  v-model="devopsSearchQuery"
                  @focus="devopsShowDropdown = true"
                  @blur="hideDevopsDropdown"
                  @keydown.esc="devopsShowDropdown = false"
                  placeholder="Search group or vault name..."
                  class="w-full border border-slate-300 rounded-lg px-2 py-1 pr-8 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
                <div class="absolute right-2 inset-y-0 flex items-center pointer-events-none text-slate-400">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                  </svg>
                </div>
                <ul 
                  v-if="devopsShowDropdown && filteredVariableGroups.length > 0" 
                  class="absolute z-50 w-full mt-1 bg-white border border-slate-200 shadow-lg max-h-60 rounded-md overflow-auto py-1 text-left"
                >
                  <li 
                    v-for="group in filteredVariableGroups" 
                    :key="group.id" 
                    @mousedown.prevent="selectVariableGroup(group.id)"
                    class="px-3 py-2 hover:bg-blue-50 cursor-pointer text-sm flex flex-col"
                  >
                    <span class="font-medium text-slate-800">{{ group.name }}</span>
                    <span v-if="group.providerData?.vault" class="text-xs text-slate-500 font-mono mt-0.5">Vault: {{ group.providerData.vault }}</span>
                  </li>
                </ul>
                <div 
                  v-else-if="devopsShowDropdown && filteredVariableGroups.length === 0"
                  class="absolute z-50 w-full mt-1 bg-white border border-slate-200 shadow-lg rounded-md p-3 text-sm text-slate-500 text-center"
                >
                  No matches found
                </div>
              </div>
            </div>

            <div class="flex-1 flex flex-col relative" @click="hideDevopsContextMenu">
              <div class="flex flex-wrap gap-2">
                <div 
                  v-for="group in devopsDataStore.selectedGroups" 
                  :key="group.id"
                  class="flex items-center gap-1.5 px-3 py-1 rounded-md text-xs font-medium bg-blue-50 border border-blue-200 text-blue-800 shadow-sm cursor-context-menu"
                  @contextmenu.prevent.stop="showDevopsContextMenu($event, group)"
                >
                  <div class="flex flex-col max-w-[200px]">
                    <span class="truncate" :title="group.name">{{ group.name }}</span>
                    <span v-if="group.providerData?.vault" class="text-[10px] text-blue-500 font-mono truncate" :title="group.providerData.vault">Vault: {{ group.providerData.vault }}</span>
                  </div>
                  <button 
                    @click.stop="devopsDataStore.toggleGroupSelection(group.id)" 
                    class="ml-1 text-blue-400 hover:text-rose-500 focus:outline-none transition-colors p-0.5"
                    title="Remove column"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                      <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
                    </svg>
                  </button>
                </div>
              </div>

              <!-- DevOps Context Menu -->
              <div v-if="devopsContextMenu.show" 
                   :style="{ top: `${devopsContextMenu.y}px`, left: `${devopsContextMenu.x}px` }"
                   class="fixed z-50 bg-white border border-slate-200 shadow-[0_4px_12px_rgba(0,0,0,0.1),_0_0_1px_rgba(0,0,0,0.2)] rounded py-1 min-w-[160px] text-[13px] text-slate-800"
                   @click.stop>
                <button @click="devopsDataStore.openAdoLibrary(devopsContextMenu.group!.id); hideDevopsContextMenu()" class="w-full text-left px-3 py-1.5 hover:bg-slate-100 flex items-center justify-between group">
                  <span>Go to ADO lib var group</span>
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
                    <polyline points="15 3 21 3 21 9"></polyline>
                    <line x1="10" y1="14" x2="21" y2="3"></line>
                  </svg>
                </button>
                <button v-if="devopsContextMenu.group?.providerData?.vault" @click="vaultStore.openAzureVaultGlobal(devopsContextMenu.group!.providerData!.vault!); hideDevopsContextMenu()" class="w-full text-left px-3 py-1.5 hover:bg-slate-100 flex items-center justify-between group">
                  <span>Go to Azure Vault</span>
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
                    <polyline points="15 3 21 3 21 9"></polyline>
                    <line x1="10" y1="14" x2="21" y2="3"></line>
                  </svg>
                </button>
              </div>

            </div>
          </div>
          <div v-else-if="!devopsDataStore.isLoading" class="text-xs text-slate-400 mt-2">
            No libraries fetched yet.
          </div>
        </div>
      </template>
    </AppHeader>

    <main class="flex-1 flex flex-col min-h-0 overflow-hidden px-2 pb-2 pt-0">

      <!-- Status Bar (Filters & Inspections) -->
      <StatusBar />

      <div v-show="['select', 'analyze', 'usage', 'code', 'devops'].includes(currentTab) || (currentTab === 'inspections-tool' && !inspectionStore.showInspectionReport)" class="w-full h-full flex flex-col gap-2 min-h-0">
        <GridTable 
          :filteredResults="filteredResultsForGrid"
          :allSortedNamesLength="allSortedNames.length"
          @clear-filters="clearFilters"
          @grant-access="showGrantAccessModal = true"
        />
      </div>      

      <!-- Staged Changes Tab -->
      <div v-if="currentTab === 'staged'" class="w-full mx-auto flex-1 flex flex-col min-h-0">
        <div v-if="stagedChanges.length === 0" class="flex-1 flex flex-col items-center justify-center text-slate-500">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mb-4 text-slate-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <h2 class="text-xl font-bold text-slate-700">No Staged Changes</h2>
          <p class="mt-2 text-sm max-w-md text-center">Modifications made in the Fetch Values Grid, with Ctrl+C / Ctrl+V, to copy/paste values from one cell to another, will appear here for review before committing and applying them to Azure Key Vault.</p>
        </div>
        <div v-else class="flex-1 flex flex-col bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
          <div class="flex-1 overflow-auto">
            <table class="w-full text-left border-collapse table-fixed">
              <thead>
                <tr class="bg-slate-50 border-b border-slate-200 text-slate-500 text-xs uppercase tracking-wider sticky top-0 shadow-sm z-10">
                  <th class="w-10 px-1 py-1.5 text-center font-semibold bg-slate-50 border-r border-slate-100"></th>
                  <th class="w-[15%] px-3 py-1.5 text-xs font-semibold bg-slate-50">Vault</th>
                  <th class="w-[25%] px-3 py-1.5 text-xs font-semibold bg-slate-50">Secret Name</th>
                  <th class="w-24 px-3 py-1.5 text-xs font-semibold bg-slate-50">Action</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">Original Value</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">New Value</th>
                  <th class="w-16 px-3 py-1.5 text-xs font-semibold bg-slate-50 text-right"></th>
                </tr>
              </thead>
              <tbody class="text-sm divide-y divide-slate-100">
                <tr v-for="(change, idx) in stagedChanges" :key="idx" class="hover:bg-slate-50 transition-colors">
                  <td class="w-10 min-w-[30px] max-w-[30px] px-1 py-1.5 text-center border-r border-slate-100 bg-white group-hover:bg-slate-50 shadow-[1px_0_0_0_#f1f5f9]">
                    <button 
                      @click="toggleStagedSecretVisibility(change.vaultUri, change.secretName)"
                      class="text-slate-400 hover:text-slate-700 focus:outline-none transition-colors"
                      title="Show/hide secret"
                    >
                      <svg v-if="visibleStagedSecrets.has(`${change.vaultUri}-${change.secretName}`)" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 mx-auto" viewBox="0 0 20 20" fill="currentColor">
                        <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                        <path fill-rule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clip-rule="evenodd" />
                      </svg>
                      <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 mx-auto opacity-50" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd" d="M3.707 2.293a1 1 0 00-1.414 1.414l14 14a1 1 0 001.414-1.414l-1.473-1.473A10.014 10.014 0 0019.542 10C18.268 5.943 14.478 3 10 3a9.958 9.958 0 00-4.512 1.074l-1.78-1.781zm4.261 4.26l1.514 1.515a2.003 2.003 0 012.45 2.45l1.514 1.514a4 4 0 00-5.478-5.478z" clip-rule="evenodd" />
                        <path d="M12.454 16.697L9.75 13.992a4 4 0 01-3.742-3.741L2.335 6.578A9.98 9.98 0 00.458 10c1.274 4.057 5.065 7 9.542 7 .847 0 1.669-.105 2.454-.303z" />
                      </svg>
                    </button>
                  </td>
                  <td class="px-3 py-1 text-xs font-medium text-slate-700">{{ getVaultName(change.vaultUri) }}</td>
                  <td class="px-3 py-1 text-xs font-medium text-slate-900 transition-all" :class="{'blur-[3px] opacity-60 select-none': uiSettings.demoMode}">{{ change.secretName }}</td>
                  <td class="px-3 py-1 text-xs">
                    <span 
                      class="px-2 py-1 rounded-md text-xs font-bold"
                      :class="change.type === 'CREATE' ? 'bg-emerald-100 text-emerald-700' : 'bg-blue-100 text-blue-700'"
                    >
                      {{ change.type }}
                    </span>
                  </td>
                  <td class="px-3 py-1 text-xs text-slate-500 font-mono text-xs truncate transition-all" :class="{'blur-[3px] opacity-60 select-none': uiSettings.demoMode}" :title="visibleStagedSecrets.has(`${change.vaultUri}-${change.secretName}`) ? (change.originalValue || '') : '********'">
                    {{ visibleStagedSecrets.has(`${change.vaultUri}-${change.secretName}`) ? (change.originalValue || '(Missing)') : '********' }}
                  </td>
                  <td class="px-3 py-1 text-xs font-mono text-xs truncate text-amber-600 transition-all" :class="{'blur-[3px] opacity-60 select-none': uiSettings.demoMode}" :title="visibleStagedSecrets.has(`${change.vaultUri}-${change.secretName}`) ? change.newValue : '********'">
                    {{ visibleStagedSecrets.has(`${change.vaultUri}-${change.secretName}`) ? change.newValue : '********' }}
                  </td>
                  <td class="px-3 py-1 text-xs text-right">
                    <button 
                      @click="stagedStore.revertChange(change.vaultUri, change.secretName)"
                      class="text-slate-400 hover:text-rose-600 p-1.5 rounded-lg hover:bg-rose-50 transition-colors"
                      title="Revert"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                      </svg>
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Inspections Report Tab -->
      <div v-show="currentTab === 'inspections-tool' && inspectionStore.showInspectionReport" class="w-full h-full flex flex-col min-h-0 bg-white rounded-xl shadow-sm border border-slate-200">
        <div class="flex-1 overflow-auto p-3 bg-slate-50/50">
          <div v-if="!inspectionStore.hasInspectionsRun" class="text-center py-20 text-slate-500">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 mx-auto text-slate-300 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <p class="text-base font-medium">No inspections have run yet.</p>
            <p class="text-sm mt-1">Go to the Inspections tab, run them, and view the report.</p>
          </div>
          <div v-else-if="filteredInspectionReportData.length === 0" class="text-center py-20 text-emerald-600">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 mx-auto text-emerald-400 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <p class="text-base font-medium">Zero vulnerabilities found!</p>
            <p class="text-sm mt-1 text-emerald-500">All visible secrets passed the active inspection filters.</p>
          </div>
          <div v-else class="space-y-4">
            <div 
              v-for="(f, i) in filteredInspectionReportData" 
              :key="i"
              class="bg-white border rounded-lg p-2 shadow-sm flex items-start gap-2"
              :class="{
                'border-rose-200 bg-rose-50/30': f.severity === 'Critical',
                'border-orange-200 bg-orange-50/30': f.severity === 'High',
                'border-amber-200 bg-amber-50/30': f.severity === 'Medium',
                'border-slate-200 bg-slate-50/50': f.severity === 'Low'
              }"
            >
              <div class="mt-1">
                <span 
                  class="px-2.5 py-1 rounded text-xs font-bold"
                  :class="{
                    'bg-rose-100 text-rose-700': f.severity === 'Critical',
                    'bg-orange-100 text-orange-700': f.severity === 'High',
                    'bg-amber-100 text-amber-700': f.severity === 'Medium',
                    'bg-slate-200 text-slate-700': f.severity === 'Low'
                  }"
                >{{ f.severity }}</span>
              </div>
              <div class="flex-1 min-w-0">
                <div class="flex items-baseline gap-2 mb-1">
                  <span class="font-bold text-slate-900 truncate">{{ f.secret }}</span>
                  <span class="text-xs text-slate-500 font-mono truncate">@ {{ f.vault }}</span>
                </div>
                <div class="font-semibold text-slate-700 text-sm">{{ f.rule }}</div>
                <div class="text-sm text-slate-600 mt-1 whitespace-pre-wrap">{{ f.message }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Logs Tab -->
      <div v-if="currentTab === 'logs'" class="w-full h-full flex flex-col items-center justify-center text-slate-500">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mb-4 text-slate-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <h2 class="text-xl font-bold text-slate-700">Audit Logs</h2>
        <p class="mt-2 text-sm max-w-md text-center">Past synchronization events and errors will be listed here.</p>
      </div>
    </main>

    <AuthErrorModal :show="showAuthError" @retry="authStore.retryAuth()" />
    <RegexHelpModal :show="showRegexHelpDialog" @close="showRegexHelpDialog = false" />
    <QueryModal :show="showQueryModal" @close="showQueryModal = false" />
    <GrantAccessModal :show="showGrantAccessModal" @close="showGrantAccessModal = false" @download="downloadGrantScript" />
    <HelpModal :show="showHelpDialog" @close="showHelpDialog = false" />
  </div>
</template>

<style>
/* Custom thin scrollbar for secret values */
.secret-scroll::-webkit-scrollbar {
  height: 6px;
}
.secret-scroll::-webkit-scrollbar-track {
  background: transparent;
}
.secret-scroll::-webkit-scrollbar-thumb {
  background-color: #cbd5e1;
  border-radius: 10px;
}
.secret-scroll:hover::-webkit-scrollbar-thumb {
  background-color: #94a3b8;
}

/* Fetch Button Visibility */
.fetch-btn {
  opacity: 0;
}
.name-cell-container:hover .fetch-btn,
.fetch-btn:focus {
  opacity: 1;
}
</style>
