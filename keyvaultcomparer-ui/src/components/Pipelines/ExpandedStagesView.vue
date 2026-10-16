<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineMatrixRow, StageDeploymentInfo, PipelineRunInfo } from '../../stores/devopsDataStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { openUrlInNewTab } from '../../utils/urlOpener';
import PipelineAuthorBadge from './PipelineAuthorBadge.vue';

const devopsStore = useDevopsDataStore();
const settingsStore = useSettingsStore();

const emit = defineEmits<{
  (e: 'open-context-menu', event: MouseEvent, row: PipelineMatrixRow, cell: any, stageName: string): void;
  (e: 'open-all-yamls', row: PipelineMatrixRow): void;
}>();

// Stages ordered strictly by DAG topology (prerequisites first), with environment rank tie-breaking
const stages = computed(() => {
  return devopsStore.releaseMatrix?.topoSortedStages || [];
});

const rows = computed(() => {
  return devopsStore.releaseMatrix?.rows || [];
});

// Expansion state: track which pipelines have their runs expanded
const expandedPipelineIds = ref<Set<number>>(new Set());

// Expand all pipelines by default when rows load
watch(
  () => rows.value,
  (newRows) => {
    if (newRows.length > 0 && expandedPipelineIds.value.size === 0) {
      expandedPipelineIds.value = new Set(newRows.map(r => r.pipelineId));
    }
  },
  { immediate: true }
);

const toggleExpand = (pipelineId: number) => {
  const set = new Set(expandedPipelineIds.value);
  if (set.has(pipelineId)) {
    set.delete(pipelineId);
  } else {
    set.add(pipelineId);
  }
  expandedPipelineIds.value = set;
};

const isAllExpanded = computed(() => {
  return rows.value.length > 0 && rows.value.every(r => expandedPipelineIds.value.has(r.pipelineId));
});

const expandAll = () => {
  expandedPipelineIds.value = new Set(rows.value.map(r => r.pipelineId));
};

const collapseAll = () => {
  expandedPipelineIds.value = new Set();
};

const totalRunsCount = computed(() => {
  return rows.value.reduce((acc, row) => {
    return acc + (row.recentRuns?.length || (Object.keys(row.deployments).length > 0 ? 1 : 0));
  }, 0);
});

const getPipelineDefinitionUrl = (pipelineId: number) => {
  const org = devopsStore.organization;
  const proj = devopsStore.project;
  return `https://dev.azure.com/${org}/${proj}/_build?definitionId=${pipelineId}`;
};

const getRepoUrl = (row: PipelineMatrixRow) => {
  const repo = row.repository;
  if (!repo) return null;
  if (repo.url) return repo.url;
  if (repo.type?.toLowerCase() === 'github') {
    const baseUrl = settingsStore.uiSettings.githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && settingsStore.uiSettings.useGithubOrg && settingsStore.uiSettings.githubOrg) {
      repoName = `${settingsStore.uiSettings.githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}`;
  }
  return null;
};

const getCommitUrl = (row: PipelineMatrixRow, commitSha?: string) => {
  if (!commitSha) return null;
  const repo = row.repository;
  if (!repo) return null;

  if (repo.type?.toLowerCase() === 'github' || repo.url?.includes('github.com')) {
    const baseUrl = settingsStore.uiSettings.githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && settingsStore.uiSettings.useGithubOrg && settingsStore.uiSettings.githubOrg) {
      repoName = `${settingsStore.uiSettings.githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}/commit/${commitSha}`;
  } else if (repo.type?.toLowerCase() === 'tfsgit' || repo.url?.includes('_git')) {
    if (repo.url) {
      return `${repo.url}/commit/${commitSha}`;
    }
    const org = devopsStore.organization;
    const proj = devopsStore.project;
    return `https://dev.azure.com/${org}/${proj}/_git/${repo.name}/commit/${commitSha}`;
  }
  return null;
};

const getPrUrl = (row: PipelineMatrixRow, prNumber?: string) => {
  if (!prNumber) return null;
  const repo = row.repository;
  if (!repo) return null;

  if (repo.type?.toLowerCase() === 'github' || repo.url?.includes('github.com')) {
    const baseUrl = settingsStore.uiSettings.githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && settingsStore.uiSettings.useGithubOrg && settingsStore.uiSettings.githubOrg) {
      repoName = `${settingsStore.uiSettings.githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}/pull/${prNumber}`;
  } else if (repo.type?.toLowerCase() === 'tfsgit' || repo.url?.includes('_git')) {
    if (repo.url) {
      return `${repo.url}/pullrequest/${prNumber}`;
    }
    const org = devopsStore.organization;
    const proj = devopsStore.project;
    return `https://dev.azure.com/${org}/${proj}/_git/${repo.name}/pullrequest/${prNumber}`;
  }
  return null;
};

const formatTimeAgo = (iso?: string) => {
  if (!iso) return '';
  const date = new Date(iso);
  const diffSec = Math.floor((Date.now() - date.getTime()) / 1000);
  if (diffSec < 60) return 'just now';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
};

const formatDuration = (start?: string, finish?: string) => {
  if (!start || !finish) return null;
  const s = new Date(start).getTime();
  const f = new Date(finish).getTime();
  if (isNaN(s) || isNaN(f) || f < s) return null;
  const diffSec = Math.round((f - s) / 1000);
  if (diffSec < 60) return `${diffSec}s`;
  const m = Math.floor(diffSec / 60);
  const remSec = diffSec % 60;
  if (m < 60) return remSec > 0 ? `${m}m ${remSec}s` : `${m}m`;
  const h = Math.floor(m / 60);
  const remM = m % 60;
  return `${h}h ${remM}m`;
};

const toDeploymentInfo = (run: PipelineRunInfo, stageName?: string): StageDeploymentInfo => {
  const stage = stageName ? run.stages[stageName] : undefined;
  return {
    stageName: stageName || '',
    status: stage?.status || run.overallStatus,
    buildId: run.buildId,
    buildNumber: run.buildNumber,
    branch: run.branch,
    commitSha: run.commitSha,
    author: run.author,
    authorImageUrl: run.authorImageUrl,
    triggerReason: run.triggerReason,
    isAutomated: run.isAutomated,
    deployedAt: stage?.finishTime || stage?.startTime || run.deployedAt,
    buildUrl: run.buildUrl,
    prNumber: run.prNumber,
  };
};
</script>

<template>
  <div class="flex-1 flex flex-col overflow-hidden">
    <!-- Sub-header Bar -->
    <div class="px-4 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between text-xs text-slate-500 shrink-0">
      <div class="flex items-center gap-2">
        <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-emerald-50 border border-emerald-200 text-emerald-700 font-semibold text-[11px]">
          <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
          Expanded Stages (Hierarchical Run DAG)
        </span>
        <span class="text-slate-400">&bull;</span>
        <span class="text-slate-600 font-medium">
          {{ rows.length }} Pipelines &times; {{ totalRunsCount }} Runs &times; {{ stages.length }} Stages
        </span>
        <span class="text-slate-400">|</span>
        
        <!-- Expand / Collapse All Toggle -->
        <button 
          @click="isAllExpanded ? collapseAll() : expandAll()"
          class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-white hover:bg-slate-100 border border-slate-200 text-slate-700 font-medium text-[11px] shadow-2xs transition-colors"
          :title="isAllExpanded ? 'Collapse all pipeline runs' : 'Expand all pipeline runs'"
        >
          <svg 
            xmlns="http://www.w3.org/2000/svg" 
            class="h-3 w-3 text-slate-500 transition-transform duration-150"
            :class="{ 'rotate-90': isAllExpanded }"
            fill="none" 
            viewBox="0 0 24 24" 
            stroke="currentColor"
          >
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 5l7 7-7 7" />
          </svg>
          <span>{{ isAllExpanded ? 'Collapse All' : 'Expand All' }}</span>
        </button>

        <span class="text-slate-400">|</span>
        <span class="text-[11px] text-slate-500 flex items-center gap-1">
          <span>Prerequisites</span>
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3" />
          </svg>
          <span>Dependents</span>
        </span>
      </div>

      <div class="flex items-center gap-3 text-[11px]">
        <span class="inline-flex items-center gap-1 text-emerald-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-emerald-500"></span> Succeeded
        </span>
        <span class="inline-flex items-center gap-1 text-rose-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-rose-500"></span> Failed
        </span>
        <span class="inline-flex items-center gap-1 text-blue-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-blue-500 animate-pulse"></span> Running
        </span>
        <span class="inline-flex items-center gap-1 text-amber-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-amber-500"></span> Partial
        </span>
      </div>
    </div>

    <!-- Scrollable Matrix Table -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left border-collapse table-auto">
        <thead>
          <tr class="bg-slate-100/90 border-b border-slate-200 text-slate-600 text-xs uppercase tracking-wider sticky top-0 shadow-xs z-10">
            <!-- Pipeline / Run Header -->
            <th class="px-4 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[280px] max-w-[340px]">
              Pipeline / Run Execution
            </th>

            <!-- Repository & Metadata Header -->
            <th class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[180px] max-w-[240px]">
              Repo &amp; Git Info
            </th>

            <!-- DAG Stages Headers -->
            <th 
              v-for="(stage, sIdx) in stages" 
              :key="stage"
              class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 text-center min-w-[190px] max-w-[50ch] align-top"
            >
              <div class="inline-flex items-start gap-1.5 px-2.5 py-1 rounded-md bg-white border border-slate-200 shadow-2xs text-slate-800 text-xs max-w-[50ch] text-left">
                <span class="w-4 h-4 rounded-full bg-slate-100 text-slate-500 text-[9px] font-mono font-bold flex items-center justify-center border border-slate-200 shrink-0 mt-0.5">
                  {{ sIdx + 1 }}
                </span>
                <span class="font-mono break-all whitespace-normal leading-tight" :title="stage">{{ stage }}</span>
              </div>
            </th>
          </tr>
        </thead>

        <tbody class="divide-y divide-slate-200 text-xs">
          <!-- Outer loop: Pipeline Row & Runs -->
          <template v-for="row in rows" :key="row.pipelineId">
            <!-- Main Pipeline Header Row -->
            <tr 
              class="bg-slate-50 hover:bg-slate-100/80 transition-colors border-t-2 border-slate-200"
            >
              <!-- Service Name & Actions (Col 1) -->
              <td 
                @contextmenu="emit('open-context-menu', $event, row, undefined, '')"
                class="px-3 py-2.5 border-r border-slate-200 bg-slate-50 sticky left-0 z-5 shadow-[2px_0_4px_rgba(0,0,0,0.02)] cursor-context-menu"
              >
                <div class="flex items-center justify-between gap-2">
                  <div class="flex items-center gap-1.5 min-w-0">
                    <!-- Expand/Collapse Chevron -->
                    <button 
                      @click.stop="toggleExpand(row.pipelineId)"
                      class="p-1 rounded text-slate-500 hover:text-slate-800 hover:bg-slate-200/80 transition-colors shrink-0"
                      :title="expandedPipelineIds.has(row.pipelineId) ? 'Collapse runs' : 'Expand runs'"
                    >
                      <svg 
                        xmlns="http://www.w3.org/2000/svg" 
                        class="h-3.5 w-3.5 text-slate-600 transition-transform duration-150"
                        :class="{ 'rotate-90': expandedPipelineIds.has(row.pipelineId) }"
                        fill="none" 
                        viewBox="0 0 24 24" 
                        stroke="currentColor"
                      >
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M9 5l7 7-7 7" />
                      </svg>
                    </button>

                    <div class="min-w-0">
                      <div class="flex items-center gap-1.5">
                        <button 
                          @click.stop="openUrlInNewTab(getPipelineDefinitionUrl(row.pipelineId))"
                          class="font-bold text-slate-900 hover:text-blue-600 hover:underline text-left text-xs leading-snug truncate"
                          :title="`Open Pipeline ${row.pipelineName} in Azure DevOps`"
                        >
                          {{ row.pipelineName }}
                        </button>
                        
                        <!-- Runs Count Badge -->
                        <span 
                          v-if="row.recentRuns && row.recentRuns.length > 0"
                          class="px-1.5 py-0.2 rounded text-[10px] font-semibold bg-blue-50 text-blue-700 border border-blue-200 shrink-0"
                        >
                          {{ row.recentRuns.length }} {{ row.recentRuns.length === 1 ? 'run' : 'runs' }}
                        </span>
                      </div>
                    </div>
                  </div>

                  <!-- Action Buttons -->
                  <div class="flex items-center gap-1 shrink-0">
                    <!-- Open all GitHub Yaml -->
                    <button 
                      @click.stop="emit('open-all-yamls', row)"
                      class="p-1 rounded text-slate-400 hover:text-violet-700 hover:bg-violet-50 transition-colors"
                      title="Open all GitHub Yaml (Main pipeline &amp; templates)"
                    >
                      <svg class="h-3.5 w-3.5 text-slate-700" viewBox="0 0 24 24" fill="currentColor">
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
                      </svg>
                    </button>

                    <!-- Copy Full YAML -->
                    <button 
                      @click.stop="devopsStore.copyPipelineYamlToClipboard(row.pipelineId, row.pipelineName)"
                      class="p-1 rounded text-slate-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"
                      title="Copy full pipeline YAML to clipboard"
                      :disabled="devopsStore.copyingYamlPipelineId === row.pipelineId"
                    >
                      <svg v-if="devopsStore.copyingYamlPipelineId === row.pipelineId" class="animate-spin h-3.5 w-3.5 text-blue-500" fill="none" viewBox="0 0 24 24">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                      </svg>
                      <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                      </svg>
                    </button>

                    <!-- View YAML Modal -->
                    <button 
                      @click.stop="devopsStore.openYamlModal(row.pipelineId, row.pipelineName)"
                      class="p-1 rounded text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 transition-colors"
                      title="View resolved pipeline YAML"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                      </svg>
                    </button>
                  </div>
                </div>
              </td>

              <!-- Repository (Col 2) -->
              <td 
                @contextmenu="emit('open-context-menu', $event, row, undefined, '')"
                class="px-3 py-2.5 border-r border-slate-200 cursor-context-menu"
              >
                <div v-if="row.repository" class="flex flex-col min-w-0">
                  <div class="flex items-center gap-1.5">
                    <svg v-if="row.repository.type?.toLowerCase() === 'github'" class="h-3.5 w-3.5 text-slate-700 shrink-0" viewBox="0 0 24 24" fill="currentColor">
                      <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
                    </svg>
                    <svg v-else class="h-3.5 w-3.5 text-blue-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                    </svg>

                    <button 
                      v-if="getRepoUrl(row)"
                      @click="openUrlInNewTab(getRepoUrl(row)!)"
                      class="text-xs font-medium text-slate-700 hover:text-blue-600 hover:underline truncate"
                      :title="row.repository.name"
                    >
                      {{ row.repository.name }}
                    </button>
                    <span v-else class="text-xs text-slate-600 truncate">
                      {{ row.repository.name }}
                    </span>
                  </div>
                  <span v-if="row.repository.defaultBranch" class="text-[10px] text-slate-400 font-mono mt-0.5">
                    {{ row.repository.defaultBranch.replace('refs/heads/', '') }}
                  </span>
                </div>
                <span v-else class="text-slate-300">-</span>
              </td>

              <!-- Overview summary per stage (Col 3..N) -->
              <td 
                v-for="stage in stages" 
                :key="stage"
                @contextmenu="emit('open-context-menu', $event, row, row.deployments[stage], stage)"
                class="px-3 py-2 border-r border-slate-200 select-none cursor-context-menu min-w-[190px] max-w-[50ch] text-center"
              >
                <!-- Latest Summary Badge across runs -->
                <div v-if="row.deployments[stage]" class="inline-flex items-center gap-1.5 text-[10px] font-medium text-slate-600">
                  <span class="w-2 h-2 rounded-full shrink-0" :class="{
                    'bg-emerald-500': row.deployments[stage].status === 'succeeded',
                    'bg-rose-500': row.deployments[stage].status === 'failed',
                    'bg-blue-500 animate-pulse': row.deployments[stage].status === 'inProgress',
                    'bg-amber-500': row.deployments[stage].status === 'partiallySucceeded',
                    'bg-slate-300': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(row.deployments[stage].status)
                  }"></span>
                  <span class="capitalize text-slate-700 text-[10px]">{{ row.deployments[stage].status }}</span>
                  <span v-if="row.deployments[stage].buildNumber" class="text-slate-400 font-mono text-[9px]">
                    (#{{ row.deployments[stage].buildNumber }})
                  </span>
                </div>
                <div v-else class="text-slate-300 text-xs">
                  &mdash;
                </div>
              </td>
            </tr>

            <!-- Expanded Sub-rows: 1 Row per Run -->
            <template v-if="expandedPipelineIds.has(row.pipelineId)">
              <!-- If recent runs are populated -->
              <tr 
                v-for="(run, rIdx) in (row.recentRuns && row.recentRuns.length > 0 ? row.recentRuns : [])" 
                :key="`${row.pipelineId}-run-${run.buildId || rIdx}`"
                class="hover:bg-blue-50/30 transition-colors border-b border-slate-100 bg-white"
              >
                <!-- Sticky Col 1: Factored Run Header (Run #, status, author, time) -->
                <td 
                  @contextmenu="emit('open-context-menu', $event, row, toDeploymentInfo(run), '')"
                  class="pl-7 pr-3 py-2 border-r border-slate-200 bg-white sticky left-0 z-5 shadow-[2px_0_4px_rgba(0,0,0,0.02)] cursor-context-menu"
                >
                  <div class="flex items-center justify-between gap-2">
                    <!-- Left: Run # and overall status -->
                    <div class="flex items-center gap-2 min-w-0">
                      <span class="text-slate-300 font-mono text-xs select-none">└</span>

                      <button 
                        v-if="run.buildUrl"
                        @click.stop="openUrlInNewTab(run.buildUrl)"
                        class="font-mono font-bold text-xs text-blue-600 hover:text-blue-800 hover:underline truncate"
                        :title="`Open Run #${run.buildNumber} in Azure DevOps`"
                      >
                        #{{ run.buildNumber }}
                      </button>
                      <span v-else class="font-mono font-bold text-xs text-slate-800 truncate">
                        #{{ run.buildNumber }}
                      </span>

                      <!-- Overall Status Badge -->
                      <span 
                        class="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider flex items-center gap-1 shrink-0"
                        :class="{
                          'bg-emerald-100 text-emerald-800 border border-emerald-200': run.overallStatus === 'succeeded',
                          'bg-rose-100 text-rose-800 border border-rose-200': run.overallStatus === 'failed',
                          'bg-blue-100 text-blue-800 border border-blue-200 animate-pulse': run.overallStatus === 'inProgress',
                          'bg-amber-100 text-amber-800 border border-amber-200': run.overallStatus === 'partiallySucceeded',
                          'bg-slate-100 text-slate-600 border border-slate-200': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(run.overallStatus)
                        }"
                      >
                        <span class="w-1.5 h-1.5 rounded-full" :class="{
                          'bg-emerald-600': run.overallStatus === 'succeeded',
                          'bg-rose-600': run.overallStatus === 'failed',
                          'bg-blue-600': run.overallStatus === 'inProgress',
                          'bg-amber-600': run.overallStatus === 'partiallySucceeded',
                          'bg-slate-400': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(run.overallStatus)
                        }"></span>
                        {{ run.overallStatus }}
                      </span>
                    </div>

                    <!-- Right: Factored Author & Time Ago -->
                    <div class="flex items-center gap-2 shrink-0">
                      <span :title="run.deployedAt" class="text-[9px] text-slate-400 font-mono">
                        {{ formatTimeAgo(run.deployedAt) }}
                      </span>
                      <PipelineAuthorBadge 
                        :author="run.author"
                        :author-image-url="run.authorImageUrl"
                        :is-automated="run.isAutomated"
                        :trigger-reason="run.triggerReason"
                      />
                    </div>
                  </div>
                </td>

                <!-- Col 2: Factored Git & PR Metadata -->
                <td 
                  @contextmenu="emit('open-context-menu', $event, row, toDeploymentInfo(run), '')"
                  class="px-3 py-2 border-r border-slate-200 cursor-context-menu"
                >
                  <div class="flex items-center gap-1.5 flex-wrap">
                    <!-- Branch -->
                    <span 
                      v-if="run.branch"
                      class="inline-flex items-center gap-1 text-[11px] font-medium text-slate-700 bg-slate-100 hover:bg-slate-200/70 px-1.5 py-0.5 rounded border border-slate-200 max-w-[120px] truncate transition-colors cursor-default"
                      :title="`Branch: ${run.branch}`"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-500 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <line x1="6" y1="3" x2="6" y2="15"></line>
                        <circle cx="18" cy="6" r="3"></circle>
                        <circle cx="6" cy="18" r="3"></circle>
                        <path d="M18 9a9 9 0 0 1-9 9"></path>
                      </svg>
                      <span class="truncate">{{ run.branch.replace('refs/heads/', '') }}</span>
                    </span>

                    <!-- Commit SHA -->
                    <button 
                      v-if="run.commitSha"
                      @click.stop="getCommitUrl(row, run.commitSha) ? openUrlInNewTab(getCommitUrl(row, run.commitSha)!) : null"
                      class="text-[10px] font-mono px-1.5 py-0.5 rounded transition-colors"
                      :class="getCommitUrl(row, run.commitSha) ? 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100 hover:underline cursor-pointer' : 'bg-slate-100 text-slate-600'"
                      :title="`Commit: ${run.commitSha}`"
                    >
                      {{ run.commitSha.substring(0, 7) }}
                    </button>

                    <!-- PR Badge -->
                    <button 
                      v-if="run.prNumber"
                      @click.stop="getPrUrl(row, run.prNumber) ? openUrlInNewTab(getPrUrl(row, run.prNumber)!) : null"
                      class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-violet-100 text-violet-800 hover:bg-violet-200 transition-colors cursor-pointer"
                      :title="`Pull Request #${run.prNumber}`"
                    >
                      PR #{{ run.prNumber }}
                    </button>
                  </div>
                </td>

                <!-- Col 3..N: Stage Status & Duration per Run -->
                <td 
                  v-for="stage in stages" 
                  :key="stage"
                  @contextmenu="emit('open-context-menu', $event, row, toDeploymentInfo(run, stage), stage)"
                  class="px-3 py-2 border-r border-slate-200 select-none cursor-context-menu min-w-[190px] max-w-[50ch] text-center"
                  :class="run.stages[stage] ? 'bg-white' : 'bg-slate-50/20'"
                >
                  <!-- If Stage executed in this specific run -->
                  <div v-if="run.stages[stage]" class="flex flex-col items-center justify-center gap-1">
                    <span 
                      class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider inline-flex items-center gap-1 shadow-2xs"
                      :class="{
                        'bg-emerald-100 text-emerald-800 border border-emerald-200': run.stages[stage].status === 'succeeded',
                        'bg-rose-100 text-rose-800 border border-rose-200': run.stages[stage].status === 'failed',
                        'bg-blue-100 text-blue-800 border border-blue-200 animate-pulse': run.stages[stage].status === 'inProgress',
                        'bg-amber-100 text-amber-800 border border-amber-200': run.stages[stage].status === 'partiallySucceeded',
                        'bg-slate-100 text-slate-600 border border-slate-200': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(run.stages[stage].status)
                      }"
                    >
                      <span class="w-1.5 h-1.5 rounded-full" :class="{
                        'bg-emerald-600': run.stages[stage].status === 'succeeded',
                        'bg-rose-600': run.stages[stage].status === 'failed',
                        'bg-blue-600': run.stages[stage].status === 'inProgress',
                        'bg-amber-600': run.stages[stage].status === 'partiallySucceeded',
                        'bg-slate-400': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(run.stages[stage].status)
                      }"></span>
                      {{ run.stages[stage].status }}
                    </span>

                    <!-- Duration if start & finish times exist -->
                    <span 
                      v-if="formatDuration(run.stages[stage].startTime, run.stages[stage].finishTime)" 
                      class="text-[9px] text-slate-400 font-mono flex items-center gap-0.5"
                      :title="`Started: ${run.stages[stage].startTime || 'N/A'}\nFinished: ${run.stages[stage].finishTime || 'N/A'}`"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <circle cx="12" cy="12" r="9" stroke-width="2" />
                        <path stroke-width="2" d="M12 7v5l3 2" />
                      </svg>
                      {{ formatDuration(run.stages[stage].startTime, run.stages[stage].finishTime) }}
                    </span>
                  </div>

                  <!-- Not executed in this run -->
                  <div v-else class="h-full flex items-center justify-center text-slate-300">
                    <span class="text-xs select-none">&mdash;</span>
                  </div>
                </td>
              </tr>

              <!-- Fallback row if no recentRuns available -->
              <tr 
                v-if="!row.recentRuns || row.recentRuns.length === 0" 
                class="hover:bg-slate-50/80 transition-colors bg-white"
              >
                <td class="pl-7 pr-3 py-2 border-r border-slate-200 bg-white sticky left-0 z-5 text-slate-400 italic text-[11px]">
                  No historical run data loaded
                </td>
                <td class="px-3 py-2 border-r border-slate-200 text-slate-300">-</td>
                <td 
                  v-for="stage in stages" 
                  :key="stage"
                  class="px-3 py-2 border-r border-slate-200 text-center text-slate-300"
                >
                  &mdash;
                </td>
              </tr>
            </template>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>
