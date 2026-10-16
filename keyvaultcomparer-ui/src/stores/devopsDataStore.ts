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
  scanPosition?: number;
}

export interface PipelineRunStageInfo {
  stageName: string;
  status: string;
  startTime?: string;
  finishTime?: string;
  order?: number;
}

export interface PipelineRunInfo {
  buildId: number;
  buildNumber: string;
  overallStatus: string;
  branch?: string;
  commitSha?: string;
  author?: string;
  authorImageUrl?: string;
  triggerReason?: string;
  isAutomated?: boolean;
  deployedAt?: string;
  buildUrl?: string;
  prNumber?: string;
  stages: Record<string, PipelineRunStageInfo>;
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
  environmentHistory?: Record<string, EnvironmentDeploymentInfo[]>;
  recentRuns?: PipelineRunInfo[];
  scannedRunsCount?: number;
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
    const savedMatrixLayoutMode = (localStorage.getItem('adoMatrixLayoutMode') as 'cards' | 'rows' | 'columns') || 'rows';
    const savedScanDepth = localStorage.getItem('adoScanDepth') ? parseInt(localStorage.getItem('adoScanDepth')!) : 25;
    const savedOnlySucceeded = localStorage.getItem('adoOnlySucceeded') !== 'false';
    const savedShowRollback = localStorage.getItem('adoShowRollbackTarget') !== 'false';

    let savedHiddenEnvs: string[] = [];
    try {
      const storedHidden = localStorage.getItem('adoHiddenEnvironments');
      if (storedHidden) savedHiddenEnvs = JSON.parse(storedHidden);
    } catch {}

    const currentOrg = localStorage.getItem('adoOrg') || '';
    const currentProj = localStorage.getItem('adoProject') || '';
    const savedPipelinesOrg = localStorage.getItem('adoPipelinesOrg') || '';
    const savedPipelinesProject = localStorage.getItem('adoPipelinesProject') || '';

    let savedPipelines: PipelineSummary[] = [];
    const isMatchingScope = (!savedPipelinesOrg || savedPipelinesOrg.toLowerCase() === currentOrg.toLowerCase()) &&
                            (!savedPipelinesProject || savedPipelinesProject.toLowerCase() === currentProj.toLowerCase());

    if (isMatchingScope) {
      try {
        const stored = localStorage.getItem('adoCachedPipelines');
        if (stored) savedPipelines = JSON.parse(stored);
      } catch {}
    }

    let savedLastFetched: string | null = null;
    if (savedPipelines.length > 0) {
      savedLastFetched = localStorage.getItem('adoPipelinesLastFetched') || null;
    }

    let savedRows: Record<number, PipelineMatrixRow> = {};
    try {
      const stored = localStorage.getItem('adoCachedPipelineRows');
      if (stored) savedRows = JSON.parse(stored);
    } catch {}

    let initialReleaseMatrix: ReleaseMatrixResponse | null = null;
    if (savedPipelineIds.length > 0 && Object.keys(savedRows).length > 0) {
      const activeRows = savedPipelineIds
        .map(id => savedRows[id])
        .filter((row): row is PipelineMatrixRow => !!row);

      if (activeRows.length > 0) {
        const envs = [...savedEnvs];
        for (const row of activeRows) {
          if (row.environmentDeployments) {
            for (const envKey of Object.keys(row.environmentDeployments)) {
              if (!envs.some(e => e.toLowerCase() === envKey.toLowerCase())) {
                envs.push(envKey);
              }
            }
          }
        }
        const activeStageSet = new Set<string>();
        for (const row of activeRows) {
          if (row.deployments) {
            for (const stage of Object.keys(row.deployments)) {
              activeStageSet.add(stage);
            }
          }
        }
        initialReleaseMatrix = {
          environments: envs,
          topoSortedStages: Array.from(activeStageSet),
          rows: [...activeRows].sort((a, b) => a.pipelineName.localeCompare(b.pipelineName))
        };
      }
    }

    return {
      organization: currentOrg,
      project: currentProj,
      variableGroups: [] as AdoVariableGroup[],
      selectedGroupIds: [] as number[],
      isLoading: false,
      error: '',

      // Pipelines & Release Matrix State (Persisted across sessions)
      pipelines: savedPipelines as PipelineSummary[],
      pipelinesLastFetched: savedLastFetched as string | null,
      selectedPipelineIds: savedPipelineIds,
      releaseMatrix: initialReleaseMatrix as ReleaseMatrixResponse | null,
      cachedPipelineRows: savedRows as Record<number, PipelineMatrixRow>,
      cachedTopoSortedStages: [] as string[],
      cachedEnvironments: [] as string[],
      isPipelinesLoading: false,
      isMatrixLoading: false,
      pipelinesError: '',
      matrixError: '',
      copyingYamlPipelineId: null as number | null,
      deepScanningPipelineIds: [] as number[],
      singleRefreshingPipelineIds: [] as number[],
      hasPendingChanges: false,

      // Dual Display Mode ('environments' = Release Matrix, 'stages' = DAG Expanded)
      viewMode: savedViewMode as 'environments' | 'stages',

      // Environment Matrix Layout Mode ('cards' = default cards, 'rows' = cross-env rows, 'columns' = temporal columns)
      matrixLayoutMode: savedMatrixLayoutMode,

      // Hidden Environment Columns
      hiddenEnvironments: savedHiddenEnvs,

      // Pipeline Run Exploration & History Filters
      scanDepth: savedScanDepth,
      onlySucceeded: savedOnlySucceeded,
      showRollbackTarget: savedShowRollback,

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
        this.pipelinesLastFetched = new Date().toISOString();

        try {
          localStorage.setItem('adoCachedPipelines', JSON.stringify(this.pipelines));
          localStorage.setItem('adoPipelinesLastFetched', this.pipelinesLastFetched);
          localStorage.setItem('adoPipelinesOrg', this.organization);
          localStorage.setItem('adoPipelinesProject', this.project);
        } catch (e) {
          console.warn('Failed to save adoCachedPipelines to localStorage', e);
        }

        // If we already have selected pipelines, compute locally from cache without triggering remote matrix refresh
        if (this.selectedPipelineIds.length > 0) {
          this.computeReleaseMatrix();
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

    isSinglePipelineRefreshing(pipelineId: number): boolean {
      return this.singleRefreshingPipelineIds.includes(pipelineId);
    },

    async refreshSinglePipeline(pipelineId: number) {
      if (!this.organization || !this.project) return;
      if (this.singleRefreshingPipelineIds.includes(pipelineId)) return;

      this.singleRefreshingPipelineIds.push(pipelineId);

      try {
        const url = `/api/devops/pipelines/${pipelineId}/deep-scan?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            profileConfig: this.profileConfig,
            scanDepth: this.scanDepth,
            onlySucceeded: this.onlySucceeded
          })
        });

        if (!res.ok) {
          const errData = await res.json().catch(() => null);
          throw new Error(errData?.error || errData?.details || 'Failed to refresh pipeline');
        }

        const updatedRow: PipelineMatrixRow = await res.json();
        if (updatedRow && updatedRow.pipelineId) {
          this.cachedPipelineRows[updatedRow.pipelineId] = updatedRow;
          try {
            localStorage.setItem('adoCachedPipelineRows', JSON.stringify(this.cachedPipelineRows));
          } catch (e) {
            console.warn('Failed to save adoCachedPipelineRows to localStorage', e);
          }
          this.computeReleaseMatrix();
        }

        const anyUnscanned = this.selectedPipelineIds.some(id => {
          const r = this.cachedPipelineRows[id];
          return !r || !r.scannedRunsCount || r.scannedRunsCount === 0;
        });
        if (!anyUnscanned) {
          this.hasPendingChanges = false;
        }
      } catch (err: any) {
        console.error(`Single pipeline refresh failed for pipeline ${pipelineId}:`, err);
      } finally {
        this.singleRefreshingPipelineIds = this.singleRefreshingPipelineIds.filter(id => id !== pipelineId);
      }
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

        // Create placeholder row immediately so it appears in the grid/chips with no auto-network call
        const summary = this.pipelines.find(p => p.id === pipelineId);
        this.cachedPipelineRows[pipelineId] = {
          pipelineId,
          pipelineName: summary?.name || `Pipeline #${pipelineId}`,
          repository: summary?.repository,
          deployments: {},
          environmentDeployments: {},
          environmentHistory: {},
          recentRuns: [],
          scannedRunsCount: 0
        };
        this.hasPendingChanges = true;
        this.computeReleaseMatrix();
      }
    },

    removePipeline(pipelineId: number) {
      const idx = this.selectedPipelineIds.indexOf(pipelineId);
      if (idx > -1) {
        this.selectedPipelineIds.splice(idx, 1);
        this.saveSelectedPipelineIds();
        // Compute in-memory with zero network overhead
        this.computeReleaseMatrix();
        if (this.selectedPipelineIds.length === 0) {
          this.hasPendingChanges = false;
        }
      }
    },

    clearSelectedPipelines() {
      this.selectedPipelineIds = [];
      this.saveSelectedPipelineIds();
      this.releaseMatrix = null;
      this.hasPendingChanges = false;
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
      try {
        localStorage.removeItem('adoCachedPipelineRows');
      } catch {}
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
            profileConfig: this.profileConfig,
            scanDepth: this.scanDepth,
            onlySucceeded: this.onlySucceeded,
            forceRefresh: forceRefresh
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
          try {
            localStorage.setItem('adoCachedPipelineRows', JSON.stringify(this.cachedPipelineRows));
          } catch (e) {
            console.warn('Failed to save adoCachedPipelineRows to localStorage', e);
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
        this.hasPendingChanges = false;
      } catch (err: any) {
        this.matrixError = err.message || 'Error fetching release matrix';
      } finally {
        this.isMatrixLoading = false;
      }
    },

    async deepScanPipeline(pipelineId: number, additionalDepth: number = 50) {
      if (!this.organization || !this.project) return;
      if (this.deepScanningPipelineIds.includes(pipelineId)) return;

      this.deepScanningPipelineIds.push(pipelineId);

      // Determine target scan depth
      const currentRow = this.cachedPipelineRows[pipelineId];
      const currentRunsCount = currentRow?.recentRuns?.length || this.scanDepth;
      const targetDepth = Math.min(Math.max(currentRunsCount + additionalDepth, this.scanDepth + additionalDepth), 200);

      try {
        const url = `/api/devops/pipelines/${pipelineId}/deep-scan?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            profileConfig: this.profileConfig,
            scanDepth: targetDepth,
            onlySucceeded: this.onlySucceeded
          })
        });

        if (!res.ok) {
          const errData = await res.json().catch(() => null);
          throw new Error(errData?.error || errData?.details || 'Failed to deep scan pipeline');
        }

        const updatedRow: PipelineMatrixRow = await res.json();
        if (updatedRow && updatedRow.pipelineId) {
          this.cachedPipelineRows[updatedRow.pipelineId] = updatedRow;
          try {
            localStorage.setItem('adoCachedPipelineRows', JSON.stringify(this.cachedPipelineRows));
          } catch (e) {
            console.warn('Failed to save adoCachedPipelineRows to localStorage', e);
          }
          this.computeReleaseMatrix();
        }
      } catch (err: any) {
        console.error(`Deep scan failed for pipeline ${pipelineId}:`, err);
      } finally {
        this.deepScanningPipelineIds = this.deepScanningPipelineIds.filter(id => id !== pipelineId);
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
    },

    setMatrixLayoutMode(mode: 'cards' | 'rows' | 'columns') {
      this.matrixLayoutMode = mode;
      localStorage.setItem('adoMatrixLayoutMode', mode);
    },

    hideEnvironment(envName: string) {
      if (!this.hiddenEnvironments.some(e => e.toLowerCase() === envName.toLowerCase())) {
        this.hiddenEnvironments.push(envName);
        localStorage.setItem('adoHiddenEnvironments', JSON.stringify(this.hiddenEnvironments));
      }
    },

    hideOtherEnvironments(envToKeep: string) {
      const allEnvs = this.releaseMatrix?.environments || [];
      this.hiddenEnvironments = allEnvs.filter(e => e.toLowerCase() !== envToKeep.toLowerCase());
      localStorage.setItem('adoHiddenEnvironments', JSON.stringify(this.hiddenEnvironments));
    },

    showAllHiddenEnvironments() {
      this.hiddenEnvironments = [];
      localStorage.removeItem('adoHiddenEnvironments');
    },

    setScanDepth(val: number) {
      this.scanDepth = val;
      localStorage.setItem('adoScanDepth', val.toString());
      this.hasPendingChanges = true;
    },

    setOnlySucceeded(val: boolean) {
      this.onlySucceeded = val;
      localStorage.setItem('adoOnlySucceeded', val.toString());
      this.hasPendingChanges = true;
    },

    setShowRollbackTarget(val: boolean) {
      this.showRollbackTarget = val;
      localStorage.setItem('adoShowRollbackTarget', val.toString());
    },

    clearLocalPipelinesCache() {
      this.pipelines = [];
      this.pipelinesLastFetched = null;
      this.cachedPipelineRows = {};
      this.releaseMatrix = null;
      try {
        localStorage.removeItem('adoCachedPipelines');
        localStorage.removeItem('adoPipelinesLastFetched');
        localStorage.removeItem('adoCachedPipelineRows');
      } catch {}
    }
  },
  getters: {
    selectedGroups: (state) => {
      return state.variableGroups.filter(g => state.selectedGroupIds.includes(g.id));
    },
    selectedPipelines: (state) => {
      return state.selectedPipelineIds.map(id => {
        const found = state.pipelines.find(p => p.id === id);
        if (found) return found;
        const cached = state.cachedPipelineRows[id];
        return {
          id,
          name: cached?.pipelineName || `Pipeline #${id}`,
          repository: cached?.repository
        };
      });
    }
  }
});
