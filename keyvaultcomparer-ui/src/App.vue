<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { storeToRefs } from 'pinia';

import AppHeader from './components/Layout/AppHeader.vue';
import VaultSelector from './components/VaultManagement/VaultSelector.vue';
import FilterSection from './components/Filters/FilterSection.vue';
import GridTable from './components/ComparisonGrid/GridTable.vue';
import AuthErrorModal from './components/Modals/AuthErrorModal.vue';
import RegexHelpModal from './components/Modals/RegexHelpModal.vue';
import HelpModal from './components/Modals/HelpModal.vue';
import GrantAccessModal from './components/Modals/GrantAccessModal.vue';
import { useAuthStore } from './stores/authStore';
import { useDataStore } from './stores/dataStore';
import { useSettingsStore } from './stores/settingsStore';
import { useFilterStore } from './stores/filterStore';
import { useStagedStore } from './stores/stagedStore';
import { useUiStateStore } from './stores/uiStateStore';
import { useSecurityAnalysis } from './composables/useSecurityAnalysis';

import { analyzeSecret, analyzeMetadata, type InspectionResult } from './inspections';

const authStore = useAuthStore();
const { showAuthError } = storeToRefs(authStore);

const dataStore = useDataStore();
const { vaultUris, vaultData } = storeToRefs(dataStore);

const settingsStore = useSettingsStore();
const { uiSettings } = storeToRefs(settingsStore);

const filterStore = useFilterStore();
const { allSortedNames } = storeToRefs(filterStore);

const stagedStore = useStagedStore();
const { stagedChanges } = storeToRefs(stagedStore);

const { results, rowUsageCount, colUsageCount, vulnerableValuesMap } = useSecurityAnalysis();

const uiStateStore = useUiStateStore();
const { currentTab } = storeToRefs(uiStateStore);
const showHelpDialog = ref(false);
const showRegexHelpDialog = ref(false);
const showGrantAccessModal = ref(false);

const hasFetchedValues = computed(() => {
  return Object.values(vaultData.value).some(vault => 
    Object.values(vault).some(v => v.status !== 'Missing' && v.status !== 'Not Retrieved' && v.status !== 'Loading')
  );
});

// Inspections
const hasInspectionsRun = ref(false);

const runInspectionsOnVisible = () => {
  if (vaultUris.value.length === 0 || results.value.length === 0) return;

  results.value.forEach(row => {
    vaultUris.value.forEach(uri => {
      const currentVal = row.vaultValues[uri];
      if (currentVal && currentVal.status === 'Present' && currentVal.value) {
        
        // Value analysis
        const valueAnalysis = analyzeSecret(
          row.secretName,
          currentVal.value
        );

        // Metadata analysis
        const metaList = dataStore.knownSecretNames[uri]?.secrets;
        const secretMeta = metaList?.find(m => m.name === row.secretName);
        const metadataInspections = secretMeta ? analyzeMetadata(secretMeta) : [];

        // Merge results
        const allInspections: InspectionResult[] = [...valueAnalysis.inspections, ...metadataInspections];

        const valLower = currentVal.value.toLowerCase();
        if (vulnerableValuesMap.value.has(valLower)) {
          const usages = vulnerableValuesMap.value.get(valLower);
          allInspections.push({
            ruleName: 'Reused Secret',
            severity: 'High',
            message: `Reused in ${usages?.length} secrets: ${usages?.join(', ')}. Click identical values to highlight occurrences.`
          });
        }

        // Recalculate highest severity
        let highestSeverity: 'Low' | 'Medium' | 'High' | 'Critical' | undefined = undefined;
        let maxScore = 0;
        const severityScore = { 'Low': 1, 'Medium': 2, 'High': 3, 'Critical': 4 };
        for (const ins of allInspections) {
          const score = severityScore[ins.severity];
          if (score > maxScore) {
            maxScore = score;
            highestSeverity = ins.severity;
          }
        }

        const d = vaultData.value[uri]?.[row.secretName];
        if (d) {
          d.inspections = allInspections;
          d.highestSeverity = highestSeverity;
        }
      }
    });
  });
  hasInspectionsRun.value = true;
};

const clearInspections = () => {
  results.value.forEach(row => {
    vaultUris.value.forEach(uri => {
      const d = vaultData.value[uri]?.[row.secretName];
      if (d) {
        d.inspections = undefined;
        d.highestSeverity = undefined;
      }
    });
  });
  hasInspectionsRun.value = false;
  filterStore.setInspectionFilter('None');
};

const inspectionCounts = computed(() => {
  const counts = { Any: 0, Critical: 0, High: 0, Medium: 0, Low: 0 };
  if (!hasInspectionsRun.value) return counts;

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
  if (!hasInspectionsRun.value) return report;
  
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

const filteredResultsForGrid = computed(() => {
  let base = results.value;
  
  if (filterStore.inspectionFilter !== 'None' && hasInspectionsRun.value) {
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
};

const getVaultName = (uri: string) => {
  try {
    return new URL(uri).hostname.split('.')[0];
  } catch {
    return uri;
  }
};

const downloadGrantScript = () => {
  // Logic to download ps1 script
  showGrantAccessModal.value = false;
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
        <FilterSection 
          :hasFetchedValues="hasFetchedValues"
          :filteredResultsLength="filteredResultsForGrid.length"
          :allSortedNamesLength="allSortedNames.length"
          :hasInspectionsRun="hasInspectionsRun"
          :inspectionCounts="inspectionCounts"
          @fetch-comparison="dataStore.fetchComparison()"
          @run-inspections="runInspectionsOnVisible"
          @clear-inspections="clearInspections"
          @show-report="currentTab = 'inspections'"
          @clear-filters="clearFilters"
          @show-regex-help="showRegexHelpDialog = true"
        />
      </template>
    </AppHeader>

    <!-- Main Content Layout -->
    <main class="flex-1 flex flex-col min-h-0 overflow-hidden p-2 gap-2">

      <div v-show="currentTab === 'select' || currentTab === 'analyze'" class="w-full h-full flex flex-col gap-2 min-h-0">
        <GridTable 
          :filteredResults="filteredResultsForGrid"
          :allSortedNamesLength="allSortedNames.length"
          @clear-filters="clearFilters"
        />
      </div>      

      <!-- Staged Changes Tab -->
      <div v-if="currentTab === 'staged'" class="w-full mx-auto flex-1 flex flex-col min-h-0">
        <div v-if="stagedChanges.length === 0" class="flex-1 flex flex-col items-center justify-center text-slate-500">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mb-4 text-slate-300" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <h2 class="text-xl font-bold text-slate-700">No Staged Changes</h2>
          <p class="mt-2 text-sm max-w-md text-center">Modifications made in the Analyze Data Grid, with Ctrl+C / Ctrl+V, to copy/paste values from one cell to another, will appear here for review before committing and applying them to Azure Key Vault.</p>
        </div>
        <div v-else class="flex-1 flex flex-col bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
          <div class="flex-1 overflow-auto">
            <table class="w-full text-left border-collapse">
              <thead>
                <tr class="bg-slate-50 border-b border-slate-200 text-slate-500 text-xs uppercase tracking-wider sticky top-0 shadow-sm z-10">
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">Vault</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">Secret Name</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">Action</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">Original Value</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50">New Value</th>
                  <th class="px-3 py-1.5 text-xs font-semibold bg-slate-50 text-right"></th>
                </tr>
              </thead>
              <tbody class="text-sm divide-y divide-slate-100">
                <tr v-for="(change, idx) in stagedChanges" :key="idx" class="hover:bg-slate-50 transition-colors">
                  <td class="px-3 py-1 text-xs font-medium text-slate-700">{{ getVaultName(change.vaultUri) }}</td>
                  <td class="px-3 py-1 text-xs font-medium text-slate-900">{{ change.secretName }}</td>
                  <td class="px-3 py-1 text-xs">
                    <span 
                      class="px-2 py-1 rounded-md text-xs font-bold"
                      :class="change.type === 'CREATE' ? 'bg-emerald-100 text-emerald-700' : 'bg-blue-100 text-blue-700'"
                    >
                      {{ change.type }}
                    </span>
                  </td>
                  <td class="px-3 py-1 text-xs text-slate-500 font-mono text-xs max-w-xs truncate" :title="change.originalValue || ''">
                    {{ change.originalValue || '(Missing)' }}
                  </td>
                  <td class="px-3 py-1 text-xs font-mono text-xs max-w-xs truncate text-amber-600" :title="change.newValue">
                    {{ change.newValue }}
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
      <div v-show="currentTab === 'inspections'" class="w-full h-full flex flex-col min-h-0 bg-white rounded-xl shadow-sm border border-slate-200">
        <div class="flex-1 overflow-auto p-3 bg-slate-50/50">
          <div v-if="!hasInspectionsRun" class="text-center py-20 text-slate-500">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 mx-auto text-slate-300 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <p class="text-base font-medium">No inspections have run yet.</p>
            <p class="text-sm mt-1">Go to the Analyze Data tab, be sure you have fetched values and run inspections to view the report.</p>
          </div>
          <div v-else-if="inspectionReportData.length === 0" class="text-center py-20 text-emerald-600">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-12 w-12 mx-auto text-emerald-400 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <p class="text-base font-medium">Zero vulnerabilities found!</p>
            <p class="text-sm mt-1 text-emerald-500">All visible secrets passed the inspections.</p>
          </div>
          <div v-else class="space-y-4">
            <div 
              v-for="(f, i) in inspectionReportData" 
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
