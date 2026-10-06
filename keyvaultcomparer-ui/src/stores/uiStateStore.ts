import { defineStore } from 'pinia';

export const useUiStateStore = defineStore('uiState', {
  state: () => ({
    globalLoadingValues: false,
    fetchingVaults: {} as Record<string, boolean>,
    loadingCells: {} as Record<string, Record<string, boolean>>,
    currentTab: 'select' as 'select' | 'analyze' | 'inspections-tool' | 'staged' | 'logs' | 'code' | 'devops',
    contextMenu: { show: false, x: 0, y: 0, colId: '' },
  }),
  actions: {
    setGlobalLoading(val: boolean) {
      this.globalLoadingValues = val;
    },
    setCurrentTab(tab: 'select' | 'analyze' | 'inspections-tool' | 'staged' | 'logs' | 'code' | 'devops') {
      this.currentTab = tab;
    },
    showContextMenu(x: number, y: number, colId: string) {
      this.contextMenu = { show: true, x, y, colId };
    },
    hideContextMenu() {
      this.contextMenu.show = false;
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
