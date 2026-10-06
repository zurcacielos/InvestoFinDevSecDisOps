import { computed } from 'vue';
import { useVaultStore } from '../stores/vaultStore';
import { useSettingsStore, hashString, identiconEmojis } from '../stores/settingsStore';
import { useFilterStore } from '../stores/filterStore';
import { calculateEntropy, type InspectionResult } from '../inspections';

export interface SecretValueStatus {
  value: string | null;
  status: string;
  identiconEmoji?: string;
  colorIndex?: number;
  isStaged?: boolean;
  isLoading?: boolean;
  inspections?: InspectionResult[];
  highestSeverity?: 'Low' | 'Medium' | 'High' | 'Critical';
  errorMessage?: string;
}

export interface SecretComparisonRow {
  secretName: string;
  vaultValues: Record<string, SecretValueStatus>;
  libraryValues?: Record<number, any>;
  globalStatus: string;
}

import { useStagedStore } from '../stores/stagedStore';
import { useDevopsDataStore } from '../stores/devopsDataStore';

export function useSecurityAnalysis() {
  const vaultStore = useVaultStore();
  const settingsStore = useSettingsStore();
  const filterStore = useFilterStore();
  const stagedStore = useStagedStore();
  const devopsDataStore = useDevopsDataStore();

  const vulnerableValuesMap = computed(() => {
    const valueMap = new Map<string, Set<string>>(); // value -> Set of secretNames
    
    // Build value map
    for (const uri of Object.keys(vaultStore.vaultData)) {
      for (const [name, status] of Object.entries(vaultStore.vaultData[uri])) {
        if (status.status === 'Present' && status.value) {
          const valLower = status.value.toLowerCase();
          if (!valueMap.has(valLower)) valueMap.set(valLower, new Set());
          valueMap.get(valLower)!.add(name);
        }
      }
    }

    const vulnerable = new Map<string, string[]>();
    const settings = settingsStore.securitySettings;
    
    for (const [val, names] of valueMap.entries()) {
      if (names.size > 1) { // It's reused
        if (settings.ignoreValues.includes(val)) continue;

        const hasCriticalName = Array.from(names).some(name => {
          const lowerName = name.toLowerCase();
          return settings.includeKeyKeywords.some(kw => lowerName.includes(kw));
        });

        if (val.length < 6 || calculateEntropy(val) < 2.0) {
          continue;
        }

        if (val.length >= settings.minLength || hasCriticalName) {
          vulnerable.set(val, Array.from(names));
        }
      }
    }
    return vulnerable;
  });

  const globalUsageCount = computed(() => {
    const counts = new Map<string, number>();
    for (const uri of Object.keys(vaultStore.vaultData)) {
      for (const status of Object.values(vaultStore.vaultData[uri])) {
        if ((status.status === 'Present' || status.status === 'Loading') && status.value) {
          const val = status.value.toLowerCase();
          counts.set(val, (counts.get(val) || 0) + 1);
        }
      }
    }
    return counts;
  });

  const rowUsageCount = computed(() => {
    const counts = new Map<string, number>();
    for (const uri of Object.keys(vaultStore.vaultData)) {
      for (const [name, status] of Object.entries(vaultStore.vaultData[uri])) {
        if ((status.status === 'Present' || status.status === 'Loading') && status.value) {
          const key = name + status.value.toLowerCase();
          counts.set(key, (counts.get(key) || 0) + 1);
        }
      }
    }
    return counts;
  });

  const colUsageCount = computed(() => {
    const counts = new Map<string, number>();
    for (const uri of Object.keys(vaultStore.vaultData)) {
      for (const status of Object.values(vaultStore.vaultData[uri])) {
        if ((status.status === 'Present' || status.status === 'Loading') && status.value) {
          const key = uri + status.value.toLowerCase();
          counts.set(key, (counts.get(key) || 0) + 1);
        }
      }
    }
    return counts;
  });

  const results = computed<SecretComparisonRow[]>(() => {
    const filtered = filterStore.filteredNames;
    const selectedGroups = devopsDataStore.selectedGroups; // Force reactivity tracking
    
    // Pre-compute case-insensitive maps for each selected group
    const groupVariableMaps = selectedGroups.map(group => {
      const lowerCaseVars = new Map<string, any>();
      if (group.variables) {
        for (const [k, v] of Object.entries(group.variables)) {
          lowerCaseVars.set(k.toLowerCase(), v);
        }
      }
      return { id: group.id, vars: lowerCaseVars };
    });
    
    return filtered.slice(0, settingsStore.uiSettings.resultLimit > 0 ? settingsStore.uiSettings.resultLimit : undefined).map(name => {
      const row: SecretComparisonRow = {
        secretName: name,
        vaultValues: {},
        libraryValues: {},
        globalStatus: 'Missing'
      };
      
      const lowerName = name.toLowerCase();
      groupVariableMaps.forEach(groupMap => {
        const variable = groupMap.vars.get(lowerName);
        row.libraryValues![groupMap.id] = variable || null;
      });
      
      vaultStore.vaultUris.forEach(uri => {
        const knownNamesForVault = vaultStore.knownSecretNames[uri]?.secrets || [];
        const vaultMetaForName = knownNamesForVault.find(k => k.name === name);
        let baseStatus: SecretValueStatus;
        
        const d = vaultStore.vaultData[uri]?.[name];
        if (d && d.status !== 'Missing' && d.status !== 'Not Retrieved') {
          baseStatus = { ...d, colorIndex: 0 };
        } else if (!vaultMetaForName) {
          baseStatus = { status: 'Missing', value: null, colorIndex: 0 };
        } else {
          baseStatus = d ? { ...d, colorIndex: 0 } : { status: 'Not Retrieved', value: null, colorIndex: 0 };
        }

        const staged = stagedStore.stagedChanges.find(s => s.vaultUri === uri && s.secretName === name);
        if (staged) {
          baseStatus.value = staged.newValue;
          baseStatus.status = 'Present';
          baseStatus.isStaged = true;
        }
        
        row.vaultValues[uri] = baseStatus;
        
        const valStr = row.vaultValues[uri].value as string | null;
        const valLower = valStr?.toLowerCase();
        if (valLower && !settingsStore.securitySettings.ignoreValues.includes(valLower)) {
          let hashKey = '';
          let isDuplicated = false;

          if (settingsStore.uiSettings.identiconsByRow && settingsStore.uiSettings.identiconsByCol) {
            hashKey = valLower;
            isDuplicated = globalUsageCount.value.get(valLower)! > 1;
          } else if (settingsStore.uiSettings.identiconsByRow) {
            hashKey = name + valLower;
            isDuplicated = rowUsageCount.value.get(hashKey)! > 1;
          } else if (settingsStore.uiSettings.identiconsByCol) {
            hashKey = uri + valLower;
            isDuplicated = colUsageCount.value.get(hashKey)! > 1;
          }

          if (isDuplicated) {
            const hash = hashString(hashKey);
            row.vaultValues[uri].identiconEmoji = identiconEmojis[hash % identiconEmojis.length];
          }
        }
      });

      const distinctValues = Object.values(row.vaultValues)
        .filter(v => v.status !== 'Missing' && v.status !== 'Not Retrieved' && v.status !== 'Error' && v.value !== null)
        .map(v => v.value!.toLowerCase())
        .filter((v, i, a) => a.indexOf(v) === i);

      Object.values(row.vaultValues).forEach(status => {
        if (status.status !== 'Present' || status.value === null) {
          status.colorIndex = 0;
        } else {
          status.colorIndex = distinctValues.indexOf(status.value.toLowerCase()) + 1;
        }
      });

      const statuses = Object.values(row.vaultValues);
      if (statuses.some(s => s.status === 'Missing')) {
        row.globalStatus = 'Missing';
      } else if (statuses.some(s => s.status !== 'Present' && s.status !== 'Match' && s.status !== 'Mismatch')) {
        row.globalStatus = 'Incomplete';
      } else {
        const firstValue = statuses.find(s => s.status === 'Present')?.value?.toLowerCase();
        if (firstValue !== undefined && statuses.every(s => s.value?.toLowerCase() === firstValue)) {
          row.globalStatus = 'Match';
        } else {
          row.globalStatus = 'Mismatch';
        }
      }

      return row;
    });
  });

  return { vulnerableValuesMap, globalUsageCount, rowUsageCount, colUsageCount, results };
}
