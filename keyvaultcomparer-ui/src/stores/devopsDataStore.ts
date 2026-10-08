import { defineStore } from 'pinia';
import { apiFetch } from '../services/apiClient';
import { openUrlInNewTab } from '../utils/urlOpener';

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
        const url = `/api/devops/variablegroups?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url);
        if (!res.ok) {
          const errData = await res.json().catch(() => null);
          throw new Error(errData?.error || errData?.details || 'Unknown ADO API error');
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
    },
    openAdoLibrary(groupId: number) {
      if (!this.organization || !this.project) return;
      const url = `https://dev.azure.com/${this.organization}/${this.project}/_library?itemType=VariableGroups&view=VariableGroupView&variableGroupId=${groupId}`;
      openUrlInNewTab(url);
    }
  },
  getters: {
    selectedGroups: (state) => {
      return state.variableGroups.filter(g => state.selectedGroupIds.includes(g.id));
    }
  }
});
