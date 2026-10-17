<script setup lang="ts">
import { computed } from 'vue';
import type { EnvironmentDeploymentInfo, PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { formatTimeAndDate, getCommitUrl, getPrUrl, getBuildUrl } from '../../../utils/pipelineUrlHelpers';
import DeploymentStatusIndicator from './DeploymentStatusIndicator.vue';
import PipelineAuthorBadge from '../PipelineAuthorBadge.vue';

const props = defineProps<{
  row: PipelineMatrixRow;
  deployments: EnvironmentDeploymentInfo[];
  env: string;
  envIndex: number;
}>();

const emit = defineEmits<{
  (e: 'open-context-menu', event: MouseEvent, row: PipelineMatrixRow, cell: EnvironmentDeploymentInfo | null, env: string): void;
}>();

const settingsStore = useSettingsStore();
const devopsStore = useDevopsDataStore();

const activeDep = computed<EnvironmentDeploymentInfo | null>(() => {
  return props.deployments.length > 0 ? props.deployments[0] : null;
});

const priorDep = computed<EnvironmentDeploymentInfo | null>(() => {
  if (props.deployments.length <= 1) return null;
  if (devopsStore.priorRunMode === 'distinct') {
    const active = props.deployments[0];
    const found = props.deployments.slice(1).find(d => {
      if (active.commitSha && d.commitSha) {
        return d.commitSha.toLowerCase() !== active.commitSha.toLowerCase();
      }
      if (active.version && d.version) {
        return d.version !== active.version;
      }
      return d.buildId !== active.buildId;
    });
    return found || null;
  }
  return props.deployments[1];
});

const isSameRevision = computed(() => {
  if (!activeDep.value || !priorDep.value) return false;
  if (activeDep.value.commitSha && priorDep.value.commitSha) {
    return activeDep.value.commitSha.toLowerCase() === priorDep.value.commitSha.toLowerCase();
  }
  if (activeDep.value.version && priorDep.value.version) {
    return activeDep.value.version === priorDep.value.version;
  }
  return false;
});

const activeBuildUrl = computed(() => {
  if (!activeDep.value) return null;
  return getBuildUrl(
    devopsStore.organization,
    devopsStore.project,
    activeDep.value.buildId,
    activeDep.value.buildUrl
  );
});

const commitUrl = computed(() => {
  if (!activeDep.value) return null;
  return getCommitUrl(
    props.row.repository,
    activeDep.value.commitSha,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
});

const prUrl = computed(() => {
  if (!activeDep.value) return null;
  return getPrUrl(
    props.row.repository,
    activeDep.value.prNumber,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
});

const cleanBranch = computed(() => {
  const b = activeDep.value?.branch;
  return b ? b.replace('refs/heads/', '') : null;
});

const formattedFullDate = computed(() => {
  if (!activeDep.value?.deployedAt) return '';
  const d = new Date(activeDep.value.deployedAt);
  const pos = activeDep.value.scanPosition ? ` · Scanned Run #${activeDep.value.scanPosition}` : '';
  return `${d.toLocaleString()}${pos}`;
});

const lagStatus = computed(() => {
  if (props.envIndex === 0) return null;
  const envs = devopsStore.releaseMatrix?.environments || [];
  const prevEnv = envs[props.envIndex - 1];
  if (!prevEnv) return null;

  const currentDep = props.row.environmentDeployments[props.env];
  const prevDep = props.row.environmentDeployments[prevEnv];
  if (!currentDep || !prevDep || !currentDep.commitSha || !prevDep.commitSha) return null;

  if (currentDep.commitSha === prevDep.commitSha) {
    return { isSynced: true, label: 'In sync with upstream' };
  } else {
    return { isSynced: false, label: 'Lagging behind upstream' };
  }
});
</script>

<template>
  <div 
    v-if="activeDep"
    @contextmenu.stop="emit('open-context-menu', $event, row, activeDep, env)"
    class="flex flex-col w-full text-xs select-none divide-y divide-slate-100 min-w-max bg-white"
  >
    <!-- Row 1: Strict Active Version / Tag & Run Link -->
    <div class="h-7 px-3 flex items-center justify-between gap-2 bg-white whitespace-nowrap">
      <div class="flex items-center gap-1.5 whitespace-nowrap">
        <DeploymentStatusIndicator :status="activeDep.status" :scan-position="activeDep.scanPosition" />
        <button 
          v-if="activeDep.version"
          @click.stop="activeBuildUrl ? openUrlInNewTab(activeBuildUrl) : null"
          class="font-mono font-bold text-xs text-slate-800 whitespace-nowrap transition-colors"
          :class="activeBuildUrl ? 'hover:underline hover:text-blue-600 cursor-pointer' : 'cursor-default'"
          :title="activeBuildUrl ? `Open Run #${activeDep.buildNumber} in Azure DevOps (v${activeDep.version})${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}` : `Version/Tag: ${activeDep.version}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          v{{ activeDep.version }}
        </button>
        <button 
          v-else-if="activeDep.commitSha"
          @click.stop="commitUrl ? openUrlInNewTab(commitUrl) : null"
          class="font-mono font-bold text-xs text-slate-800 whitespace-nowrap hover:underline hover:text-blue-600 cursor-pointer"
          :title="`Commit: ${activeDep.commitSha}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </button>
      </div>

      <!-- Open Run in Azure DevOps (External Link Icon) -->
      <button 
        v-if="activeBuildUrl"
        @click.stop="openUrlInNewTab(activeBuildUrl)"
        class="p-0.5 rounded text-slate-400 hover:text-blue-600 hover:bg-slate-100 transition-colors shrink-0 cursor-pointer flex items-center justify-center"
        :title="activeDep.scanPosition ? `Open Run #${activeDep.buildNumber} in Azure DevOps (Scanned build position: #${activeDep.scanPosition})` : `Open Run #${activeDep.buildNumber} in Azure DevOps`"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
          <polyline points="15 3 21 3 21 9"></polyline>
          <line x1="10" y1="14" x2="21" y2="3"></line>
        </svg>
      </button>
    </div>

    <!-- Row 2: Strict Active Commit SHA & PR -->
    <div class="h-6 px-3 flex items-center justify-between gap-2 text-[10px] whitespace-nowrap">
      <div class="flex items-center gap-1 whitespace-nowrap">
        <span class="text-slate-400 font-mono text-[9px]">SHA:</span>
        <button 
          v-if="activeDep.commitSha"
          @click.stop="commitUrl ? openUrlInNewTab(commitUrl) : null"
          class="font-mono text-slate-700 hover:text-blue-600 hover:underline whitespace-nowrap"
          :title="`Commit: ${activeDep.commitSha}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </button>
        <span 
          v-if="activeDep.buildNumber && isSameRevision"
          class="text-slate-400 font-mono text-[9px] whitespace-nowrap"
          title="Build number"
        >
          (build #{{ activeDep.buildNumber }})
        </span>
        <span v-else-if="!activeDep.commitSha" class="text-slate-300">-</span>
      </div>

      <button 
        v-if="activeDep.prNumber"
        @click.stop="prUrl ? openUrlInNewTab(prUrl) : null"
        class="font-semibold text-slate-600 hover:text-blue-600 hover:underline shrink-0 text-[9px]"
        :title="`Pull Request #${activeDep.prNumber}`"
      >
        PR #{{ activeDep.prNumber }}
      </button>
    </div>

    <!-- Row 3: Strict Active Branch -->
    <div class="h-6 px-3 flex items-center gap-1 text-[10px] text-slate-700 whitespace-nowrap">
      <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="18" cy="6" r="3"></circle>
        <circle cx="6" cy="18" r="3"></circle>
        <path d="M18 9a9 9 0 0 1-9 9"></path>
      </svg>
      <span class="whitespace-nowrap font-medium" :title="`Branch: ${activeDep.branch}`">
        {{ cleanBranch || '-' }}
      </span>
    </div>

    <!-- Row 4: Strict Active Date / Time & Author (e.g. 5d ago · Jun 18, 2026) -->
    <div class="h-6 px-3 flex items-center justify-between gap-2 text-[10px] text-slate-500 whitespace-nowrap">
      <span :title="formattedFullDate" class="text-[9px] font-mono whitespace-nowrap">
        {{ formatTimeAndDate(activeDep.deployedAt) }}
      </span>

      <PipelineAuthorBadge 
        :author="activeDep.author"
        :author-image-url="activeDep.authorImageUrl"
        :is-automated="activeDep.isAutomated"
        :trigger-reason="activeDep.triggerReason"
        compact
      />
    </div>

    <!-- Row 5: Upstream Status -->
    <div class="h-5 px-3 flex items-center justify-between text-[9px] bg-white whitespace-nowrap">
      <div v-if="lagStatus" class="flex items-center gap-1 font-medium whitespace-nowrap">
        <span 
          class="w-1.5 h-1.5 rounded-full" 
          :class="lagStatus.isSynced ? 'bg-emerald-500' : 'bg-amber-500'"
        ></span>
        <span :class="lagStatus.isSynced ? 'text-emerald-700' : 'text-amber-700 font-semibold'">
          {{ lagStatus.label }}
        </span>
      </div>
      <div v-else class="flex items-center gap-1 font-medium text-slate-500 text-[9px] whitespace-nowrap">
        <span class="w-1.5 h-1.5 rounded-full bg-slate-400"></span>
        <span>Origin baseline</span>
      </div>
    </div>
  </div>

  <!-- Fallback if Active Dep not found -->
  <div v-else class="h-30 flex items-center justify-center text-slate-400 text-xs italic">
    —
  </div>
</template>
