import { defineStore } from 'pinia';
import { apiFetch } from '../services/apiClient';
import { useFilterStore } from './filterStore';
import { useSettingsStore, loadSharableConfig } from './settingsStore';
import { useAuthStore } from './authStore';
import { useUiStateStore } from './uiStateStore';
import { useStagedStore } from './stagedStore';
import type { SecretMetadata } from '../inspections';

export const useDataStore = defineStore('data', {
  state: () => {
    const urlConfig = loadSharableConfig();
    let initialUris: string[] = [];
    if (urlConfig && Array.isArray(urlConfig.v)) {
      initialUris = urlConfig.v;
    } else {
      try {
        initialUris = JSON.parse(localStorage.getItem('savedVaultUris') || '[]');
      } catch (e) {}
    }

    let initialKnownSecretNames = {};
    try {
      initialKnownSecretNames = JSON.parse(localStorage.getItem('knownSecretNames') || '{}');
    } catch (e) {}

    let initialVaultData = {};
    try {
      initialVaultData = JSON.parse(localStorage.getItem('vaultData') || '{}');
    } catch (e) {}

    let initialLastFetched = {};
    try {
      initialLastFetched = JSON.parse(localStorage.getItem('lastFetched') || '{}');
    } catch (e) {}

    return {
      vaultUris: initialUris,
      knownSecretNames: initialKnownSecretNames as Record<string, { secrets: SecretMetadata[], errorMessage?: string }>,
      vaultData: initialVaultData as Record<string, Record<string, any>>,
      lastFetched: initialLastFetched as Record<string, number>,
    };
  },
  actions: {
    _saveVaultUris() {
      localStorage.setItem('savedVaultUris', JSON.stringify(this.vaultUris));
      const settingsStore = useSettingsStore();
      const filterStore = useFilterStore();
      settingsStore.syncUrl(this.vaultUris, filterStore.nameFilter);
    },
    _saveKnownSecretNames() {
      try {
        localStorage.setItem('knownSecretNames', JSON.stringify(this.knownSecretNames));
      } catch (e) {
        console.warn('Failed to save knownSecretNames to localStorage', e);
      }
    },
    _saveVaultData() {
      try {
        localStorage.setItem('vaultData', JSON.stringify(this.vaultData));
        localStorage.setItem('lastFetched', JSON.stringify(this.lastFetched));
      } catch (e) {
        console.warn('Failed to save vaultData to localStorage', e);
      }
    },
    setVaultUris(uris: string[]) {
      this.vaultUris = uris;
      this._saveVaultUris();
    },
    addVaultUri(uri: string) {
      if (!this.vaultUris.includes(uri)) {
        this.vaultUris.push(uri);
        this._saveVaultUris();
      }
    },
    removeVault(index: number) {
      const uri = this.vaultUris[index];
      if (!uri) return;
      this.vaultUris.splice(index, 1);
      this._saveVaultUris();
      
      delete this.knownSecretNames[uri];
      this._saveKnownSecretNames();
      delete this.vaultData[uri];
      delete this.lastFetched[uri];
      this._saveVaultData();

      const uiStateStore = useUiStateStore();
      uiStateStore.removeVaultStates(uri);
    },
    mergeKnownSecretNames(uri: string, data: { secrets: SecretMetadata[], errorMessage?: string }) {
      this.knownSecretNames = { ...this.knownSecretNames, [uri]: data };
      this._saveKnownSecretNames();
    },
    mergeVaultData(uri: string, data: Record<string, any>) {
      if (!this.vaultData[uri]) {
        this.vaultData[uri] = {};
      }
      this.vaultData[uri] = { ...this.vaultData[uri], ...data };
      this.lastFetched[uri] = Date.now();
      this._saveVaultData();
    },
    clearAll() {
      this.knownSecretNames = {};
      this._saveKnownSecretNames();
      this.vaultData = {};
      this.lastFetched = {};
      this._saveVaultData();
      const uiStateStore = useUiStateStore();
      uiStateStore.clearAll();
      const stagedStore = useStagedStore();
      stagedStore.clearAll();
    },
    
    // API Fetching Actions - Pure Data Merging, offloading UI state to uiStateStore
    
    async fetchVaultKeys(urisToFetch?: string[]) {
      const currentUris = urisToFetch || [...this.vaultUris];
      if (currentUris.length === 0) return;
      
      const uiStateStore = useUiStateStore();
      uiStateStore.setVaultsFetching(currentUris, true);

      try {
        const authStore = useAuthStore();
        if (!(await authStore.connectToAzure())) return;
        
        const response = await apiFetch('/api/vaults/keys', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(currentUris)
        });
        if (response.ok) {
          const data = await response.json();
          for (const [uri, res] of Object.entries(data)) {
            const result = res as any;
            const mappedSecrets = result.secrets.map((n: any) => {
              if (typeof n === 'string') return { name: n.toUpperCase() } as SecretMetadata;
              return {...n, name: (n?.name || '').toString().toUpperCase()} as SecretMetadata;
            });
            this.mergeKnownSecretNames(uri, { secrets: mappedSecrets, errorMessage: result.errorMessage });
          }
        }
      } catch (error) {
        console.error('Failed to fetch keys', error);
      } finally {
        uiStateStore.setVaultsFetching(currentUris, false);
      }
    },
    
    async refetchNames() {
      if (this.vaultUris.length === 0) return;
      const currentUris = [...this.vaultUris];
      
      const uiStateStore = useUiStateStore();
      uiStateStore.setVaultsFetching(currentUris, true);

      try {
        const authStore = useAuthStore();
        if (!(await authStore.connectToAzure())) return;
        
        const response = await apiFetch('/api/vaults/keys', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(currentUris)
        });
        if (!response.ok) throw new Error('Failed to fetch keys');
        
        const data = await response.json();
        for (const [uri, res] of Object.entries(data)) {
          const result = res as { secrets: SecretMetadata[], errorMessage?: string };
          this.mergeKnownSecretNames(uri, {
            secrets: result.secrets.map(n => ({...n, name: n.name.toUpperCase()})),
            errorMessage: result.errorMessage
          });
        }
      } catch (error) {
        console.error('Error fetching names:', error);
        alert('Failed to fetch secret names. Please try again.');
      } finally {
        uiStateStore.setVaultsFetching(currentUris, false);
      }
    },
    
    async fetchValuesForVaultAndNames(uri: string, namesToFetch: string[]) {
      if (namesToFetch.length === 0) return;
      
      const uiStateStore = useUiStateStore();
      uiStateStore.setVaultFetching(uri, true);
      uiStateStore.setCellLoading(uri, namesToFetch, true);

      try {
        const authStore = useAuthStore();
        if (!(await authStore.connectToAzure())) return;

        const response = await apiFetch('/api/vault/values', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ 
            vaultUri: uri,
            secretNames: namesToFetch
          })
        });
        
        if (response.ok) {
          const data = await response.json();
          this.mergeVaultData(uri, data);
        } else {
          console.error('Failed to fetch values for vault', uri);
        }
      } catch (error) {
        console.error('Network error fetching values for vault', uri, error);
      } finally {
        uiStateStore.setVaultFetching(uri, false);
        uiStateStore.setCellLoading(uri, namesToFetch, false);
      }
    },
    
    async fetchValuesForVault(uri: string) {
      const filterStore = useFilterStore();
      const settingsStore = useSettingsStore();
      
      const limit = settingsStore.uiSettings.resultLimit;
      const namesToFetch = filterStore.filteredNames.slice(0, limit > 0 ? limit : undefined);
      await this.fetchValuesForVaultAndNames(uri, namesToFetch);
    },
    
    async fetchValuesForRow(secretName: string) {
      if (this.vaultUris.length === 0) return;
      try {
        const tasks = this.vaultUris.map(uri => this.fetchValuesForVaultAndNames(uri, [secretName]));
        await Promise.all(tasks);
      } catch (error) {
        console.error('Error fetching values for row:', error);
      }
    },
    
    async fetchComparison() {
      if (this.vaultUris.length === 0) return;
      const authStore = useAuthStore();
      if (!(await authStore.connectToAzure())) return;
      
      const filterStore = useFilterStore();
      filterStore.applyNameFilter(); // Update recent searches
      
      const uiStateStore = useUiStateStore();
      uiStateStore.setGlobalLoading(true);
      
      try {
        const tasks = this.vaultUris.map(uri => this.fetchValuesForVault(uri));
        await Promise.all(tasks);
      } catch (error) {
        console.error('Error fetching comparison:', error);
        alert('Failed to fetch comparison data. Please try again.');
      } finally {
        uiStateStore.setGlobalLoading(false);
      }
    }
  }
});
