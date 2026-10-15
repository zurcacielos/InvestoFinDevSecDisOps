<script setup lang="ts">
import { computed } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineMatrixRow, EnvironmentDeploymentInfo } from '../../stores/devopsDataStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { openUrlInNewTab } from '../../utils/urlOpener';
import PipelineAuthorBadge from './PipelineAuthorBadge.vue';

const devopsStore = useDevopsDataStore();
const settingsStore = useSettingsStore();

const emit = defineEmits<{
  (e: 'open-context-menu', event: MouseEvent, row: PipelineMatrixRow, cell: any, envName: string): void;
  (e: 'open-all-yamls', row: PipelineMatrixRow): void;
}>();

const environments = computed(() => {
  return devopsStore.releaseMatrix?.environments || [];
});

const rows = computed(() => {
  return devopsStore.releaseMatrix?.rows || [];
});

const getDeploymentsForEnv = (row: PipelineMatrixRow, env: string): EnvironmentDeploymentInfo[] => {
  const history = row.environmentHistory?.[env];
  if (history && history.length > 0) return history;
  const current = row.environmentDeployments?.[env];
  return current ? [current] : [];
};

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

const getCommitUrl = (row: PipelineMatrixRow, cell: EnvironmentDeploymentInfo | null) => {
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

const getPrUrl = (row: PipelineMatrixRow, cell: EnvironmentDeploymentInfo | null) => {
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

// Check promotion lag relative to the previous environment column
const getLagStatus = (row: PipelineMatrixRow, envIndex: number) => {
  if (envIndex === 0) return null;
  const currentEnv = environments.value[envIndex];
  const prevEnv = environments.value[envIndex - 1];

  const currentDep = row.environmentDeployments[currentEnv];
  const prevDep = row.environmentDeployments[prevEnv];

  if (!currentDep || !prevDep) return null;
  if (!currentDep.commitSha || !prevDep.commitSha) return null;

  if (currentDep.commitSha === prevDep.commitSha) {
    return { isSynced: true, label: 'In sync with upstream' };
  } else {
    return { isSynced: false, label: 'Lagging behind upstream' };
  }
};
</script>

<template>
  <div class="flex-1 flex flex-col overflow-hidden">
    <!-- Sub-header Bar -->
    <div class="px-4 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between text-xs text-slate-500 shrink-0 flex-wrap gap-2">
      <div class="flex items-center gap-2 flex-wrap">
        <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-indigo-50 border border-indigo-200 text-indigo-700 font-semibold text-[11px]">
          <span class="w-1.5 h-1.5 rounded-full bg-indigo-500"></span>
          Environment Matrix
        </span>
        <span class="text-slate-400">&bull;</span>
        <span class="text-slate-600 font-medium">
          {{ rows.length }} Pipelines &times; {{ environments.length }} Environments
        </span>
        <span class="text-slate-400">|</span>

        <!-- Show Rollback (N-1) Toggle -->
        <label 
          class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md bg-white border border-slate-300 shadow-2xs text-[11px] font-semibold text-slate-700 cursor-pointer hover:bg-slate-50 select-none transition-colors"
          title="Display the previous installation (N-1) in each environment as a rollback target"
        >
          <input 
            type="checkbox" 
            v-model="devopsStore.showRollbackTarget"
            @change="devopsStore.setShowRollbackTarget(devopsStore.showRollbackTarget)"
            class="rounded text-amber-600 focus:ring-amber-500 h-3.5 w-3.5"
          />
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
          </svg>
          <span>Show Rollback Target (N-1)</span>
        </label>
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

            <!-- Environment Headers -->
            <th 
              v-for="env in environments" 
              :key="env"
              class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 text-center min-w-[220px]"
            >
              <div class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-white border border-slate-200 shadow-2xs text-slate-800 font-mono font-bold text-xs uppercase">
                <span class="w-2 h-2 rounded-full bg-blue-500"></span>
                {{ env }}
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
              class="px-4 py-3 border-r border-slate-200 bg-white sticky left-0 z-5 shadow-[2px_0_4px_rgba(0,0,0,0.02)] cursor-context-menu align-top"
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
              class="px-3 py-3 border-r border-slate-200 cursor-context-menu align-top"
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

            <!-- Environment Deployment Cells -->
            <td 
              v-for="(env, envIdx) in environments" 
              :key="env"
              @contextmenu="emit('open-context-menu', $event, row, getDeploymentsForEnv(row, env)[0] || null, env)"
              class="px-3 py-2.5 border-r border-slate-200 select-none cursor-context-menu min-w-[220px] align-top"
              :class="getDeploymentsForEnv(row, env).length ? 'hover:bg-blue-50/40 bg-white' : 'bg-slate-50/40'"
            >
              <template v-for="depList in [getDeploymentsForEnv(row, env)]">
                <!-- If Environment Deployment Exists -->
                <div v-if="depList.length > 0 && depList[0]" class="flex flex-col gap-1.5">
                  
                  <!-- 1. Active / Current Deployment (N) -->
                  <div class="flex flex-col gap-1">
                    <!-- Top Row: Status Badge & Build Number -->
                    <div class="flex items-center justify-between gap-1">
                      <span 
                        class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1"
                        :class="{
                          'bg-emerald-100 text-emerald-800 border border-emerald-200': depList[0].status === 'succeeded',
                          'bg-rose-100 text-rose-800 border border-rose-200': depList[0].status === 'failed',
                          'bg-blue-100 text-blue-800 border border-blue-200 animate-pulse': depList[0].status === 'inProgress',
                          'bg-amber-100 text-amber-800 border border-amber-200': depList[0].status === 'partiallySucceeded',
                          'bg-slate-100 text-slate-600 border border-slate-200': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(depList[0].status)
                        }"
                      >
                        <span class="w-1.5 h-1.5 rounded-full" :class="{
                          'bg-emerald-600': depList[0].status === 'succeeded',
                          'bg-rose-600': depList[0].status === 'failed',
                          'bg-blue-600': depList[0].status === 'inProgress',
                          'bg-amber-600': depList[0].status === 'partiallySucceeded',
                          'bg-slate-400': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(depList[0].status)
                        }"></span>
                        {{ depList[0].status }}
                      </span>

                      <!-- Build Number Link -->
                      <button 
                        v-if="depList[0].buildUrl"
                        @click.stop="openUrlInNewTab(depList[0].buildUrl!)"
                        class="text-[11px] font-mono text-blue-600 hover:underline hover:text-blue-800 truncate"
                        :title="`Open Run #${depList[0].buildNumber} in Azure DevOps`"
                      >
                        #{{ depList[0].buildNumber }}
                      </button>
                    </div>

                    <!-- Middle Row: Version / Operation / Branch -->
                    <div class="flex items-center gap-1.5 flex-wrap">
                      <!-- Version / Tag if present -->
                      <span 
                        v-if="depList[0].version"
                        class="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-violet-50 text-violet-700 border border-violet-200 truncate"
                        :title="`Version/Tag: ${depList[0].version}`"
                      >
                        v{{ depList[0].version }}
                      </span>

                      <!-- Operation badge if present (e.g. release) -->
                      <span 
                        v-if="depList[0].operation"
                        class="px-1.5 py-0.5 rounded text-[9px] font-medium uppercase tracking-wider bg-slate-100 text-slate-600 border border-slate-200"
                      >
                        {{ depList[0].operation }}
                      </span>

                      <!-- Branch -->
                      <span 
                        v-if="depList[0].branch"
                        class="inline-flex items-center gap-1 text-[11px] font-medium text-slate-700 bg-slate-100 hover:bg-slate-200/70 px-1.5 py-0.5 rounded border border-slate-200 max-w-[130px] truncate transition-colors cursor-default"
                        :title="`Branch: ${depList[0].branch}`"
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-500 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                          <line x1="6" y1="3" x2="6" y2="15"></line>
                          <circle cx="18" cy="6" r="3"></circle>
                          <circle cx="6" cy="18" r="3"></circle>
                          <path d="M18 9a9 9 0 0 1-9 9"></path>
                        </svg>
                        <span class="truncate">{{ (depList[0].branch || '').replace('refs/heads/', '') }}</span>
                      </span>

                      <!-- Commit SHA Link -->
                      <button 
                        v-if="depList[0].commitSha"
                        @click.stop="getCommitUrl(row, depList[0]) ? openUrlInNewTab(getCommitUrl(row, depList[0])!) : null"
                        class="text-[10px] font-mono px-1.5 py-0.5 rounded transition-colors"
                        :class="getCommitUrl(row, depList[0]) ? 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100 hover:underline' : 'bg-slate-100 text-slate-600'"
                        :title="`Commit: ${depList[0].commitSha}`"
                      >
                        {{ depList[0].commitSha?.substring(0, 7) }}
                      </button>

                      <!-- PR Badge if present -->
                      <button 
                        v-if="depList[0].prNumber"
                        @click.stop="getPrUrl(row, depList[0]) ? openUrlInNewTab(getPrUrl(row, depList[0])!) : null"
                        class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-violet-100 text-violet-800 hover:bg-violet-200 transition-colors"
                        :title="`Pull Request #${depList[0].prNumber}`"
                      >
                        PR #{{ depList[0].prNumber }}
                      </button>
                    </div>

                    <!-- Bottom Row: Time ago & Author Badge -->
                    <div class="flex items-center justify-between text-[10px] text-slate-400 mt-1 pt-1 border-t border-slate-100">
                      <span :title="depList[0].deployedAt" class="text-[9px] shrink-0 font-mono">
                        {{ formatTimeAgo(depList[0].deployedAt) }}
                      </span>
                      <PipelineAuthorBadge 
                        :author="depList[0].author"
                        :author-image-url="depList[0].authorImageUrl"
                        :is-automated="depList[0].isAutomated"
                        :trigger-reason="depList[0].triggerReason"
                      />
                    </div>

                    <!-- Upstream Lag Indicator -->
                    <div v-if="getLagStatus(row, envIdx)" class="pt-0.5 border-t border-slate-100 flex items-center justify-between text-[9px]">
                      <span 
                        class="inline-flex items-center gap-1 font-medium"
                        :class="getLagStatus(row, envIdx)?.isSynced ? 'text-emerald-600' : 'text-amber-600 font-semibold'"
                      >
                        <span class="w-1.5 h-1.5 rounded-full" :class="getLagStatus(row, envIdx)?.isSynced ? 'bg-emerald-500' : 'bg-amber-500'"></span>
                        {{ getLagStatus(row, envIdx)?.label }}
                      </span>
                    </div>
                  </div>

                  <!-- 2. Rollback Target (N-1) Card (when enabled) -->
                  <template v-if="devopsStore.showRollbackTarget">
                    <!-- Previous deployment found -->
                    <div 
                      v-if="depList.length > 1 && depList[1]"
                      @contextmenu.stop="emit('open-context-menu', $event, row, depList[1], env)"
                      class="mt-1 p-1.5 rounded-md bg-amber-50/70 border border-amber-200/80 shadow-2xs flex flex-col gap-1"
                    >
                      <!-- Top Row: Rollback Badge & Previous Build -->
                      <div class="flex items-center justify-between gap-1">
                        <span class="inline-flex items-center gap-1 text-[9px] font-bold text-amber-800 uppercase tracking-wider">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
                          </svg>
                          Rollback (N-1)
                        </span>

                        <!-- Previous Build Number Link -->
                        <button 
                          v-if="depList[1].buildUrl"
                          @click.stop="openUrlInNewTab(depList[1].buildUrl!)"
                          class="text-[10px] font-mono text-blue-600 hover:underline hover:text-blue-800 truncate"
                          :title="`Open Previous Run #${depList[1].buildNumber} in Azure DevOps`"
                        >
                          #{{ depList[1].buildNumber }}
                        </button>
                      </div>

                      <!-- Middle: Version, Branch, Commit -->
                      <div class="flex items-center gap-1 flex-wrap">
                        <span 
                          v-if="depList[1].version"
                          class="px-1.5 py-0.2 rounded text-[9px] font-mono font-bold bg-white text-amber-900 border border-amber-200"
                          :title="`Previous Version/Tag: ${depList[1].version}`"
                        >
                          v{{ depList[1].version }}
                        </span>

                        <span 
                          v-if="depList[1].branch"
                          class="inline-flex items-center gap-0.5 text-[9px] font-medium text-slate-700 bg-white px-1 py-0.2 rounded border border-amber-200/60 max-w-[100px] truncate"
                          :title="`Branch: ${depList[1].branch}`"
                        >
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="18" cy="6" r="3"></circle>
                            <circle cx="6" cy="18" r="3"></circle>
                            <path d="M18 9a9 9 0 0 1-9 9"></path>
                          </svg>
                          <span class="truncate">{{ (depList[1].branch || '').replace('refs/heads/', '') }}</span>
                        </span>

                        <button 
                          v-if="depList[1].commitSha"
                          @click.stop="getCommitUrl(row, depList[1]) ? openUrlInNewTab(getCommitUrl(row, depList[1])!) : null"
                          class="text-[9px] font-mono px-1 py-0.2 rounded bg-white text-indigo-700 hover:underline border border-amber-200/60"
                          :title="`Previous Commit: ${depList[1].commitSha}`"
                        >
                          {{ depList[1].commitSha?.substring(0, 7) }}
                        </button>

                        <button 
                          v-if="depList[1].prNumber"
                          @click.stop="getPrUrl(row, depList[1]) ? openUrlInNewTab(getPrUrl(row, depList[1])!) : null"
                          class="text-[9px] font-semibold px-1 py-0.2 rounded bg-white text-violet-800 hover:underline border border-amber-200/60"
                          :title="`Previous PR #${depList[1].prNumber}`"
                        >
                          PR #{{ depList[1].prNumber }}
                        </button>
                      </div>

                      <!-- Bottom: Time Ago & Author -->
                      <div class="flex items-center justify-between text-[9px] text-slate-400 pt-0.5 border-t border-amber-100">
                        <span :title="depList[1].deployedAt">{{ formatTimeAgo(depList[1].deployedAt) }}</span>
                        <PipelineAuthorBadge 
                          :author="depList[1].author"
                          :author-image-url="depList[1].authorImageUrl"
                          :is-automated="depList[1].isAutomated"
                          :trigger-reason="depList[1].triggerReason"
                        />
                      </div>
                    </div>

                    <!-- No previous deployment found in explored window -->
                    <div 
                      v-else
                      class="mt-1 px-2 py-1 rounded bg-slate-50 border border-dashed border-slate-200 text-center text-[10px] text-slate-400 italic"
                    >
                      No prior version in last {{ devopsStore.scanDepth }} runs
                    </div>
                  </template>
                </div>

                <!-- Not Deployed in this environment -->
                <div v-else class="h-full flex items-center justify-center py-4 text-slate-300">
                  <span class="text-xs">&mdash;</span>
                </div>
              </template>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
