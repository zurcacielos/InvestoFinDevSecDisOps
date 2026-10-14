<script setup lang="ts">
import { ref } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineMatrixRow, StageDeploymentInfo } from '../../stores/devopsDataStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { openUrlInNewTab } from '../../utils/urlOpener';

const devopsStore = useDevopsDataStore();
const settingsStore = useSettingsStore();

// Context Menu State
interface ContextMenuState {
  show: boolean;
  x: number;
  y: number;
  row: PipelineMatrixRow | null;
  cell: StageDeploymentInfo | null;
  environment: string;
}

const contextMenu = ref<ContextMenuState>({
  show: false,
  x: 0,
  y: 0,
  row: null,
  cell: null,
  environment: '',
});

const hideContextMenu = () => {
  contextMenu.value.show = false;
  window.removeEventListener('click', hideContextMenu);
  window.removeEventListener('keydown', handleContextMenuEsc);
};

const handleContextMenuEsc = (e: KeyboardEvent) => {
  if (e.key === 'Escape') hideContextMenu();
};

const openContextMenu = (e: MouseEvent, row: PipelineMatrixRow, cell: StageDeploymentInfo | undefined, env: string) => {
  e.preventDefault();
  contextMenu.value = {
    show: true,
    x: e.clientX,
    y: e.clientY,
    row,
    cell: cell || null,
    environment: env,
  };
  setTimeout(() => {
    window.addEventListener('click', hideContextMenu);
    window.addEventListener('keydown', handleContextMenuEsc);
  }, 0);
};

// URL construction helpers honoring GitHub tab settings
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

const getPipelineDefinitionUrl = (pipelineId: number) => {
  const org = devopsStore.organization;
  const proj = devopsStore.project;
  return `https://dev.azure.com/${org}/${proj}/_build?definitionId=${pipelineId}`;
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

const copyToClipboard = async (text?: string) => {
  if (!text) return;
  await navigator.clipboard.writeText(text);
  hideContextMenu();
};

const isOpeningYamls = ref(false);

const openAllGithubYamls = async (row: PipelineMatrixRow) => {
  isOpeningYamls.value = true;
  try {
    const baseUrl = settingsStore.uiSettings.githubBaseUrl?.replace(/\/$/, '') || 'https://github.com';
    const org = settingsStore.uiSettings.useGithubOrg ? settingsStore.uiSettings.githubOrg : '';

    const result = await devopsStore.getPipelineGithubYamls(row.pipelineId, baseUrl, org);

    if (result && result.links && result.links.length > 0) {
      // First link is main pipeline YAML, subsequent links are templates in order
      for (const link of result.links) {
        openUrlInNewTab(link.url);
      }
    } else {
      // Fallback: construct main YAML link directly
      const repo = row.repository;
      if (repo) {
        let repoName = repo.name || '';
        if (!repoName.includes('/') && settingsStore.uiSettings.useGithubOrg && settingsStore.uiSettings.githubOrg) {
          repoName = `${settingsStore.uiSettings.githubOrg}/${repoName}`;
        }
        const branch = repo.defaultBranch?.replace('refs/heads/', '') || 'main';
        const yamlPath = row.yamlFilename || 'azure-pipelines.yml';
        const fallbackUrl = `${baseUrl}/${repoName}/blob/${branch}/${yamlPath}`;
        openUrlInNewTab(fallbackUrl);
      }
    }
  } catch (err) {
    console.error('Error opening GitHub YAMLs:', err);
  } finally {
    isOpeningYamls.value = false;
  }
};
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0 bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden relative">
    
    <!-- Empty State: No Pipelines Selected -->
    <div 
      v-if="devopsStore.selectedPipelineIds.length === 0"
      class="flex-1 flex flex-col items-center justify-center p-8 text-center"
    >
      <div class="w-16 h-16 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mb-4 border border-blue-100">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
        </svg>
      </div>
      <h3 class="text-base font-bold text-slate-800 mb-1">No Pipelines Selected for Release Matrix</h3>
      <p class="text-xs text-slate-500 max-w-md mb-4 leading-relaxed">
        Select Azure DevOps pipelines in the ribbon bar above to generate a cross-environment deployment matrix, track live release branches, SHAs, and copy fully resolved YAMLs with nested sub-templates.
      </p>
      <button 
        @click="devopsStore.fetchPipelines()"
        class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-medium text-xs rounded-lg shadow-sm transition-colors"
      >
        Fetch &amp; Browse Pipelines
      </button>
    </div>

    <!-- Loading Matrix State -->
    <div 
      v-else-if="devopsStore.isMatrixLoading && !devopsStore.releaseMatrix"
      class="flex-1 flex flex-col items-center justify-center p-8 text-center"
    >
      <svg class="animate-spin h-8 w-8 text-blue-600 mb-3" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
      </svg>
      <div class="text-sm font-semibold text-slate-700">Querying ADO Pipeline Runs &amp; Stages...</div>
      <div class="text-xs text-slate-400 mt-1">Aggregating multi-stage timeline records across selected services</div>
    </div>

    <!-- Error State -->
    <div 
      v-else-if="devopsStore.matrixError"
      class="flex-1 flex flex-col items-center justify-center p-8 text-center"
    >
      <div class="p-4 bg-rose-50 border border-rose-200 rounded-xl text-rose-700 max-w-md">
        <div class="font-bold text-sm mb-1">Failed to Load Release Matrix</div>
        <div class="text-xs">{{ devopsStore.matrixError }}</div>
        <button 
          @click="devopsStore.fetchReleaseMatrix()"
          class="mt-3 px-3 py-1.5 bg-rose-600 hover:bg-rose-700 text-white text-xs font-semibold rounded-lg shadow-sm transition-colors"
        >
          Retry Matrix Fetch
        </button>
      </div>
    </div>

    <!-- Release Matrix Table -->
    <div v-else class="flex-1 flex flex-col overflow-hidden">
      
      <!-- Table Header Summary Strip -->
      <div class="px-4 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between text-xs text-slate-500 shrink-0">
        <div class="flex items-center gap-3">
          <span class="font-semibold text-slate-700">
            {{ devopsStore.releaseMatrix?.rows.length || 0 }} Pipelines &middot; {{ devopsStore.releaseMatrix?.environments.length || 0 }} Environments Discovered
          </span>
          <span class="text-slate-400">|</span>
          <span class="text-slate-500 text-[11px]">
            Right-click any cell for Azure DevOps &amp; GitHub deep links
          </span>
        </div>
        <div class="flex items-center gap-2">
          <span class="inline-flex items-center gap-1 text-[11px] text-emerald-700 font-medium">
            <span class="w-2 h-2 rounded-full bg-emerald-500"></span> Succeeded
          </span>
          <span class="inline-flex items-center gap-1 text-[11px] text-rose-700 font-medium">
            <span class="w-2 h-2 rounded-full bg-rose-500"></span> Failed
          </span>
          <span class="inline-flex items-center gap-1 text-[11px] text-blue-700 font-medium">
            <span class="w-2 h-2 rounded-full bg-blue-500 animate-pulse"></span> Running
          </span>
        </div>
      </div>

      <!-- Scrollable Grid Table -->
      <div class="flex-1 overflow-auto">
        <table class="w-full text-left border-collapse table-auto">
          <thead>
            <tr class="bg-slate-100/80 border-b border-slate-200 text-slate-600 text-xs uppercase tracking-wider sticky top-0 shadow-sm z-10">
              
              <!-- Service / Pipeline Name Column -->
              <th class="px-4 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[240px] max-w-[300px]">
                Service / Pipeline
              </th>

              <!-- Repository Column -->
              <th class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[160px]">
                Repository
              </th>

              <!-- Environment Columns -->
              <th 
                v-for="env in devopsStore.releaseMatrix?.environments" 
                :key="env"
                class="px-3 py-2.5 font-bold bg-slate-100 border-r border-slate-200 text-center min-w-[190px]"
              >
                <div class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-white border border-slate-200 shadow-xs text-slate-800">
                  <span class="w-1.5 h-1.5 rounded-full bg-blue-500"></span>
                  {{ env }}
                </div>
              </th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100 text-xs">
            <tr 
              v-for="row in devopsStore.releaseMatrix?.rows" 
              :key="row.pipelineId"
              class="hover:bg-slate-50/80 transition-colors"
            >
              <!-- Row Header: Pipeline Info & Action Buttons -->
              <td 
                @contextmenu="openContextMenu($event, row, undefined, '')"
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

                  <!-- Quick Action Buttons -->
                  <div class="flex items-center gap-1 shrink-0">
                    <!-- Open all GitHub Yaml Button -->
                    <button 
                      @click.stop="openAllGithubYamls(row)"
                      class="p-1 rounded text-slate-400 hover:text-violet-700 hover:bg-violet-50 transition-colors"
                      title="Open all GitHub Yaml (Main pipeline &amp; templates in new tabs)"
                    >
                      <svg class="h-3.5 w-3.5 text-slate-700" viewBox="0 0 24 24" fill="currentColor">
                        <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
                      </svg>
                    </button>

                    <!-- Copy Full YAML Button -->
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

                    <!-- View YAML Modal Button -->
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

              <!-- Repository Cell -->
              <td 
                @contextmenu="openContextMenu($event, row, undefined, '')"
                class="px-3 py-3 border-r border-slate-200 cursor-context-menu"
              >
                <div v-if="row.repository" class="flex flex-col min-w-0">
                  <div class="flex items-center gap-1.5">
                    <!-- Git Icon -->
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
                v-for="env in devopsStore.releaseMatrix?.environments" 
                :key="env"
                @contextmenu="openContextMenu($event, row, row.deployments[env], env)"
                class="px-3 py-2.5 border-r border-slate-200 select-none cursor-context-menu"
                :class="row.deployments[env] ? 'hover:bg-blue-50/50' : 'bg-slate-50/40'"
              >
                <!-- If Deployed -->
                <div v-if="row.deployments[env]" class="flex flex-col gap-1">
                  
                  <!-- Top Row: Status Badge & Build Number -->
                  <div class="flex items-center justify-between gap-1">
                    <span 
                      class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider flex items-center gap-1"
                      :class="{
                        'bg-emerald-100 text-emerald-800 border border-emerald-200': row.deployments[env].status === 'succeeded',
                        'bg-rose-100 text-rose-800 border border-rose-200': row.deployments[env].status === 'failed',
                        'bg-blue-100 text-blue-800 border border-blue-200 animate-pulse': row.deployments[env].status === 'inProgress',
                        'bg-amber-100 text-amber-800 border border-amber-200': row.deployments[env].status === 'partiallySucceeded',
                        'bg-slate-100 text-slate-600 border border-slate-200': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(row.deployments[env].status)
                      }"
                    >
                      <span class="w-1.5 h-1.5 rounded-full" :class="{
                        'bg-emerald-600': row.deployments[env].status === 'succeeded',
                        'bg-rose-600': row.deployments[env].status === 'failed',
                        'bg-blue-600': row.deployments[env].status === 'inProgress',
                        'bg-amber-600': row.deployments[env].status === 'partiallySucceeded',
                        'bg-slate-400': !['succeeded', 'failed', 'inProgress', 'partiallySucceeded'].includes(row.deployments[env].status)
                      }"></span>
                      {{ row.deployments[env].status }}
                    </span>

                    <button 
                      v-if="row.deployments[env].buildUrl"
                      @click.stop="openUrlInNewTab(row.deployments[env].buildUrl!)"
                      class="text-[11px] font-mono text-blue-600 hover:underline hover:text-blue-800 truncate"
                      title="Open Run in Azure DevOps"
                    >
                      #{{ row.deployments[env].buildNumber }}
                    </button>
                  </div>

                  <!-- Middle Row: Branch & Commit SHA -->
                  <div class="flex items-center gap-1.5 flex-wrap">
                    <!-- Branch -->
                    <span 
                      v-if="row.deployments[env].branch"
                      class="inline-flex items-center gap-1 text-[11px] font-medium text-slate-700 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200 max-w-[120px] truncate"
                      :title="row.deployments[env].branch"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
                      </svg>
                      <span class="truncate">{{ row.deployments[env].branch }}</span>
                    </span>

                    <!-- Commit SHA -->
                    <button 
                      v-if="row.deployments[env].commitSha"
                      @click.stop="getCommitUrl(row, row.deployments[env]) ? openUrlInNewTab(getCommitUrl(row, row.deployments[env])!) : null"
                      class="text-[10px] font-mono px-1.5 py-0.5 rounded transition-colors"
                      :class="getCommitUrl(row, row.deployments[env]) ? 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100 hover:underline' : 'bg-slate-100 text-slate-600'"
                      :title="`Commit: ${row.deployments[env].commitSha}`"
                    >
                      {{ row.deployments[env].commitSha?.substring(0, 7) }}
                    </button>

                    <!-- PR Badge if present -->
                    <button 
                      v-if="row.deployments[env].prNumber"
                      @click.stop="getPrUrl(row, row.deployments[env]) ? openUrlInNewTab(getPrUrl(row, row.deployments[env])!) : null"
                      class="text-[10px] font-semibold px-1.5 py-0.5 rounded bg-violet-100 text-violet-800 hover:bg-violet-200 transition-colors"
                      :title="`Pull Request #${row.deployments[env].prNumber}`"
                    >
                      PR #{{ row.deployments[env].prNumber }}
                    </button>
                  </div>

                  <!-- Bottom Row: Time ago & Author -->
                  <div class="flex items-center justify-between text-[10px] text-slate-400 mt-0.5">
                    <span :title="row.deployments[env].deployedAt">
                      {{ formatTimeAgo(row.deployments[env].deployedAt) }}
                    </span>
                    <span v-if="row.deployments[env].author" class="truncate max-w-[90px]" :title="row.deployments[env].author">
                      {{ row.deployments[env].author }}
                    </span>
                  </div>
                </div>

                <!-- Not Deployed In This Environment -->
                <div v-else class="h-full flex items-center justify-center py-2 text-slate-300">
                  <span class="text-xs">&mdash;</span>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Right-Click Cell Context Menu -->
    <div 
      v-if="contextMenu.show"
      :style="{ top: `${contextMenu.y}px`, left: `${contextMenu.x}px` }"
      class="fixed z-50 bg-white border border-slate-200 rounded-xl shadow-2xl py-1 w-64 text-xs divide-y divide-slate-100 select-none animate-in fade-in duration-100"
    >
      <!-- Target Summary -->
      <div class="px-3 py-1.5 bg-slate-50 text-slate-600 font-semibold flex items-center justify-between">
        <span class="truncate">{{ contextMenu.row?.pipelineName }}</span>
        <span class="text-[10px] text-blue-600 font-mono">{{ contextMenu.environment }}</span>
      </div>

      <!-- Links Group -->
      <div class="py-1">
        <!-- Open Pipeline Run in ADO -->
        <button 
          v-if="contextMenu.cell?.buildUrl"
          @click="openUrlInNewTab(contextMenu.cell.buildUrl); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-blue-50 hover:text-blue-700 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
          </svg>
          Open Run #{{ contextMenu.cell.buildNumber }} in Azure DevOps
        </button>

        <!-- Open Commit Link (GitHub / Azure Repos) -->
        <button 
          v-if="contextMenu.row && contextMenu.cell && getCommitUrl(contextMenu.row, contextMenu.cell)"
          @click="openUrlInNewTab(getCommitUrl(contextMenu.row, contextMenu.cell)!); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-blue-50 hover:text-blue-700 flex items-center gap-2 text-slate-700"
        >
          <svg class="h-3.5 w-3.5 text-slate-700" viewBox="0 0 24 24" fill="currentColor">
            <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
          </svg>
          Open Commit ({{ contextMenu.cell.commitSha?.substring(0, 7) }})
        </button>

        <!-- Open PR Link -->
        <button 
          v-if="contextMenu.row && contextMenu.cell?.prNumber && getPrUrl(contextMenu.row, contextMenu.cell)"
          @click="openUrlInNewTab(getPrUrl(contextMenu.row, contextMenu.cell)!); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-blue-50 hover:text-blue-700 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-violet-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
          </svg>
          Open Pull Request #{{ contextMenu.cell.prNumber }}
        </button>

        <!-- Open Repository -->
        <button 
          v-if="contextMenu.row && getRepoUrl(contextMenu.row)"
          @click="openUrlInNewTab(getRepoUrl(contextMenu.row)!); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-blue-50 hover:text-blue-700 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
          </svg>
          Open Repository
        </button>

        <!-- Open all GitHub Yaml -->
        <button 
          v-if="contextMenu.row"
          @click="openAllGithubYamls(contextMenu.row); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-violet-50 hover:text-violet-700 flex items-center gap-2 text-slate-800 font-medium"
        >
          <svg class="h-3.5 w-3.5 text-slate-800 shrink-0" viewBox="0 0 24 24" fill="currentColor">
            <path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
          </svg>
          <span>Open all GitHub Yaml</span>
          <span class="ml-auto text-[10px] text-violet-600 bg-violet-50 px-1 py-0.5 rounded border border-violet-200 font-mono">All Tabs</span>
        </button>

        <!-- Open Definition in ADO -->
        <button 
          v-if="contextMenu.row"
          @click="openUrlInNewTab(getPipelineDefinitionUrl(contextMenu.row.pipelineId)); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-blue-50 hover:text-blue-700 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          Open Pipeline Definition in ADO
        </button>
      </div>

      <!-- Actions & Copy Group -->
      <div class="py-1">
        <!-- Copy Full Pipeline YAML (All Sub-Templates Resolved) -->
        <button 
          v-if="contextMenu.row"
          @click="devopsStore.copyPipelineYamlToClipboard(contextMenu.row.pipelineId, contextMenu.row.pipelineName); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-emerald-50 hover:text-emerald-700 flex items-center gap-2 font-medium text-slate-800"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
          Copy Full YAML (All Templates Inlined)
        </button>

        <!-- View YAML Modal -->
        <button 
          v-if="contextMenu.row"
          @click="devopsStore.openYamlModal(contextMenu.row.pipelineId, contextMenu.row.pipelineName); hideContextMenu()"
          class="w-full text-left px-3 py-1.5 hover:bg-blue-50 hover:text-blue-700 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
          </svg>
          View Full YAML in Modal
        </button>

        <!-- Copy Commit SHA -->
        <button 
          v-if="contextMenu.cell?.commitSha"
          @click="copyToClipboard(contextMenu.cell.commitSha)"
          class="w-full text-left px-3 py-1.5 hover:bg-slate-50 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          Copy Commit SHA ({{ contextMenu.cell.commitSha.substring(0, 7) }})
        </button>

        <!-- Copy Branch -->
        <button 
          v-if="contextMenu.cell?.branch"
          @click="copyToClipboard(contextMenu.cell.branch)"
          class="w-full text-left px-3 py-1.5 hover:bg-slate-50 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
          </svg>
          Copy Branch Name ({{ contextMenu.cell.branch }})
        </button>

        <!-- Copy Build Number -->
        <button 
          v-if="contextMenu.cell?.buildNumber"
          @click="copyToClipboard(contextMenu.cell.buildNumber)"
          class="w-full text-left px-3 py-1.5 hover:bg-slate-50 flex items-center gap-2 text-slate-700"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Copy Build Number (#{{ contextMenu.cell.buildNumber }})
        </button>
      </div>
    </div>
  </div>
</template>
