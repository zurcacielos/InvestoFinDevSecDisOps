<script setup lang="ts">
import { computed } from 'vue';
import type { EnvironmentDeploymentInfo, PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { formatTimeAndDate, getCommitUrl, getPrUrl, getBuildUrl } from '../../../utils/pipelineUrlHelpers';
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

const isScanning = computed(() => {
  return devopsStore.deepScanningPipelineIds.includes(props.row.pipelineId) ||
         devopsStore.isSinglePipelineRefreshing(props.row.pipelineId);
});

const currentRunsCount = computed(() => {
  return props.row.scannedRunsCount ?? props.row.recentRuns?.length ?? devopsStore.scanDepth;
});

const canScanMore = computed(() => {
  return devopsStore.canScanMore(props.row);
});

const nextDepth = computed(() => {
  return Math.min(currentRunsCount.value + 50, 500);
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
  if (!priorDep.value?.commitSha) return null;
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
  if (!priorDep.value?.prNumber) return null;
  return getPrUrl(
    props.row.repository,
    priorDep.value.prNumber,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
});

const cleanBranch = computed(() => {
  const b = priorDep.value?.branch;
  return b ? b.replace('refs/heads/', '') : null;
});

const formattedFullDate = computed(() => {
  if (!priorDep.value?.deployedAt) return '';
  const d = new Date(priorDep.value.deployedAt);
  const pos = priorDep.value.scanPosition ? ` · Scanned Run #${priorDep.value.scanPosition}` : '';
  return `${d.toLocaleString()}${pos}`;
});
</script>

<template>
  <!-- Prior Deployment Found -->
  <div 
    v-if="priorDep"
    @contextmenu.stop="emit('open-context-menu', $event, row, priorDep, env)"
    class="flex flex-col w-full text-xs select-none divide-y divide-amber-100 min-w-max bg-amber-50/20"
  >
    <!-- Row 1: Strict Prior Version / Tag & Run Link -->
    <div class="h-7 px-3 flex items-center justify-between gap-2 bg-amber-50/60 whitespace-nowrap">
      <div class="flex items-center gap-1.5 whitespace-nowrap">
        <svg 
          xmlns="http://www.w3.org/2000/svg" 
          class="h-3 w-3 text-amber-600 shrink-0" 
          fill="none" 
          viewBox="0 0 24 24" 
          stroke="currentColor"
          :title="priorDep.scanPosition ? `Previous deployment return point · Scanned build position #${priorDep.scanPosition}` : 'Previous deployment return point'"
        >
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
        </svg>
        <span 
          v-if="priorDep.version"
          class="font-mono font-bold text-xs text-amber-950 whitespace-nowrap"
          :title="`Previous Version/Tag: ${priorDep.version}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
        >
          v{{ priorDep.version }}
        </span>
        <span 
          v-else-if="priorDep.commitSha"
          class="font-mono font-bold text-xs text-slate-700 whitespace-nowrap"
          :title="`Previous Commit: ${priorDep.commitSha}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
        >
          {{ priorDep.commitSha.substring(0, 7) }}
        </span>
        <span v-if="priorDep.buildNumber" class="text-amber-800/80 font-mono text-[9px]">#{{ priorDep.buildNumber }}</span>
      </div>

      <!-- Open Prior Run in Azure DevOps (External Link Icon) -->
      <button 
        v-if="priorBuildUrl"
        @click.stop="openUrlInNewTab(priorBuildUrl)"
        class="p-0.5 rounded text-amber-700 hover:text-amber-900 hover:bg-amber-100/60 transition-colors shrink-0 cursor-pointer flex items-center justify-center"
        :title="priorDep.scanPosition ? `Open Previous Run #${priorDep.buildNumber} in Azure DevOps (Scanned build position: #${priorDep.scanPosition})` : `Open Previous Run #${priorDep.buildNumber} in Azure DevOps`"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
          <polyline points="15 3 21 3 21 9"></polyline>
          <line x1="10" y1="14" x2="21" y2="3"></line>
        </svg>
      </button>
    </div>

    <!-- Row 2: Strict Prior Commit SHA & PR -->
    <div class="h-6 px-3 flex items-center justify-between gap-2 text-[10px] whitespace-nowrap">
      <div class="flex items-center gap-1 whitespace-nowrap">
        <span class="text-slate-400 font-mono text-[9px]">SHA:</span>
        <button 
          v-if="priorDep.commitSha"
          @click.stop="commitUrl ? openUrlInNewTab(commitUrl) : null"
          class="font-mono text-indigo-700 hover:underline whitespace-nowrap"
          :title="`Previous Commit: ${priorDep.commitSha}`"
        >
          {{ priorDep.commitSha.substring(0, 7) }}
        </button>
        <span v-else class="text-slate-300">-</span>
      </div>

      <button 
        v-if="priorDep.prNumber"
        @click.stop="prUrl ? openUrlInNewTab(prUrl) : null"
        class="font-semibold text-violet-700 hover:underline shrink-0 text-[9px]"
        :title="`Previous Pull Request #${priorDep.prNumber}`"
      >
        PR #{{ priorDep.prNumber }}
      </button>
    </div>

    <!-- Row 3: Strict Prior Branch -->
    <div class="h-6 px-3 flex items-center gap-1 text-[10px] text-amber-950 whitespace-nowrap">
      <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-amber-700 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="18" cy="6" r="3"></circle>
        <circle cx="6" cy="18" r="3"></circle>
        <path d="M18 9a9 9 0 0 1-9 9"></path>
      </svg>
      <span class="whitespace-nowrap font-medium" :title="`Previous Branch: ${priorDep.branch}`">
        {{ cleanBranch || '-' }}
      </span>
    </div>

    <!-- Row 4: Strict Prior Date / Time & Author (e.g. 9d ago · Jun 14, 2026) -->
    <div class="h-6 px-3 flex items-center justify-between gap-2 text-[10px] text-amber-800/80 whitespace-nowrap">
      <span :title="formattedFullDate" class="text-[9px] font-mono whitespace-nowrap">
        {{ formatTimeAndDate(priorDep.deployedAt) }}
      </span>

      <PipelineAuthorBadge 
        :author="priorDep.author"
        :author-image-url="priorDep.authorImageUrl"
        :is-automated="priorDep.isAutomated"
        :trigger-reason="priorDep.triggerReason"
        compact
      />
    </div>

    <!-- Row 5: Prior Baseline / same SHA badge -->
    <div class="h-5 px-3 flex items-center justify-between text-[9px] bg-amber-100/30 whitespace-nowrap">
      <div v-if="devopsStore.priorRunMode === 'consecutive' && isSameRevision" class="flex items-center gap-1">
        <span class="px-1 py-0.2 text-[8px] rounded bg-amber-100 text-amber-800 font-semibold border border-amber-200">same SHA</span>
      </div>
      <div v-else class="flex items-center gap-1 text-amber-800/80 font-medium text-[9px]">
        <span class="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
        <span>Prior return point</span>
      </div>
    </div>
  </div>

  <!-- Empty Prior State: 5 Uniform Rows Matching Exact Baselines -->
  <div 
    v-else
    class="flex flex-col w-full text-xs select-none divide-y divide-slate-100 min-w-max bg-slate-50/20"
    :title="canScanMore 
      ? `No prior ${devopsStore.priorRunMode === 'distinct' ? 'distinct revision' : 'run'} found in the last ${currentRunsCount} scanned ${currentRunsCount === 1 ? 'run' : 'runs'} for '${row.pipelineName}' on '${env}'. Click 'Scan +50' to search older runs.` 
      : `No prior ${devopsStore.priorRunMode === 'distinct' ? 'distinct revision' : 'run'} found across all ${currentRunsCount} ${currentRunsCount === 1 ? 'run' : 'runs'} in pipeline history for '${row.pipelineName}' on '${env}'`"
  >
    <!-- Row 1: Header / Label -->
    <div class="h-7 px-3 flex items-center justify-center gap-1.5 text-slate-400 text-[10px] bg-slate-50/40 whitespace-nowrap">
      <span class="text-amber-500 font-bold font-mono text-xs select-none">&mdash;</span>
      <span class="font-medium text-slate-500">
        {{ devopsStore.priorRunMode === 'distinct' ? 'No prior revision' : 'No prior run' }}
      </span>
    </div>

    <!-- Row 2: In X runs -->
    <div class="h-6 px-3 flex items-center justify-center text-[9px] text-slate-400 italic whitespace-nowrap">
      in {{ currentRunsCount }} {{ currentRunsCount === 1 ? 'run' : 'runs' }}
    </div>

    <!-- Row 3: Scan +50 action -->
    <div class="h-6 px-3 flex items-center justify-center whitespace-nowrap">
      <button 
        v-if="canScanMore"
        type="button"
        @click.stop="handleDeepScan"
        :disabled="isScanning"
        class="px-2 py-0.5 rounded text-[9px] font-semibold text-blue-600 bg-blue-50/60 hover:bg-blue-100/80 border border-blue-200/70 transition-colors disabled:opacity-50 flex items-center gap-1 cursor-pointer whitespace-nowrap"
        :title="`Search up to ${nextDepth} older runs for '${row.pipelineName}' in Azure DevOps`"
      >
        <svg v-if="!isScanning" class="w-2.5 h-2.5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
        </svg>
        <svg v-else class="animate-spin h-2.5 w-2.5 text-blue-600" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
        <span>{{ isScanning ? 'Scanning...' : 'Scan +50' }}</span>
      </button>
      <span v-else class="text-[9px] text-slate-300 italic">Start of history</span>
    </div>

    <!-- Row 4: Spacer -->
    <div class="h-6 px-3 flex items-center justify-center text-slate-300 text-[10px] whitespace-nowrap">
      &mdash;
    </div>

    <!-- Row 5: Spacer -->
    <div class="h-5 px-3 flex items-center justify-center text-slate-300 text-[9px] bg-slate-50/40 whitespace-nowrap">
      &mdash;
    </div>
  </div>
</template>
