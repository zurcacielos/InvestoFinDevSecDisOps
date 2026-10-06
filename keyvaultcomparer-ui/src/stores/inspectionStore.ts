import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { useVaultStore } from './vaultStore';
import { useFilterStore } from './filterStore';
import { analyzeSecret, analyzeMetadata, type InspectionResult } from '../inspections';
import type { SecretComparisonRow } from '../composables/useSecurityAnalysis';

export const useInspectionStore = defineStore('inspection', () => {
  const showInspectionReport = ref(false);
  const inspectionSeverities = ref({
    Critical: true,
    High: true,
    Medium: true,
    Low: true
  });

  const vaultStore = useVaultStore();
  const filterStore = useFilterStore();

  const hasInspectionsRun = computed(() => {
    for (const uri in vaultStore.vaultData) {
      for (const secret in vaultStore.vaultData[uri]) {
        if (vaultStore.vaultData[uri][secret].inspections?.length > 0) {
          return true;
        }
      }
    }
    return false;
  });

  const runInspectionsOnVisible = (results: SecretComparisonRow[], vulnerableValuesMap: Map<string, string[]>) => {
    if (vaultStore.vaultUris.length === 0 || results.length === 0) return;

    results.forEach(row => {
      vaultStore.vaultUris.forEach(uri => {
        const currentVal = row.vaultValues[uri];
        if (currentVal && currentVal.status === 'Present' && currentVal.value) {
          
          // Value analysis
          const valueAnalysis = analyzeSecret(
            row.secretName,
            currentVal.value
          );

          // Metadata analysis
          const metaList = vaultStore.knownSecretNames[uri]?.secrets;
          const secretMeta = metaList?.find(m => m.name === row.secretName);
          const metadataInspections = secretMeta ? analyzeMetadata(secretMeta) : [];

          // Merge results
          const allInspections: InspectionResult[] = [...valueAnalysis.inspections, ...metadataInspections];

          const valLower = currentVal.value.toLowerCase();
          if (vulnerableValuesMap.has(valLower)) {
            const usages = vulnerableValuesMap.get(valLower);
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

          const d = vaultStore.vaultData[uri]?.[row.secretName];
          if (d) {
            d.inspections = allInspections;
            d.highestSeverity = highestSeverity;
          }
        }
      });
    });
  };

  const clearInspections = () => {
    Object.keys(vaultStore.vaultData).forEach(uri => {
      Object.keys(vaultStore.vaultData[uri]).forEach(secret => {
        const d = vaultStore.vaultData[uri][secret];
        if (d) {
          d.inspections = undefined;
          d.highestSeverity = undefined;
        }
      });
    });
    filterStore.setInspectionFilter('None');
  };

  return {
    showInspectionReport,
    inspectionSeverities,
    hasInspectionsRun,
    runInspectionsOnVisible,
    clearInspections
  };
});
