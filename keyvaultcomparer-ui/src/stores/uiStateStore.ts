import { defineStore } from 'pinia';

export const useUiStateStore = defineStore('uiState', {
  state: () => ({
    globalLoadingValues: false,
    fetchingVaults: {} as Record<string, boolean>,
    loadingCells: {} as Record<string, Record<string, boolean>>,
    currentTab: 'select' as 'select' | 'analyze' | 'staged' | 'inspections' | 'logs',
  }),
  actions: {
    setGlobalLoading(val: boolean) {
      this.globalLoadingValues = val;
    },
    setCurrentTab(tab: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs') {
      this.currentTab = tab;
    },
    setVaultFetching(uri: string, isFetching: boolean) {
      this.fetchingVaults = { ...this.fetchingVaults, [uri]: isFetching };
    },
    setVaultsFetching(uris: string[], isFetching: boolean) {
      const newFetching = { ...this.fetchingVaults };
      uris.forEach(uri => newFetching[uri] = isFetching);
      this.fetchingVaults = newFetching;
    },
    setCellLoading(uri: string, names: string[], isLoading: boolean) {
      if (!this.loadingCells[uri]) {
        this.loadingCells[uri] = {};
      }
      names.forEach(name => {
        this.loadingCells[uri][name] = isLoading;
      });
      // Force reactivity update
      this.loadingCells = { ...this.loadingCells };
    },
    removeVaultStates(uri: string) {
      delete this.fetchingVaults[uri];
      delete this.loadingCells[uri];
      this.fetchingVaults = { ...this.fetchingVaults };
      this.loadingCells = { ...this.loadingCells };
    },
    clearAll() {
      this.globalLoadingValues = false;
      this.fetchingVaults = {};
      this.loadingCells = {};
    }
  }
});
