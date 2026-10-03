import { defineStore } from 'pinia';

export interface AdoVariableGroup {
  id: number;
  name: string;
  type: string;
  variables: Record<string, any>;
  providerData?: {
    serviceEndpointId?: string;
    vault?: string;
  };
}

export const useDevopsDataStore = defineStore('devopsData', {
  state: () => ({
    organization: localStorage.getItem('adoOrg') || '',
    project: localStorage.getItem('adoProject') || '',
    variableGroups: [] as AdoVariableGroup[],
    selectedGroupIds: [] as number[],
    isLoading: false,
    error: '',
  }),
  actions: {
    async fetchVariableGroups() {
      if (!this.organization || !this.project) return;
      
      localStorage.setItem('adoOrg', this.organization);
      localStorage.setItem('adoProject', this.project);
      
      this.isLoading = true;
      this.error = '';
      this.variableGroups = [];
      
      try {
        // Pointing to mock endpoint for now
        const url = `http://localhost:8081/`; 
        const res = await fetch(url);
        if (!res.ok) {
          throw new Error('Failed to fetch ADO variable groups');
        }
        const data = await res.json();
        this.variableGroups = data.value || [];
      } catch (err: any) {
        this.error = err.message || 'Failed to fetch Variable Groups';
      } finally {
        this.isLoading = false;
      }
    },
    toggleGroupSelection(groupId: number) {
      const index = this.selectedGroupIds.indexOf(groupId);
      if (index > -1) {
        this.selectedGroupIds.splice(index, 1);
      } else {
        this.selectedGroupIds.push(groupId);
      }
    }
  },
  getters: {
    selectedGroups: (state) => {
      return state.variableGroups.filter(g => state.selectedGroupIds.includes(g.id));
    }
  }
});
