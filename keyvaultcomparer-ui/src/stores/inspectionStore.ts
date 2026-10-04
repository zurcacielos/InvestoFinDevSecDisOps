import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { useDataStore } from './dataStore';
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

  const dataStore = useDataStore();
  const filterStore = useFilterStore();

  const hasInspectionsRun = computed(() => {
    for (const uri in dataStore.vaultData) {
      for (const secret in dataStore.vaultData[uri]) {
        if (dataStore.vaultData[uri][secret].inspections?.length > 0) {
          return true;
        }
      }
    }
    return false;
  });

  const runInspectionsOnVisible = (results: SecretComparisonRow[], vulnerableValuesMap: Map<string, string[]>) => {
    if (dataStore.vaultUris.length === 0 || results.length === 0) return;

    results.forEach(row => {
      dataStore.vaultUris.forEach(uri => {
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

          const d = dataStore.vaultData[uri]?.[row.secretName];
          if (d) {
            d.inspections = allInspections;
            d.highestSeverity = highestSeverity;
          }
        }
      });
    });
  };

  const clearInspections = () => {
    Object.keys(dataStore.vaultData).forEach(uri => {
      Object.keys(dataStore.vaultData[uri]).forEach(secret => {
        const d = dataStore.vaultData[uri][secret];
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
