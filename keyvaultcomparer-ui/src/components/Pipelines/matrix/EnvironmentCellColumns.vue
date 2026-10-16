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

const getCommitLink = (dep: EnvironmentDeploymentInfo | null) => {
  if (!dep?.commitSha) return null;
  return getCommitUrl(
    props.row.repository,
    dep.commitSha,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
};

const getPrLink = (dep: EnvironmentDeploymentInfo | null) => {
  if (!dep?.prNumber) return null;
  return getPrUrl(
    props.row.repository,
    dep.prNumber,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
};

const getCleanBranch = (dep: EnvironmentDeploymentInfo | null) => {
  const b = dep?.branch;
  return b ? b.replace('refs/heads/', '') : null;
};
</script>

<template>
  <div 
    v-if="activeDep"
    class="flex w-full h-full divide-x divide-slate-200 text-xs select-none min-w-[280px]"
  >
    <!-- Left Column: Prior Run (N-1) (Side-by-Side) -->
    <div 
      v-if="priorDep"
      @contextmenu.stop="emit('open-context-menu', $event, row, priorDep, env)"
      class="flex-1 p-2 flex flex-col gap-1 min-w-0 bg-amber-50/30"
    >
      <!-- Sub-header: Prior Icon & Run Link -->
      <div class="flex items-center justify-between gap-1 text-[10px] pb-1 border-b border-amber-200/60 whitespace-nowrap">
        <div class="flex items-center gap-1 min-w-0">
          <svg 
            xmlns="http://www.w3.org/2000/svg" 
            class="h-2.5 w-2.5 text-amber-600 shrink-0" 
            fill="none" 
            viewBox="0 0 24 24" 
            stroke="currentColor"
            :title="priorDep.scanPosition ? `Previous deployment return point · Scanned build position #${priorDep.scanPosition}` : 'Previous deployment return point'"
          >
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
          </svg>
          <span class="font-bold text-amber-900 text-[10px]">Prior</span>
        </div>
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

      <!-- Line 2: Prior Version / Tag -->
      <div class="flex items-center gap-1 min-w-0 py-0.5 whitespace-nowrap">
        <button 
          v-if="priorDep.version"
          @click.stop="priorBuildUrl ? openUrlInNewTab(priorBuildUrl) : null"
          class="font-mono font-bold text-xs text-amber-950 bg-white/90 px-1.5 py-0.2 rounded border border-amber-200 truncate transition-colors"
          :class="priorBuildUrl ? 'hover:underline hover:text-amber-800 cursor-pointer' : 'cursor-default'"
          :title="priorBuildUrl ? `Open Previous Run #${priorDep.buildNumber} in Azure DevOps (v${priorDep.version})${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}` : `Previous Version/Tag: ${priorDep.version}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
        >
          v{{ priorDep.version }}
        </button>
        <span 
          v-else-if="priorDep.commitSha"
          class="font-mono font-bold text-xs text-slate-700 bg-white px-1 py-0.2 rounded border border-slate-200 truncate"
          :title="`Previous Commit: ${priorDep.commitSha}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
        >
          {{ priorDep.commitSha.substring(0, 7) }}
        </span>
        <span v-else class="text-slate-300 font-mono text-xs">-</span>
      </div>

      <!-- Line 3: Prior Commit SHA & PR (SHA on one line) -->
      <div class="flex items-center justify-between gap-1 text-[10px] whitespace-nowrap min-w-0">
        <div class="flex items-center gap-1 min-w-0">
          <span class="text-slate-400 font-mono text-[9px]">SHA:</span>
          <button 
            v-if="priorDep.commitSha"
            @click.stop="getCommitLink(priorDep) ? openUrlInNewTab(getCommitLink(priorDep)!) : null"
            class="font-mono text-indigo-700 hover:underline truncate"
            :title="`Previous Commit: ${priorDep.commitSha}`"
          >
            {{ priorDep.commitSha.substring(0, 7) }}
          </button>
          <span v-else class="text-slate-300">-</span>
        </div>
        <button 
          v-if="priorDep.prNumber"
          @click.stop="getPrLink(priorDep) ? openUrlInNewTab(getPrLink(priorDep)!) : null"
          class="font-semibold px-1 py-0.2 rounded bg-violet-100 text-violet-800 hover:underline border border-violet-200 text-[9px] shrink-0"
          :title="`Previous Pull Request #${priorDep.prNumber}`"
        >
          PR #{{ priorDep.prNumber }}
        </button>
      </div>

      <!-- Line 4: Prior Branch on its own line with branch icon -->
      <div class="flex items-center gap-1 text-[10px] text-amber-950 min-w-0 whitespace-nowrap">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-amber-700 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="18" cy="6" r="3"></circle>
          <circle cx="6" cy="18" r="3"></circle>
          <path d="M18 9a9 9 0 0 1-9 9"></path>
        </svg>
        <span class="truncate font-medium" :title="`Previous Branch: ${priorDep.branch}`">
          {{ getCleanBranch(priorDep) || '-' }}
        </span>
      </div>

      <!-- Line 5: Prior Time & Author -->
      <div class="flex items-center justify-between text-[10px] text-slate-400 mt-auto pt-1 border-t border-amber-200/50 whitespace-nowrap">
        <span class="text-[9px] font-mono text-amber-800/80" :title="priorDep.deployedAt">
          {{ formatTimeAgo(priorDep.deployedAt) }}
        </span>
        <PipelineAuthorBadge 
          :author="priorDep.author"
          :author-image-url="priorDep.authorImageUrl"
          :is-automated="priorDep.isAutomated"
          :trigger-reason="priorDep.triggerReason"
          compact
        />
      </div>
    </div>

    <!-- If No Prior Run -->
    <div 
      v-else
      class="flex-1 p-2 flex flex-col items-center justify-center text-center bg-slate-50/30 min-w-0 gap-1"
      :title="canScanMore 
        ? `No prior run found in the last ${currentRunsCount} scanned ${currentRunsCount === 1 ? 'run' : 'runs'} for '${row.pipelineName}' on '${env}'. Click 'Scan +50' to search older runs.` 
        : `No prior run found across all ${currentRunsCount} ${currentRunsCount === 1 ? 'run' : 'runs'} in pipeline history for '${row.pipelineName}' on '${env}'`"
    >
      <div class="flex items-center gap-1.5 text-slate-400 text-[10px]">
        <span class="text-amber-500 font-bold font-mono text-xs select-none">&mdash;</span>
        <span class="font-medium text-slate-500">No prior run</span>
      </div>
      <span class="text-[9px] text-slate-400 italic">
        in {{ currentRunsCount }} {{ currentRunsCount === 1 ? 'run' : 'runs' }}
      </span>
      <button 
        v-if="canScanMore"
        type="button"
        @click.stop="handleDeepScan"
        :disabled="isScanning"
        class="mt-1 px-2 py-0.5 rounded text-[9px] font-semibold text-blue-600 bg-blue-50/60 hover:bg-blue-100/80 border border-blue-200/70 transition-colors disabled:opacity-50 flex items-center gap-1 cursor-pointer whitespace-nowrap"
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
    </div>

    <!-- Right Column: Current Active Run (N) -->
    <div 
      @contextmenu.stop="emit('open-context-menu', $event, row, activeDep, env)"
      class="flex-1 p-2 flex flex-col gap-1 min-w-0 bg-white"
    >
      <!-- Sub-header: Current Status & Run Link -->
      <div class="flex items-center justify-between gap-1.5 text-[10px] pb-1 border-b border-slate-100 min-w-0 whitespace-nowrap">
        <div class="flex items-center gap-1 shrink-0">
          <DeploymentStatusIndicator :status="activeDep.status" :scan-position="activeDep.scanPosition" />
          <span class="font-bold text-slate-800 text-[10px] shrink-0">Active</span>
        </div>
        <button 
          v-if="activeDep.buildUrl"
          @click.stop="openUrlInNewTab(activeDep.buildUrl!)"
          class="p-0.5 rounded text-blue-600 hover:text-blue-800 hover:bg-blue-50 transition-colors shrink-0 cursor-pointer flex items-center justify-center"
          :title="activeDep.scanPosition ? `Open Run #${activeDep.buildNumber} in Azure DevOps (Scanned build position: #${activeDep.scanPosition})` : `Open Run #${activeDep.buildNumber} in Azure DevOps`"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
            <polyline points="15 3 21 3 21 9"></polyline>
            <line x1="10" y1="14" x2="21" y2="3"></line>
          </svg>
        </button>
      </div>

      <!-- Line 2: Version / Tag Anchor -->
      <div class="flex items-center gap-1 min-w-0 py-0.5 whitespace-nowrap">
        <span 
          v-if="activeDep.version"
          class="font-mono font-bold text-xs text-violet-700 bg-violet-50 px-1.5 py-0.2 rounded border border-violet-200 truncate"
          :title="`Version/Tag: ${activeDep.version}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          v{{ activeDep.version }}
        </span>
        <span 
          v-else-if="activeDep.commitSha"
          class="font-mono font-bold text-xs text-slate-700 bg-slate-100 px-1 py-0.2 rounded border border-slate-200 truncate"
          :title="`Commit: ${activeDep.commitSha}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </span>
        <span v-else class="text-slate-300 font-mono text-xs">-</span>
      </div>

      <!-- Line 3: Commit SHA & PR (SHA on one line) -->
      <div class="flex items-center justify-between gap-1 text-[10px] whitespace-nowrap min-w-0">
        <div class="flex items-center gap-1 min-w-0">
          <span class="text-slate-400 font-mono text-[9px]">SHA:</span>
          <button 
            v-if="activeDep.commitSha"
            @click.stop="getCommitLink(activeDep) ? openUrlInNewTab(getCommitLink(activeDep)!) : null"
            class="font-mono text-indigo-700 hover:underline truncate"
            :title="`Commit: ${activeDep.commitSha}`"
          >
            {{ activeDep.commitSha.substring(0, 7) }}
          </button>
          <span v-else class="text-slate-300">-</span>
        </div>
        <button 
          v-if="activeDep.prNumber"
          @click.stop="getPrLink(activeDep) ? openUrlInNewTab(getPrLink(activeDep)!) : null"
          class="font-semibold px-1 py-0.2 rounded bg-violet-100 text-violet-800 hover:underline border border-violet-200 text-[9px] shrink-0"
          :title="`Pull Request #${activeDep.prNumber}`"
        >
          PR #{{ activeDep.prNumber }}
        </button>
      </div>

      <!-- Line 4: Active Branch on its own line with branch icon -->
      <div class="flex items-center gap-1 text-[10px] text-slate-700 min-w-0 whitespace-nowrap">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="18" cy="6" r="3"></circle>
          <circle cx="6" cy="18" r="3"></circle>
          <path d="M18 9a9 9 0 0 1-9 9"></path>
        </svg>
        <span class="truncate font-medium" :title="`Branch: ${activeDep.branch}`">
          {{ getCleanBranch(activeDep) || '-' }}
        </span>
      </div>

      <!-- Line 5: Active Time & Author -->
      <div class="flex items-center justify-between text-[10px] text-slate-400 mt-auto pt-1 border-t border-slate-100 whitespace-nowrap">
        <span class="text-[9px] font-mono text-slate-500" :title="activeDep.deployedAt">
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
    </div>
  </div>

  <!-- Empty State with On-Demand Deep Scan -->
  <EmptyEnvironmentCell v-else :row="row" :env="env" />
</template>
