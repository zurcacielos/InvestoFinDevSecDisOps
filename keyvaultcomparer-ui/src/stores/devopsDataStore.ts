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
  authorImageUrl?: string;
  triggerReason?: string;
  isAutomated?: boolean;
  deployedAt?: string;
  buildUrl?: string;
  prNumber?: string;
}

export interface PipelineYamlLink {
  title: string;
  repository: string;
  path: string;
  branch: string;
  url: string;
  isMainPipeline: boolean;
}

export interface PipelineYamlLinksResponse {
  pipelineId: number;
  pipelineName: string;
  links: PipelineYamlLink[];
}

export interface EnvironmentDeploymentInfo {
  environment: string;
  status: string;
  buildId: number;
  buildNumber: string;
  branch?: string;
  commitSha?: string;
  author?: string;
  authorImageUrl?: string;
  triggerReason?: string;
  isAutomated?: boolean;
  deployedAt?: string;
  buildUrl?: string;
  prNumber?: string;
  operation?: string;
  version?: string;
}

export interface PipelineProfileConfig {
  environmentParameterName: string;
  operationParameterName: string;
  environments: string[];
}

export interface PipelineMatrixRow {
  pipelineId: number;
  pipelineName: string;
  yamlFilename?: string;
  repository?: PipelineRepository;
  deployments: Record<string, StageDeploymentInfo>;
  environmentDeployments: Record<string, EnvironmentDeploymentInfo>;
}

export interface ReleaseMatrixResponse {
  environments: string[];
  topoSortedStages: string[];
  rows: PipelineMatrixRow[];
}

export const useDevopsDataStore = defineStore('devopsData', {
  state: () => {
    let savedPipelineIds: number[] = [];
    try {
      const stored = localStorage.getItem('adoSelectedPipelineIds');
      if (stored) savedPipelineIds = JSON.parse(stored);
    } catch {}

    let savedEnvs = ['dev', 'uat', 'uat1', 'uat2', 'stg', 'stg1', 'prd', 'prd1'];
    try {
      const storedEnvs = localStorage.getItem('adoTargetEnvs');
      if (storedEnvs) savedEnvs = JSON.parse(storedEnvs);
    } catch {}

    const savedViewMode = (localStorage.getItem('adoMatrixViewMode') as 'environments' | 'stages') || 'environments';

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
      cachedPipelineRows: {} as Record<number, PipelineMatrixRow>,
      cachedTopoSortedStages: [] as string[],
      cachedEnvironments: [] as string[],
      isPipelinesLoading: false,
      isMatrixLoading: false,
      pipelinesError: '',
      matrixError: '',
      copyingYamlPipelineId: null as number | null,

      // Dual Display Mode ('environments' = Release Matrix, 'stages' = DAG Expanded)
      viewMode: savedViewMode as 'environments' | 'stages',

      // Parametrization & Profile Configuration
      profileConfig: {
        environmentParameterName: localStorage.getItem('adoEnvParamName') || 'Environment',
        operationParameterName: localStorage.getItem('adoOpParamName') || 'Operation',
        environments: savedEnvs,
      } as PipelineProfileConfig,
      isProfileModalOpen: false,

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

    computeReleaseMatrix() {
      if (this.selectedPipelineIds.length === 0) {
        this.releaseMatrix = null;
        return;
      }

      // Filter cached rows according to currently selected pipeline IDs
      const activeRows = this.selectedPipelineIds
        .map(id => this.cachedPipelineRows[id])
        .filter((row): row is PipelineMatrixRow => !!row);

      if (activeRows.length === 0) {
        this.releaseMatrix = null;
        return;
      }

      // Compute dynamic environments list: start with profileConfig.environments
      const envs = [...this.profileConfig.environments];
      for (const row of activeRows) {
        if (row.environmentDeployments) {
          for (const envKey of Object.keys(row.environmentDeployments)) {
            if (!envs.some(e => e.toLowerCase() === envKey.toLowerCase())) {
              envs.push(envKey);
            }
          }
        }
      }

      // Compute active stages: stages present in currently selected rows
      const activeStageSet = new Set<string>();
      for (const row of activeRows) {
        if (row.deployments) {
          for (const stage of Object.keys(row.deployments)) {
            activeStageSet.add(stage);
          }
        }
      }

      // Preserve topological sort order from cache for active stages
      const sortedStages: string[] = [];
      if (this.cachedTopoSortedStages && this.cachedTopoSortedStages.length > 0) {
        for (const stage of this.cachedTopoSortedStages) {
          if (activeStageSet.has(stage) && !sortedStages.includes(stage)) {
            sortedStages.push(stage);
          }
        }
      }
      // Add any remaining active stages not yet in cachedTopoSortedStages
      for (const stage of activeStageSet) {
        if (!sortedStages.includes(stage)) {
          sortedStages.push(stage);
        }
      }

      this.releaseMatrix = {
        environments: envs,
        topoSortedStages: sortedStages.length > 0 ? sortedStages : (this.cachedTopoSortedStages || []),
        rows: [...activeRows].sort((a, b) => a.pipelineName.localeCompare(b.pipelineName))
      };
    },

    async selectPipeline(pipelineId: number) {
      if (!this.selectedPipelineIds.includes(pipelineId)) {
        this.selectedPipelineIds.push(pipelineId);
        this.saveSelectedPipelineIds();

        // If row is already cached in store, compute locally with zero network latency
        if (this.cachedPipelineRows[pipelineId]) {
          this.computeReleaseMatrix();
          return;
        }

        // Only fetch the newly added pipeline from server
        await this.fetchReleaseMatrix([pipelineId]);
      }
    },

    removePipeline(pipelineId: number) {
      const idx = this.selectedPipelineIds.indexOf(pipelineId);
      if (idx > -1) {
        this.selectedPipelineIds.splice(idx, 1);
        this.saveSelectedPipelineIds();
        // Compute in-memory with zero network overhead
        this.computeReleaseMatrix();
      }
    },

    clearSelectedPipelines() {
      this.selectedPipelineIds = [];
      this.saveSelectedPipelineIds();
      this.releaseMatrix = null;
    },

    async togglePipelineSelection(pipelineId: number) {
      if (this.selectedPipelineIds.includes(pipelineId)) {
        this.removePipeline(pipelineId);
      } else {
        await this.selectPipeline(pipelineId);
      }
    },

    saveSelectedPipelineIds() {
      localStorage.setItem('adoSelectedPipelineIds', JSON.stringify(this.selectedPipelineIds));
    },

    setViewMode(mode: 'environments' | 'stages') {
      this.viewMode = mode;
      localStorage.setItem('adoMatrixViewMode', mode);
    },

    setProfileModalOpen(open: boolean) {
      this.isProfileModalOpen = open;
    },

    async updateProfileConfig(newConfig: PipelineProfileConfig) {
      this.profileConfig = { ...newConfig };
      localStorage.setItem('adoEnvParamName', newConfig.environmentParameterName);
      localStorage.setItem('adoOpParamName', newConfig.operationParameterName);
      localStorage.setItem('adoTargetEnvs', JSON.stringify(newConfig.environments));
      // Invalidate row cache because environment extraction depends on profileConfig
      this.cachedPipelineRows = {};
      await this.fetchReleaseMatrix(undefined, true);
    },

    async fetchReleaseMatrix(pipelineIdsToFetch?: number[], forceRefresh = false) {
      if (!this.organization || !this.project) return;
      if (this.selectedPipelineIds.length === 0) {
        this.releaseMatrix = null;
        return;
      }

      // Determine which IDs need to be fetched from the backend
      let targetIds: number[];
      if (pipelineIdsToFetch && pipelineIdsToFetch.length > 0) {
        targetIds = pipelineIdsToFetch;
      } else if (forceRefresh) {
        targetIds = [...this.selectedPipelineIds];
      } else {
        // Only fetch pipelines that are not yet in cache
        targetIds = this.selectedPipelineIds.filter(id => !this.cachedPipelineRows[id]);
      }

      // If all selected pipelines are already cached and forceRefresh is false, compute locally
      if (targetIds.length === 0 && !forceRefresh) {
        this.computeReleaseMatrix();
        return;
      }

      this.isMatrixLoading = true;
      this.matrixError = '';

      try {
        const url = `/api/devops/pipelines/matrix?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ 
            pipelineIds: targetIds,
            profileConfig: this.profileConfig
          })
        });

        if (!res.ok) {
          const errData = await res.json().catch(() => null);
          throw new Error(errData?.error || errData?.details || 'Failed to fetch release matrix');
        }

        const data: ReleaseMatrixResponse = await res.json();
        
        // Cache newly fetched rows in the store
        if (data.rows && data.rows.length > 0) {
          for (const row of data.rows) {
            this.cachedPipelineRows[row.pipelineId] = row;
          }
        }

        // Cache stages
        if (data.topoSortedStages && data.topoSortedStages.length > 0) {
          for (const stage of data.topoSortedStages) {
            if (!this.cachedTopoSortedStages.includes(stage)) {
              this.cachedTopoSortedStages.push(stage);
            }
          }
        }

        // Cache environments
        if (data.environments && data.environments.length > 0) {
          for (const env of data.environments) {
            if (!this.cachedEnvironments.includes(env)) {
              this.cachedEnvironments.push(env);
            }
          }
        }

        // Recompute the release matrix for all currently selected pipeline IDs
        this.computeReleaseMatrix();
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
    },

    async getPipelineGithubYamls(pipelineId: number, githubBaseUrl?: string, githubOrg?: string): Promise<PipelineYamlLinksResponse | null> {
      if (!this.organization || !this.project) return null;
      try {
        const params = new URLSearchParams({
          organization: this.organization,
          project: this.project,
        });
        if (githubBaseUrl) params.append('githubBaseUrl', githubBaseUrl);
        if (githubOrg) params.append('githubOrg', githubOrg);

        const res = await apiFetch(
          `/api/devops/pipelines/${pipelineId}/github-yamls?${params.toString()}`
        );
        if (!res.ok) return null;
        const data = await res.json();
        return data as PipelineYamlLinksResponse;
      } catch (err: any) {
        console.error('Failed to get pipeline GitHub YAMLs:', err);
        return null;
      }
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
