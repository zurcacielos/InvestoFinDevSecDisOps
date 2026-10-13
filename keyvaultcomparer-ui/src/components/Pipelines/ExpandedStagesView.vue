<script setup lang="ts">
import { computed } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineMatrixRow, StageDeploymentInfo } from '../../stores/devopsDataStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { openUrlInNewTab } from '../../utils/urlOpener';

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

const getCommitUrl = (row: PipelineMatrixRow, cell: StageDeploymentInfo | null) => {
  if (!cell?.commitSha) return null;
  const repo = row.repository;
  if (!repo) return null;

  if (repo.type?.toLowerCase() === 'github' || repo.url?.includes('github.com')) {
    const baseUrl = settingsStore.uiSettings.githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && settingsStore.uiSettings.useGithubOrg && settingsStore.uiSettings.githubOrg) {
      repoName = `${settingsStore.uiSettings.githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}/commit/${cell.commitSha}`;
  } else if (repo.type?.toLowerCase() === 'tfsgit' || repo.url?.includes('_git')) {
    if (repo.url) {
      return `${repo.url}/commit/${cell.commitSha}`;
    }
    const org = devopsStore.organization;
    const proj = devopsStore.project;
    return `https://dev.azure.com/${org}/${proj}/_git/${repo.name}/commit/${cell.commitSha}`;
  }
  return null;
};

const getPrUrl = (row: PipelineMatrixRow, cell: StageDeploymentInfo | null) => {
  if (!cell?.prNumber) return null;
  const repo = row.repository;
  if (!repo) return null;

  if (repo.type?.toLowerCase() === 'github' || repo.url?.includes('github.com')) {
    const baseUrl = settingsStore.uiSettings.githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    let repoName = repo.name || '';
    if (!repoName.includes('/') && settingsStore.uiSettings.useGithubOrg && settingsStore.uiSettings.githubOrg) {
      repoName = `${settingsStore.uiSettings.githubOrg}/${repoName}`;
    }
    return `${baseUrl}/${repoName}/pull/${cell.prNumber}`;
  } else if (repo.type?.toLowerCase() === 'tfsgit' || repo.url?.includes('_git')) {
    if (repo.url) {
      return `${repo.url}/pullrequest/${cell.prNumber}`;
    }
    const org = devopsStore.organization;
    const proj = devopsStore.project;
    return `https://dev.azure.com/${org}/${proj}/_git/${repo.name}/pullrequest/${cell.prNumber}`;
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
</script>

<template>
  <div class="flex-1 flex flex-col overflow-hidden">
    <!-- Sub-header Bar -->
    <div class="px-4 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between text-xs text-slate-500 shrink-0">
      <div class="flex items-center gap-2">
        <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-emerald-50 border border-emerald-200 text-emerald-700 font-semibold text-[11px]">
          <span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
          Modo Expandido (DAG Topological Order)
        </span>
        <span class="text-slate-400">&bull;</span>
        <span class="text-slate-600 font-medium">
          {{ rows.length }} Pipelines &times; {{ stages.length }} Stages
        </span>
        <span class="text-slate-400">|</span>
        <span class="text-[11px] text-slate-500 flex items-center gap-1">
          <span>Prerequisites</span>
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 5l7 7m0 0l-7 7m7-7H3" />
          </svg>
          <span>Dependents (Tie-breaker: DEV &rarr; UAT &rarr; UAT1 &rarr; UAT2 &rarr; STG &rarr; PRD)</span>
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
      </div>
    </div>

    <!-- Scrollable Matrix Table -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left border-collapse table-auto">
        <thead>
          <tr class="bg-slate-100/90 border-b border-slate-200 text-slate-600 text-xs uppercase tracking-wider sticky top-0 shadow-xs z-10">
            <!-- Pipeline Name Header -->
            <th class="px-4 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[240px] max-w-[300px]">
              Service / Pipeline
            </th>

            <!-- Repository Header -->
            <th class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[160px]">
              Repository
            </th>

            <!-- DAG Stages Headers -->
            <th 
              v-for="(stage, sIdx) in stages" 
              :key="stage"
              class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 text-center min-w-[190px]"
            >
              <div class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-white border border-slate-200 shadow-2xs text-slate-800 text-xs">
                <span class="w-4 h-4 rounded-full bg-slate-100 text-slate-500 text-[9px] font-mono font-bold flex items-center justify-center border border-slate-200">
                  {{ sIdx + 1 }}
                </span>
                <span class="font-mono truncate max-w-[150px]" :title="stage">{{ stage }}</span>
              </div>
            </th>
          </tr>
        </thead>

        <tbody class="divide-y divide-slate-100 text-xs">
          <tr 
            v-for="row in rows" 
            :key="row.pipelineId"
            class="hover:bg-slate-50/80 transition-colors"
          >
            <!-- Service Header Column -->
            <td 
              @contextmenu="emit('open-context-menu', $event, row, undefined, '')"
              class="px-4 py-3 border-r border-slate-200 bg-white sticky left-0 z-5 shadow-[2px_0_4px_rgba(0,0,0,0.02)] cursor-context-menu"
            >
              <div class="flex items-start justify-between gap-2">
                <div class="min-w-0">
                  <button 
                    @click="openUrlInNewTab(getPipelineDefinitionUrl(row.pipelineId))"
                    class="font-bold text-slate-900 hover:text-blue-600 hover:underline text-left text-xs leading-snug truncate block"
                    title="Open Pipeline Definition in Azure DevOps"
                  >
                    {{ row.pipelineName }}
                  </button>
                  <span class="text-[10px] text-slate-400 font-mono mt-0.5 block">
                    ID: {{ row.pipelineId }}
                  </span>
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
                    title="Copy full pipeline YAML (resolved nested templates) to clipboard"
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

            <!-- Repository Column -->
            <td 
              @contextmenu="emit('open-context-menu', $event, row, undefined, '')"
              class="px-3 py-3 border-r border-slate-200 cursor-context-menu"
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

            <!-- Stage Deployment Cells -->
            <td 
              v-for="stage in stages" 
              :key="stage"
              @contextmenu="emit('open-context-menu', $event, row, row.deployments[stage], stage)"
              class="px-3 py-2.5 border-r border-slate-200 select-none cursor-context-menu"
              :class="row.deployments[stage] ? 'hover:bg-blue-50/50' : 'bg-slate-50/40'"
            >
              <!-- If Stage Executed -->
              <div v-if="row.deployments[stage]" class="flex flex-col gap-1">
                <!-- Top Row: Status Badge & Build Number -->
                <div class="flex items-center justify-between gap-1">
                  <span 
                    class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1"
                    :class="{
                      'bg-emerald-100 text-emerald-800 border border-emerald-200': row.deployments[stage].status === 'succeeded',
                      'bg-rose-100 text-rose-800 border border-rose-200': row.deployments[stage].status === 'failed',
                      'bg-blue-100 text-blue-800 border border-blue-200 animate-pulse': row.deployments[stage].status === 'inProgress',
                      'bg-amber-100 text-amber-800 border border-amber-200': row.deployments[stage].status === 'partiallySucceeded',
                      'bg-slate-100 text-slate-600 border border-slate-200': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(row.deployments[stage].status)
                    }"
                  >
                    <span class="w-1.5 h-1.5 rounded-full" :class="{
                      'bg-emerald-600': row.deployments[stage].status === 'succeeded',
                      'bg-rose-600': row.deployments[stage].status === 'failed',
                      'bg-blue-600': row.deployments[stage].status === 'inProgress',
                      'bg-amber-600': row.deployments[stage].status === 'partiallySucceeded',
                      'bg-slate-400': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(row.deployments[stage].status)
                    }"></span>
                    {{ row.deployments[stage].status }}
                  </span>

                  <!-- Build Number Link -->
                  <button 
                    v-if="row.deployments[stage].buildUrl"
                    @click.stop="openUrlInNewTab(row.deployments[stage].buildUrl!)"
                    class="text-[11px] font-mono text-blue-600 hover:underline hover:text-blue-800 truncate"
                    title="Open Run in Azure DevOps"
                  >
                    #{{ row.deployments[stage].buildNumber }}
                  </button>
                </div>

                <!-- Middle Row: Branch & Commit SHA -->
                <div class="flex items-center gap-1.5 flex-wrap">
                  <!-- Branch -->
                  <span 
                    v-if="row.deployments[stage].branch"
                    class="inline-flex items-center gap-1 text-[11px] font-medium text-slate-700 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200 max-w-[120px] truncate"
                    :title="row.deployments[stage].branch"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
                    </svg>
                    <span class="truncate">{{ row.deployments[stage].branch }}</span>
                  </span>

                  <!-- Commit SHA -->
                  <button 
                    v-if="row.deployments[stage].commitSha"
                    @click.stop="getCommitUrl(row, row.deployments[stage]) ? openUrlInNewTab(getCommitUrl(row, row.deployments[stage])!) : null"
                    class="text-[10px] font-mono px-1.5 py-0.5 rounded transition-colors"
                    :class="getCommitUrl(row, row.deployments[stage]) ? 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100 hover:underline' : 'bg-slate-100 text-slate-600'"
                    :title="`Commit: ${row.deployments[stage].commitSha}`"
                  >
                    {{ row.deployments[stage].commitSha?.substring(0, 7) }}
                  </button>

                  <!-- PR Badge if present -->
                  <button 
                    v-if="row.deployments[stage].prNumber"
                    @click.stop="getPrUrl(row, row.deployments[stage]) ? openUrlInNewTab(getPrUrl(row, row.deployments[stage])!) : null"
                    class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-violet-100 text-violet-800 hover:bg-violet-200 transition-colors"
                    :title="`Pull Request #${row.deployments[stage].prNumber}`"
                  >
                    PR #{{ row.deployments[stage].prNumber }}
                  </button>
                </div>

                <!-- Bottom Row: Time ago & Author -->
                <div class="flex items-center justify-between text-[10px] text-slate-400 mt-0.5">
                  <span :title="row.deployments[stage].deployedAt">
                    {{ formatTimeAgo(row.deployments[stage].deployedAt) }}
                  </span>
                  <span v-if="row.deployments[stage].author" class="truncate max-w-[90px]" :title="row.deployments[stage].author">
                    {{ row.deployments[stage].author }}
                  </span>
                </div>
              </div>

              <!-- Not Executed -->
              <div v-else class="h-full flex items-center justify-center py-2 text-slate-300">
                <span class="text-xs">&mdash;</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
