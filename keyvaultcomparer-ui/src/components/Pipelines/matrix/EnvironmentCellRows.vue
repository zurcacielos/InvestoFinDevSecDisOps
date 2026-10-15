<script setup lang="ts">
import { computed } from 'vue';
import type { EnvironmentDeploymentInfo, PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { formatTimeAgo, getCommitUrl, getPrUrl, getBuildUrl } from '../../../utils/pipelineUrlHelpers';
import DeploymentStatusIndicator from './DeploymentStatusIndicator.vue';
import PipelineAuthorBadge from '../PipelineAuthorBadge.vue';
import EmptyEnvironmentCell from './EmptyEnvironmentCell.vue';

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
  return props.deployments.length > 1 ? props.deployments[1] : null;
});

const isScanning = computed(() => {
  return devopsStore.deepScanningPipelineIds.includes(props.row.pipelineId) ||
         devopsStore.isSinglePipelineRefreshing(props.row.pipelineId);
});

const currentRunsCount = computed(() => {
  return props.row.scannedRunsCount ?? props.row.recentRuns?.length ?? devopsStore.scanDepth;
});

const nextDepth = computed(() => {
  return Math.min(currentRunsCount.value + 50, 200);
});

const handleDeepScan = () => {
  devopsStore.deepScanPipeline(props.row.pipelineId, 50);
};

const priorBuildUrl = computed(() => {
  if (!priorDep.value) return null;
  return getBuildUrl(
    devopsStore.organization,
    devopsStore.project,
    priorDep.value.buildId,
    priorDep.value.buildUrl
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

const priorCommitUrl = computed(() => {
  if (!priorDep.value) return null;
  return getCommitUrl(
    props.row.repository,
    priorDep.value.commitSha,
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
    class="flex flex-col rounded-lg bg-white border border-slate-200/90 shadow-2xs divide-y divide-slate-100 overflow-hidden text-xs select-none hover:border-blue-300 transition-all"
  >
    <!-- Row 1: Strict Version / Tag & Run Link -->
    <div class="h-7 px-2 flex items-center justify-between gap-1 bg-slate-50/70">
      <div class="flex items-center gap-1.5 min-w-0">
        <DeploymentStatusIndicator :status="activeDep.status" :scan-position="activeDep.scanPosition" />
        <span 
          v-if="activeDep.version"
          class="font-mono font-bold text-xs text-violet-700 truncate"
          :title="`Version/Tag: ${activeDep.version}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          v{{ activeDep.version }}
        </span>
        <span 
          v-else-if="activeDep.commitSha"
          class="font-mono font-bold text-xs text-slate-700 truncate"
          :title="`Commit: ${activeDep.commitSha}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </span>
      </div>

      <!-- Run # Link -->
      <button 
        v-if="activeDep.buildUrl"
        @click.stop="openUrlInNewTab(activeDep.buildUrl!)"
        class="text-[10px] font-mono text-blue-600 hover:underline shrink-0"
        :title="activeDep.scanPosition ? `Open Run #${activeDep.buildNumber} in Azure DevOps (Scanned build position: #${activeDep.scanPosition})` : `Open Run #${activeDep.buildNumber} in Azure DevOps`"
      >
        #{{ activeDep.buildNumber }}
      </button>
    </div>

    <!-- Row 2: Strict Commit SHA & PR -->
    <div class="h-6 px-2 flex items-center justify-between gap-1 text-[10px]">
      <div class="flex items-center gap-1 min-w-0">
        <span class="text-slate-400 font-mono text-[9px]">SHA:</span>
        <button 
          v-if="activeDep.commitSha"
          @click.stop="commitUrl ? openUrlInNewTab(commitUrl) : null"
          class="font-mono text-indigo-700 hover:underline truncate"
          :title="`Commit: ${activeDep.commitSha}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </button>
        <span v-else class="text-slate-300">-</span>
      </div>

      <button 
        v-if="activeDep.prNumber"
        @click.stop="prUrl ? openUrlInNewTab(prUrl) : null"
        class="font-semibold text-violet-700 hover:underline shrink-0 text-[9px]"
        :title="`Pull Request #${activeDep.prNumber}`"
      >
        PR #{{ activeDep.prNumber }}
      </button>
    </div>

    <!-- Row 3: Strict Branch -->
    <div class="h-6 px-2 flex items-center gap-1 text-[10px] text-slate-700 min-w-0">
      <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="18" cy="6" r="3"></circle>
        <circle cx="6" cy="18" r="3"></circle>
        <path d="M18 9a9 9 0 0 1-9 9"></path>
      </svg>
      <span class="truncate font-medium" :title="`Branch: ${activeDep.branch}`">
        {{ cleanBranch || '-' }}
      </span>
    </div>

    <!-- Row 4: Strict Date / Time & Author -->
    <div class="h-6 px-2 flex items-center justify-between gap-1 text-[10px] text-slate-500">
      <span :title="formattedFullDate" class="text-[9px] font-mono cursor-help truncate">
        {{ formatTimeAgo(activeDep.deployedAt) }}
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
    <div class="h-5 px-2 flex items-center justify-between text-[9px] bg-slate-50/50">
      <div v-if="lagStatus" class="flex items-center gap-1 font-medium">
        <span 
          class="w-1.5 h-1.5 rounded-full" 
          :class="lagStatus.isSynced ? 'bg-emerald-500' : 'bg-amber-500'"
        ></span>
        <span :class="lagStatus.isSynced ? 'text-emerald-700' : 'text-amber-700 font-semibold'">
          {{ lagStatus.label }}
        </span>
      </div>
      <span v-else class="text-slate-300 italic text-[9px]">Origin baseline</span>
    </div>

    <!-- Row 6: Prior Run (N-1) Row (when enabled) -->
    <div 
      v-if="devopsStore.showRollbackTarget"
      class="h-6 px-2 flex items-center justify-between gap-1 text-[9px] bg-amber-50/80 border-t border-amber-200/60"
    >
      <div v-if="priorDep" class="flex items-center gap-1 min-w-0">
        <svg 
          xmlns="http://www.w3.org/2000/svg" 
          class="h-2.5 w-2.5 text-amber-600 shrink-0 cursor-help" 
          fill="none" 
          viewBox="0 0 24 24" 
          stroke="currentColor"
          :title="priorDep.scanPosition ? `Previous deployment return point · Scanned build position #${priorDep.scanPosition}` : 'Previous deployment return point'"
        >
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
        </svg>
        <button 
          v-if="priorDep.version"
          @click.stop="priorBuildUrl ? openUrlInNewTab(priorBuildUrl) : null"
          class="font-mono font-bold text-amber-950 truncate transition-colors"
          :class="priorBuildUrl ? 'hover:underline hover:text-amber-800 cursor-pointer' : 'cursor-default'"
          :title="priorBuildUrl ? `Open Previous Run #${priorDep.buildNumber} in Azure DevOps (v${priorDep.version})${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}` : `Previous Version: v${priorDep.version}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
        >
          v{{ priorDep.version }}
        </button>
        <button 
          v-else-if="priorDep.commitSha"
          @click.stop="priorCommitUrl ? openUrlInNewTab(priorCommitUrl) : null"
          class="font-mono text-indigo-700 hover:underline truncate"
          :title="`Previous Commit: ${priorDep.commitSha}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
        >
          {{ priorDep.commitSha.substring(0, 7) }}
        </button>
      </div>
      <span v-if="priorDep" class="text-amber-800/80 font-mono shrink-0">
        {{ formatTimeAgo(priorDep.deployedAt) }}
      </span>
      <div v-else class="flex items-center justify-between w-full text-[9px] text-slate-500">
        <span class="italic text-slate-400 truncate" :title="`No prior deployment found in the last ${currentRunsCount} scanned runs for '${row.pipelineName}' on '${env}'`">
          No prior in {{ currentRunsCount }} runs
        </span>
        <button 
          type="button"
          @click.stop="handleDeepScan"
          :disabled="isScanning"
          class="font-semibold text-amber-700 hover:text-amber-900 hover:underline shrink-0 ml-1 transition-colors disabled:opacity-50 flex items-center gap-1 cursor-pointer"
          :title="`Search up to ${nextDepth} older runs for '${row.pipelineName}' in Azure DevOps`"
        >
          <svg v-if="isScanning" class="animate-spin h-2.5 w-2.5 text-amber-600" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <span>{{ isScanning ? 'Scanning...' : 'Scan +50' }}</span>
        </button>
      </div>
    </div>
  </div>

  <!-- Empty State with On-Demand Deep Scan -->
  <EmptyEnvironmentCell v-else :row="row" :env="env" />
</template>
