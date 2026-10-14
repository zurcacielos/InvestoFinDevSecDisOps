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

export interface PipelineRepository {
  id?: string;
  name?: string;
  url?: string;
  type?: string;
  defaultBranch?: string;
}

export interface PipelineSummary {
  id: number;
  name: string;
  path?: string;
  repository?: PipelineRepository;
}

export interface StageDeploymentInfo {
  stageName: string;
  status: string;
  buildId: number;
  buildNumber: string;
  branch?: string;
  commitSha?: string;
  author?: string;
  deployedAt?: string;
  buildUrl?: string;
  prNumber?: string;
}

export interface PipelineMatrixRow {
  pipelineId: number;
  pipelineName: string;
  repository?: PipelineRepository;
  deployments: Record<string, StageDeploymentInfo>;
}

export interface ReleaseMatrixResponse {
  environments: string[];
  rows: PipelineMatrixRow[];
}

export const useDevopsDataStore = defineStore('devopsData', {
  state: () => {
    let savedPipelineIds: number[] = [];
    try {
      const stored = localStorage.getItem('adoSelectedPipelineIds');
      if (stored) savedPipelineIds = JSON.parse(stored);
    } catch {}

    return {
      organization: localStorage.getItem('adoOrg') || '',
      project: localStorage.getItem('adoProject') || '',
      variableGroups: [] as AdoVariableGroup[],
      selectedGroupIds: [] as number[],
      isLoading: false,
      error: '',

      // Pipelines & Release Matrix State
      pipelines: [] as PipelineSummary[],
      selectedPipelineIds: savedPipelineIds,
      releaseMatrix: null as ReleaseMatrixResponse | null,
      isPipelinesLoading: false,
      isMatrixLoading: false,
      pipelinesError: '',
      matrixError: '',
      copyingYamlPipelineId: null as number | null,
      yamlModal: {
        show: false,
        pipelineId: 0,
        pipelineName: '',
        yaml: '',
        isLoading: false,
        error: '',
      },
    };
  },
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
    },
    async addVariableToGroup(groupId: number, secretName: string) {
      if (!this.organization || !this.project) return;
      try {
        const url = `/api/devops/variablegroups/${groupId}/variables?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const response = await apiFetch(url, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ secretName })
        });
        
        if (!response.ok) {
          const text = await response.text();
          let errorMessage = text;
          try {
            const parsed = JSON.parse(text);
            errorMessage = `${parsed.error || 'Unknown Error'}\n${parsed.details || ''}`;
          } catch (e) {}
          alert(`Azure DevOps Error:\n${errorMessage}`);
        } else {
          const group = this.variableGroups.find(g => g.id === groupId);
          if (group) {
            if (!group.variables) {
              group.variables = {};
            }
            group.variables[secretName] = { enabled: true };
          }
        }
      } catch (err: any) {
        alert(`Failed to add variable to library:\n${err.message || err}`);
      }
    },

    // --- PIPELINES & RELEASES MATRIX ACTIONS ---

    async fetchPipelines() {
      if (!this.organization || !this.project) return;

      localStorage.setItem('adoOrg', this.organization);
      localStorage.setItem('adoProject', this.project);

      this.isPipelinesLoading = true;
      this.pipelinesError = '';

      try {
        const url = `/api/devops/pipelines?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url);
        if (!res.ok) {
          const errData = await res.json().catch(() => null);
          throw new Error(errData?.error || errData?.details || 'Failed to retrieve ADO pipelines');
        }
        const data = await res.json();
        this.pipelines = data || [];

        // If we already have selected pipelines, fetch matrix automatically
        if (this.selectedPipelineIds.length > 0) {
          await this.fetchReleaseMatrix();
        }
      } catch (err: any) {
        this.pipelinesError = err.message || 'Error fetching pipelines';
      } finally {
        this.isPipelinesLoading = false;
      }
    },

    selectPipeline(pipelineId: number) {
      if (!this.selectedPipelineIds.includes(pipelineId)) {
        this.selectedPipelineIds.push(pipelineId);
        this.saveSelectedPipelineIds();
        this.fetchReleaseMatrix();
      }
    },

    removePipeline(pipelineId: number) {
      const idx = this.selectedPipelineIds.indexOf(pipelineId);
      if (idx > -1) {
        this.selectedPipelineIds.splice(idx, 1);
        this.saveSelectedPipelineIds();
        if (this.selectedPipelineIds.length > 0) {
          this.fetchReleaseMatrix();
        } else {
          this.releaseMatrix = null;
        }
      }
    },

    togglePipelineSelection(pipelineId: number) {
      if (this.selectedPipelineIds.includes(pipelineId)) {
        this.removePipeline(pipelineId);
      } else {
        this.selectPipeline(pipelineId);
      }
    },

    saveSelectedPipelineIds() {
      localStorage.setItem('adoSelectedPipelineIds', JSON.stringify(this.selectedPipelineIds));
    },

    async fetchReleaseMatrix() {
      if (!this.organization || !this.project) return;
      if (this.selectedPipelineIds.length === 0) {
        this.releaseMatrix = null;
        return;
      }

      this.isMatrixLoading = true;
      this.matrixError = '';

      try {
        const url = `/api/devops/pipelines/matrix?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ pipelineIds: this.selectedPipelineIds })
        });

        if (!res.ok) {
          const errData = await res.json().catch(() => null);
          throw new Error(errData?.error || errData?.details || 'Failed to fetch release matrix');
        }

        const data: ReleaseMatrixResponse = await res.json();
        this.releaseMatrix = data;
      } catch (err: any) {
        this.matrixError = err.message || 'Error fetching release matrix';
      } finally {
        this.isMatrixLoading = false;
      }
    },

    async fetchPipelinePreviewYaml(pipelineId: number): Promise<string> {
      if (!this.organization || !this.project) {
        throw new Error('Organization and Project are required');
      }

      const url = `/api/devops/pipelines/${pipelineId}/preview-yaml?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
      const res = await apiFetch(url, { method: 'POST' });

      if (!res.ok) {
        const errData = await res.json().catch(() => null);
        throw new Error(errData?.error || 'Failed to preview pipeline YAML');
      }

      const data = await res.json();
      return data.finalYaml;
    },

    async copyPipelineYamlToClipboard(pipelineId: number, pipelineName?: string) {
      this.copyingYamlPipelineId = pipelineId;
      try {
        const yaml = await this.fetchPipelinePreviewYaml(pipelineId);
        await navigator.clipboard.writeText(yaml);
        return { success: true, length: yaml.length, name: pipelineName || `Pipeline ${pipelineId}` };
      } catch (err: any) {
        alert(`Failed to copy YAML:\n${err.message || err}`);
        return { success: false, error: err.message };
      } finally {
        this.copyingYamlPipelineId = null;
      }
    },

    async openYamlModal(pipelineId: number, pipelineName?: string) {
      this.yamlModal = {
        show: true,
        pipelineId,
        pipelineName: pipelineName || `Pipeline ${pipelineId}`,
        yaml: '',
        isLoading: true,
        error: '',
      };

      try {
        const yaml = await this.fetchPipelinePreviewYaml(pipelineId);
        this.yamlModal.yaml = yaml;
      } catch (err: any) {
        this.yamlModal.error = err.message || 'Failed to fetch pipeline YAML';
      } finally {
        this.yamlModal.isLoading = false;
      }
    },

    closeYamlModal() {
      this.yamlModal.show = false;
      this.yamlModal.yaml = '';
      this.yamlModal.error = '';
    }
  },
  getters: {
    selectedGroups: (state) => {
      return state.variableGroups.filter(g => state.selectedGroupIds.includes(g.id));
    },
    selectedPipelines: (state) => {
      return state.pipelines.filter(p => state.selectedPipelineIds.includes(p.id));
    }
  }
});
