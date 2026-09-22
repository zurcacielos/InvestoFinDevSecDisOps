import { defineStore } from 'pinia';
import { useDataStore } from './dataStore';
import { useSettingsStore, loadSharableConfig } from './settingsStore';

const loadRecentFilters = (): string[] => {
  const filters: string[] = JSON.parse(localStorage.getItem('recentFilters') || '[]');
  const defaults = [
    'db, token, api, key',
    '^prod-, -key$',
    '\\bpassword\\b',
    'token-\\d{3,}'
  ];
  
  if (filters.length < 10) {
    for (const d of defaults) {
      if (filters.length >= 10) break;
      if (!filters.includes(d)) {
        filters.push(d);
      }
    }
  }
  return filters;
};

export const useFilterStore = defineStore('filter', {
  state: () => {
    let initialFilter = '';
    const urlConfig = loadSharableConfig();
    if (urlConfig && urlConfig.f) {
      initialFilter = urlConfig.f;
    }

    return {
      nameFilter: initialFilter,
      recentFilters: loadRecentFilters(),
      inspectionFilter: 'None' as 'None' | 'Any' | 'Critical' | 'High' | 'Medium' | 'Low'
    };
  },
  getters: {
    availableRecentFilters: (state) => {
      return state.recentFilters.filter(x => x !== state.nameFilter);
    },
    allSortedNames: () => {
      const dataStore = useDataStore();
      const set = new Set<string>();
      dataStore.vaultUris.forEach(uri => {
        const meta = dataStore.knownSecretNames[uri];
        if (meta && meta.secrets) {
          meta.secrets.forEach(n => set.add(n.name));
        }
      });
      return Array.from(set).sort();
    },
    filteredNames: (state): string[] => {
      // Accessing a getter from this store
      let names = (useFilterStore()).allSortedNames;

      if (state.nameFilter.trim()) {
        const filters = state.nameFilter.split(/,(?![^{]*\})/).map(f => f.trim()).filter(f => f);
        
        // Pre-compile regexes outside the loop to prevent UI freezing
        const compiledFilters = filters.map(f => {
          try {
            return { isRegex: true, rx: new RegExp(f, 'i'), str: f };
          } catch {
            return { isRegex: false, rx: null, str: f.toLowerCase() };
          }
        });

        names = names.filter(name => {
          for (const f of compiledFilters) {
            if (f.isRegex) {
              if (f.rx!.test(name)) return true;
            } else {
              if (name.toLowerCase().includes(f.str)) return true;
            }
          }
          return false;
        });
      }
      return names;
    }
  },
  actions: {
    setNameFilter(filterText: string) {
      this.nameFilter = filterText;
    },
    setInspectionFilter(filterLevel: 'None' | 'Any' | 'Critical' | 'High' | 'Medium' | 'Low') {
      this.inspectionFilter = filterLevel;
    },
    applyNameFilter() {
      const f = this.nameFilter.trim();
      if (f) {
        const newHistory = [f, ...this.recentFilters.filter(x => x !== f)].slice(0, 10);
        this.recentFilters = newHistory;
        localStorage.setItem('recentFilters', JSON.stringify(newHistory));
      }
      const dataStore = useDataStore();
      const settingsStore = useSettingsStore();
      settingsStore.syncUrl(dataStore.vaultUris, this.nameFilter);
    },
    forgetRecentFilter(filterText: string) {
      this.recentFilters = this.recentFilters.filter(x => x !== filterText);
      localStorage.setItem('recentFilters', JSON.stringify(this.recentFilters));
    }
  }
});
