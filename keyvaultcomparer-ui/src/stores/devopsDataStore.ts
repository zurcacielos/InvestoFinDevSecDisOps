import { defineStore } from 'pinia';
import { apiFetch } from '../services/apiClient';
import { openUrlInNewTab } from '../utils/urlOpener';
import { useSettingsStore } from './settingsStore';

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
  hasRuns?: boolean;
  runCount?: number;
  latestBuildNumber?: string;
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
  hasReachedRunHistoryEnd?: boolean;
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

    let savedRecentPipelineIds: number[] = [];
    try {
      const storedRecents = localStorage.getItem('adoRecentPipelineIds');
      if (storedRecents) savedRecentPipelineIds = JSON.parse(storedRecents);
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
    const savedPriorRunMode = (localStorage.getItem('adoPriorRunMode') as 'distinct' | 'consecutive') || 'distinct';

    const savedColWidth = localStorage.getItem('adoPipelineColWidth') ? parseInt(localStorage.getItem('adoPipelineColWidth')!) : 260;

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
      backendGithubOrg: '',
      backendAdoOrg: '',
      variableGroups: [] as AdoVariableGroup[],
      selectedGroupIds: [] as number[],
      isLoading: false,
      error: '',

      // Pipelines & Release Matrix State (Persisted across sessions)
      pipelines: savedPipelines as PipelineSummary[],
      pipelinesLastFetched: savedLastFetched as string | null,
      selectedPipelineIds: savedPipelineIds,
      recentPipelineIds: savedRecentPipelineIds as number[],
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
      priorRunMode: savedPriorRunMode as 'distinct' | 'consecutive',
      pipelineColumnWidth: savedColWidth || 260,

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

    mergePipelineMatrixRows(existing: PipelineMatrixRow, incoming: PipelineMatrixRow): PipelineMatrixRow {
      // 1. Merge recent runs without duplicates and sort chronologically descending
      const incomingRuns = incoming.recentRuns || [];
      const existingRuns = existing.recentRuns || [];
      const hasOverlap = incomingRuns.length === 0 || existingRuns.length === 0 ||
        incomingRuns.some(inRun => existingRuns.some(exRun => exRun.buildId === inRun.buildId));

      if (!hasOverlap && incomingRuns.length > 0 && existingRuns.length > 0) {
        // Disjoint gap detected: incoming runs are strictly newer than existing runs without overlap.
        // Schedule an immediate bridge fetch for this pipeline to close the gap.
        const neededAdditional = Math.min(incomingRuns.length, 500 - (existing.scannedRunsCount || 0));
        if (neededAdditional > 0 && !this.deepScanningPipelineIds.includes(incoming.pipelineId)) {
          setTimeout(() => {
            this.deepScanPipeline(incoming.pipelineId, neededAdditional);
          }, 150);
        }
      }

      const mergedRuns: PipelineRunInfo[] = [];
      const seenRunBuildIds = new Set<number>();
      for (const run of incomingRuns) {
        if (!seenRunBuildIds.has(run.buildId)) {
          seenRunBuildIds.add(run.buildId);
          mergedRuns.push(run);
        }
      }
      for (const run of existingRuns) {
        if (!seenRunBuildIds.has(run.buildId)) {
          seenRunBuildIds.add(run.buildId);
          mergedRuns.push(run);
        }
      }

      mergedRuns.sort((a, b) => {
        if (a.deployedAt && b.deployedAt) {
          return new Date(b.deployedAt).getTime() - new Date(a.deployedAt).getTime();
        }
        return (b.buildId || 0) - (a.buildId || 0);
      });

      // Map buildId to 1-based chronological scan position in mergedRuns
      const buildPositionMap = new Map<number, number>();
      mergedRuns.forEach((run, idx) => {
        buildPositionMap.set(run.buildId, idx + 1);
      });

      // 2. Compute true maximum scanned runs count
      const scannedRunsCount = Math.max(
        existing.scannedRunsCount || 0,
        incoming.scannedRunsCount || 0,
        mergedRuns.length
      );

      // 3. Evaluate history completion:
      // True only if incoming explicitly verified end of history (builds < scanDepth),
      // or if existing had already confirmed full end of history (build #1 reached)
      const hasReachedRunHistoryEnd = Boolean(
        incoming.hasReachedRunHistoryEnd || existing.hasReachedRunHistoryEnd
      );

      // 4. Merge environment deployment history per environment
      const mergedEnvHistory: Record<string, EnvironmentDeploymentInfo[]> = {};
      const allEnvKeys = new Set([
        ...Object.keys(existing.environmentHistory || {}),
        ...Object.keys(incoming.environmentHistory || {}),
        ...Object.keys(existing.environmentDeployments || {}),
        ...Object.keys(incoming.environmentDeployments || {})
      ]);

      for (const env of allEnvKeys) {
        const incomingList = incoming.environmentHistory?.[env] || 
          (incoming.environmentDeployments?.[env] ? [incoming.environmentDeployments[env]] : []);
        const existingList = existing.environmentHistory?.[env] || 
          (existing.environmentDeployments?.[env] ? [existing.environmentDeployments[env]] : []);

        const combined: EnvironmentDeploymentInfo[] = [];
        const seenBuildIds = new Set<number>();

        // Incoming runs take priority for latest state of the same build
        for (const dep of incomingList) {
          if (!seenBuildIds.has(dep.buildId)) {
            seenBuildIds.add(dep.buildId);
            combined.push({ ...dep });
          }
        }
        // Older consolidated runs from existing cache preserved
        for (const dep of existingList) {
          if (!seenBuildIds.has(dep.buildId)) {
            seenBuildIds.add(dep.buildId);
            combined.push({ ...dep });
          }
        }

        combined.sort((a, b) => {
          if (a.deployedAt && b.deployedAt) {
            return new Date(b.deployedAt).getTime() - new Date(a.deployedAt).getTime();
          }
          return (b.buildId || 0) - (a.buildId || 0);
        });

        // Update scan position according to chronological position in mergedRuns
        for (const dep of combined) {
          const pos = buildPositionMap.get(dep.buildId);
          if (pos !== undefined) {
            dep.scanPosition = pos;
          }
        }

        if (combined.length > 0) {
          mergedEnvHistory[env] = combined;
        }
      }

      // 5. Active deployment is the most recent deployment per environment
      const mergedEnvDeployments: Record<string, EnvironmentDeploymentInfo> = {};
      for (const env of Object.keys(mergedEnvHistory)) {
        if (mergedEnvHistory[env].length > 0) {
          mergedEnvDeployments[env] = mergedEnvHistory[env][0];
        }
      }

      const mergedStageDeployments: Record<string, StageDeploymentInfo> = {
        ...(existing.deployments || {}),
        ...(incoming.deployments || {})
      };

      return {
        ...existing,
        ...incoming,
        scannedRunsCount,
        hasReachedRunHistoryEnd,
        deployments: mergedStageDeployments,
        environmentDeployments: mergedEnvDeployments,
        environmentHistory: mergedEnvHistory,
        recentRuns: mergedRuns
      };
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

      // Compute dynamic environments list: if user configured target environments, show ONLY those columns
      let envs: string[];
      if (this.profileConfig.environments && this.profileConfig.environments.length > 0) {
        envs = [...this.profileConfig.environments];
      } else {
        envs = [];
        for (const row of activeRows) {
          if (row.environmentDeployments) {
            for (const envKey of Object.keys(row.environmentDeployments)) {
              if (!envs.some(e => e.toLowerCase() === envKey.toLowerCase())) {
                envs.push(envKey);
              }
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
        const cached = this.cachedPipelineRows[pipelineId];
        const targetDepth = Math.max(cached?.scannedRunsCount || 0, this.scanDepth);
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
          throw new Error(errData?.error || errData?.details || 'Failed to refresh pipeline');
        }

        const updatedRow: PipelineMatrixRow = await res.json();
        if (updatedRow && updatedRow.pipelineId) {
          const existingRow = this.cachedPipelineRows[updatedRow.pipelineId];
          this.cachedPipelineRows[updatedRow.pipelineId] = (existingRow && existingRow.scannedRunsCount)
            ? this.mergePipelineMatrixRows(existingRow, updatedRow)
            : updatedRow;
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

    recordRecentPipeline(pipelineId: number) {
      this.recentPipelineIds = [pipelineId, ...this.recentPipelineIds.filter(id => id !== pipelineId)].slice(0, 10);
      try {
        localStorage.setItem('adoRecentPipelineIds', JSON.stringify(this.recentPipelineIds));
      } catch (e) {
        console.warn('Failed to save adoRecentPipelineIds to localStorage', e);
      }
    },

    async selectPipeline(pipelineId: number) {
      this.recordRecentPipeline(pipelineId);
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

    async clearAllAndCache() {
      this.selectedPipelineIds = [];
      this.saveSelectedPipelineIds();
      this.releaseMatrix = null;
      this.cachedPipelineRows = {};
      this.hasPendingChanges = false;
      this.recentPipelineIds = [];

      try {
        localStorage.removeItem('adoSelectedPipelineIds');
        localStorage.removeItem('adoCachedPipelineRows');
        localStorage.removeItem('adoRecentPipelineIds');
      } catch (e) {
        console.warn('Failed to clear localStorage', e);
      }

      try {
        await apiFetch('/api/cache/clear?category=PipelineRuns', { method: 'POST' });
      } catch (e) {
        console.warn('Failed to clear PipelineRuns from server cache', e);
      }
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

    updateProfileConfig(newConfig: PipelineProfileConfig) {
      this.profileConfig = { ...newConfig };
      localStorage.setItem('adoEnvParamName', newConfig.environmentParameterName);
      localStorage.setItem('adoOpParamName', newConfig.operationParameterName);
      localStorage.setItem('adoTargetEnvs', JSON.stringify(newConfig.environments));

      // Unhide any environments that are now explicitly configured in columns
      if (newConfig.environments && newConfig.environments.length > 0) {
        this.hiddenEnvironments = this.hiddenEnvironments.filter(
          h => !newConfig.environments.some(e => e.toLowerCase() === h.toLowerCase())
        );
        try {
          localStorage.setItem('adoHiddenEnvironments', JSON.stringify(this.hiddenEnvironments));
        } catch {}
      }

      // Recompute locally in-memory: instantly updates visible columns with zero network overhead
      this.computeReleaseMatrix();
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

      // Build per-pipeline scan depths preserving deep-scanned histories
      const pipelineScanDepths: Record<number, number> = {};
      for (const id of targetIds) {
        const cached = this.cachedPipelineRows[id];
        if (cached && (cached.scannedRunsCount || 0) > this.scanDepth) {
          pipelineScanDepths[id] = cached.scannedRunsCount!;
        }
      }

      try {
        const url = `/api/devops/pipelines/matrix?organization=${encodeURIComponent(this.organization)}&project=${encodeURIComponent(this.project)}`;
        const res = await apiFetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ 
            pipelineIds: targetIds,
            profileConfig: this.profileConfig,
            scanDepth: this.scanDepth,
            pipelineScanDepths: Object.keys(pipelineScanDepths).length > 0 ? pipelineScanDepths : undefined,
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
            const existingRow = this.cachedPipelineRows[row.pipelineId];
            this.cachedPipelineRows[row.pipelineId] = (existingRow && existingRow.scannedRunsCount)
              ? this.mergePipelineMatrixRows(existingRow, row)
              : row;
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

    canScanMore(row: PipelineMatrixRow): boolean {
      if (row.hasReachedRunHistoryEnd) return false;
      const runsCount = row.scannedRunsCount ?? row.recentRuns?.length ?? this.scanDepth;
      if (runsCount >= 500) return false;
      return true;
    },

    async deepScanPipeline(pipelineId: number, additionalDepth: number = 50) {
      if (!this.organization || !this.project) return;
      if (this.deepScanningPipelineIds.includes(pipelineId)) return;

      // Determine target scan depth
      const currentRow = this.cachedPipelineRows[pipelineId];
      if (currentRow && !this.canScanMore(currentRow)) return;

      const currentRunsCount = currentRow?.scannedRunsCount ?? currentRow?.recentRuns?.length ?? this.scanDepth;
      const targetDepth = Math.min(Math.max(currentRunsCount + additionalDepth, this.scanDepth + additionalDepth), 500);
      if (targetDepth <= currentRunsCount) {
        if (currentRow) {
          currentRow.hasReachedRunHistoryEnd = true;
        }
        return;
      }

      this.deepScanningPipelineIds.push(pipelineId);

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
          const existingRow = this.cachedPipelineRows[updatedRow.pipelineId];
          const merged = (existingRow && existingRow.scannedRunsCount)
            ? this.mergePipelineMatrixRows(existingRow, updatedRow)
            : updatedRow;

          if (existingRow && updatedRow.scannedRunsCount !== undefined && existingRow.scannedRunsCount !== undefined) {
            if (updatedRow.scannedRunsCount <= existingRow.scannedRunsCount) {
              merged.hasReachedRunHistoryEnd = true;
            }
          }
          if (updatedRow.hasReachedRunHistoryEnd) {
            merged.hasReachedRunHistoryEnd = true;
          }

          this.cachedPipelineRows[updatedRow.pipelineId] = merged;
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

    setPriorRunMode(mode: 'distinct' | 'consecutive') {
      this.priorRunMode = mode;
      localStorage.setItem('adoPriorRunMode', mode);
      this.hasPendingChanges = true;
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
    },

    async fetchBackendSettings() {
      try {
        const res = await apiFetch('/api/settings');
        if (res.ok) {
          const data = await res.json();
          if (data.githubOrganization) {
            this.backendGithubOrg = data.githubOrganization;
          }
          if (data.adoOrganization) {
            this.backendAdoOrg = data.adoOrganization;
            if (!this.organization) {
              this.organization = data.adoOrganization;
            }
          }
          if (data.adoProject && !this.project) {
            this.project = data.adoProject;
          }
        }
      } catch {}
    },

    setPipelineColumnWidth(width: number) {
      this.pipelineColumnWidth = width;
      localStorage.setItem('adoPipelineColWidth', width.toString());
    },

    applyEnvironmentPreset(preset: 'default' | 'standard' | 'prod' | 'simple' | 'extended') {
      let envs: string[] = [];
      if (preset === 'default' || preset === 'standard') {
        envs = ['dev', 'uat', 'uat1', 'uat2', 'stg', 'stg1', 'prd', 'prd1'];
      } else if (preset === 'prod') {
        envs = ['prd', 'prd1'];
      } else if (preset === 'simple') {
        envs = ['dev', 'test', 'staging', 'prod'];
      } else if (preset === 'extended') {
        envs = ['dev', 'qa', 'uat', 'uat1', 'uat2', 'stg', 'preprod', 'prd'];
      }
      this.updateProfileConfig({
        environmentParameterName: this.profileConfig.environmentParameterName || localStorage.getItem('adoEnvParamName') || 'Environment',
        operationParameterName: this.profileConfig.operationParameterName || localStorage.getItem('adoOpParamName') || 'Operation',
        environments: envs
      });
    },

    cleanPipelineName(name: string, repositoryName?: string): string {
      if (!name) return '';
      let cleaned = name.trim();

      const candidateOrgs = new Set<string>();

      // 1. ADO Organization from devopsDataStore and local storage
      if (this.organization?.trim()) {
        candidateOrgs.add(this.organization.trim());
      }
      if (this.backendAdoOrg?.trim()) {
        candidateOrgs.add(this.backendAdoOrg.trim());
      }
      const localAdoOrg = localStorage.getItem('adoOrg') || localStorage.getItem('adoOrganization');
      if (localAdoOrg?.trim()) {
        candidateOrgs.add(localAdoOrg.trim());
      }

      // 2. ADO Project from devopsDataStore and local storage
      if (this.project?.trim()) {
        candidateOrgs.add(this.project.trim());
      }
      const localAdoProj = localStorage.getItem('adoProject');
      if (localAdoProj?.trim()) {
        candidateOrgs.add(localAdoProj.trim());
      }

      // 3. GitHub Organization from settingsStore and backend
      try {
        const settingsStore = useSettingsStore();
        if (settingsStore.uiSettings?.githubOrg?.trim()) {
          candidateOrgs.add(settingsStore.uiSettings.githubOrg.trim());
        }
      } catch {}

      if (this.backendGithubOrg?.trim()) {
        candidateOrgs.add(this.backendGithubOrg.trim());
      }

      // 4. GitHub Org directly from localStorage
      try {
        const storedUi = localStorage.getItem('uiSettings');
        if (storedUi) {
          const parsed = JSON.parse(storedUi);
          if (parsed.githubOrg?.trim()) {
            candidateOrgs.add(parsed.githubOrg.trim());
          }
        }
      } catch {}
      const directGithubOrg = localStorage.getItem('githubOrg') || localStorage.getItem('githubOrganization');
      if (directGithubOrg?.trim()) {
        candidateOrgs.add(directGithubOrg.trim());
      }

      // 5. Repository owner from passed repositoryName (e.g. 'org/repo')
      if (repositoryName && repositoryName.includes('/')) {
        const repoOwner = repositoryName.split('/')[0].trim();
        if (repoOwner) candidateOrgs.add(repoOwner);
      }

      // 6. Known repository owners across loaded pipelines and matrix rows
      if (this.pipelines && this.pipelines.length) {
        for (const p of this.pipelines) {
          if (p.repository?.name && p.repository.name.includes('/')) {
            const org = p.repository.name.split('/')[0].trim();
            if (org) candidateOrgs.add(org);
          }
        }
      }
      if (this.releaseMatrix?.rows && this.releaseMatrix.rows.length) {
        for (const r of this.releaseMatrix.rows) {
          if (r.repository?.name && r.repository.name.includes('/')) {
            const org = r.repository.name.split('/')[0].trim();
            if (org) candidateOrgs.add(org);
          }
        }
      }

      // Iteratively strip prefix followed by a dot, as long as there are characters remaining after it
      let strippedAny = true;
      while (strippedAny) {
        strippedAny = false;
        for (const org of candidateOrgs) {
          if (!org) continue;
          const prefix = `${org.toLowerCase()}.`;
          if (cleaned.toLowerCase().startsWith(prefix) && cleaned.length > prefix.length) {
            cleaned = cleaned.substring(prefix.length).trim();
            strippedAny = true;
            break;
          }
        }
      }

      return cleaned;
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
    },
    recentPipelines: (state): PipelineSummary[] => {
      if (!state.recentPipelineIds || state.recentPipelineIds.length === 0) return [];
      const map = new Map(state.pipelines.map(p => [p.id, p]));
      return state.recentPipelineIds
        .map(id => {
          const found = map.get(id);
          if (found) return found;
          const cached = state.cachedPipelineRows[id];
          if (cached) {
            return {
              id,
              name: cached.pipelineName,
              repository: cached.repository
            };
          }
          return null;
        })
        .filter((p): p is PipelineSummary => !!p);
    },
    activePreset: (state): 'default' | 'prod' | 'simple' | 'extended' | null => {
      const current = (state.profileConfig?.environments || []).map(e => e.toLowerCase());
      const isMatch = (target: string[]) => {
        if (current.length !== target.length) return false;
        return target.every((t, i) => current[i] === t);
      };
      if (isMatch(['dev', 'uat', 'uat1', 'uat2', 'stg', 'stg1', 'prd', 'prd1'])) return 'default';
      if (isMatch(['prd', 'prd1'])) return 'prod';
      if (isMatch(['dev', 'test', 'staging', 'prod'])) return 'simple';
      if (isMatch(['dev', 'qa', 'uat', 'uat1', 'uat2', 'stg', 'preprod', 'prd'])) return 'extended';
      return null;
    }
  }
});
