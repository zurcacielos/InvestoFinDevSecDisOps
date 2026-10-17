<script setup lang="ts">
import { computed } from 'vue';
import type { EnvironmentDeploymentInfo, PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { formatTimeAndDate, getCommitUrl, getPrUrl, getBuildUrl } from '../../../utils/pipelineUrlHelpers';
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

const priorPrUrl = computed(() => {
  if (!priorDep.value) return null;
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
  const b = activeDep.value?.branch;
  return b ? b.replace('refs/heads/', '') : null;
});

const priorCleanBranch = computed(() => {
  const b = priorDep.value?.branch;
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
    <!-- Row 1: Strict Version / Tag & Run Link (Never Truncate) -->
    <div class="h-7 px-3 flex items-center justify-between gap-2 bg-white whitespace-nowrap">
      <div class="flex items-center gap-1.5 whitespace-nowrap">
        <DeploymentStatusIndicator :status="activeDep.status" :scan-position="activeDep.scanPosition" />
        <span 
          v-if="activeDep.version"
          class="font-mono font-bold text-xs text-slate-800 whitespace-nowrap"
          :title="`Version/Tag: ${activeDep.version}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          v{{ activeDep.version }}
        </span>
        <span 
          v-else-if="activeDep.commitSha"
          class="font-mono font-bold text-xs text-slate-800 whitespace-nowrap"
          :title="`Commit: ${activeDep.commitSha}${activeDep.scanPosition ? ` · Scanned Run #${activeDep.scanPosition}` : ''}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </span>
      </div>

      <!-- Open Run in Azure DevOps (External Link Icon) -->
      <button 
        v-if="activeDep.buildUrl"
        @click.stop="openUrlInNewTab(activeDep.buildUrl!)"
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

    <!-- Row 2: Strict Commit SHA & PR -->
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

    <!-- Row 3: Strict Branch -->
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

    <!-- Row 4: Strict Date / Time & Author (e.g. 5d ago · Jun 18, 2026) -->
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

    <!-- Row 6: Prior Release Tag (same font size as active) -->
    <div 
      v-if="devopsStore.showRollbackTarget"
      class="h-7 px-3 flex items-center justify-between gap-2 border-t border-slate-200 bg-amber-50/40 whitespace-nowrap"
    >
      <template v-if="priorDep">
        <div class="flex items-center gap-1.5 whitespace-nowrap">
          <svg 
            xmlns="http://www.w3.org/2000/svg" 
            class="h-3 w-3 text-slate-400 shrink-0" 
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
            class="font-mono font-bold text-xs text-slate-800 whitespace-nowrap transition-colors"
            :class="priorBuildUrl ? 'hover:underline hover:text-blue-600 cursor-pointer' : 'cursor-default'"
            :title="priorBuildUrl ? `Open Previous Run #${priorDep.buildNumber} in Azure DevOps (v${priorDep.version})${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}` : `Previous Version: v${priorDep.version}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
          >
            v{{ priorDep.version }}
          </button>
          <button 
            v-else-if="priorDep.commitSha"
            @click.stop="priorCommitUrl ? openUrlInNewTab(priorCommitUrl) : null"
            class="font-mono font-bold text-xs text-slate-800 hover:underline hover:text-blue-600 whitespace-nowrap cursor-pointer"
            :title="`Previous Commit: ${priorDep.commitSha}${priorDep.scanPosition ? ` · Scanned Run #${priorDep.scanPosition}` : ''}`"
          >
            {{ priorDep.commitSha.substring(0, 7) }}
          </button>
        </div>

        <!-- Open Prior Run in Azure DevOps (External Link Icon) -->
        <button 
          v-if="priorBuildUrl"
          @click.stop="openUrlInNewTab(priorBuildUrl)"
          class="p-0.5 rounded text-slate-400 hover:text-blue-600 hover:bg-slate-100 transition-colors shrink-0 cursor-pointer flex items-center justify-center"
          :title="priorDep.scanPosition ? `Open Previous Run #${priorDep.buildNumber} in Azure DevOps (Scanned build position: #${priorDep.scanPosition})` : `Open Previous Run #${priorDep.buildNumber} in Azure DevOps`"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
            <polyline points="15 3 21 3 21 9"></polyline>
            <line x1="10" y1="14" x2="21" y2="3"></line>
          </svg>
        </button>
      </template>

      <!-- Empty State: Row 6 -->
      <template v-else>
        <div 
          class="flex items-center gap-1.5 text-slate-400 whitespace-nowrap"
          :title="canScanMore 
            ? `No prior deployment found in the last ${currentRunsCount} scanned ${currentRunsCount === 1 ? 'run' : 'runs'} for '${row.pipelineName}' on '${env}'. Click 'Scan +50' to search older runs.` 
            : `No prior deployment found across all ${currentRunsCount} ${currentRunsCount === 1 ? 'run' : 'runs'} in pipeline history for '${row.pipelineName}' on '${env}'`"
        >
          <span class="text-slate-400 font-bold font-mono text-xs select-none">&mdash;</span>
          <span class="italic text-slate-400 whitespace-nowrap text-[10px]">
            No prior run
          </span>
        </div>
      </template>
    </div>

    <!-- Row 7: Prior Deployed Date (directly below release of prior) -->
    <div 
      v-if="devopsStore.showRollbackTarget"
      class="h-6 px-3 flex items-center justify-between gap-2 text-[10px] text-slate-500 bg-amber-50/40 whitespace-nowrap"
    >
      <template v-if="priorDep">
        <span 
          class="text-slate-500 font-mono shrink-0 whitespace-nowrap text-[9px]"
          :title="priorDep.deployedAt ? new Date(priorDep.deployedAt).toLocaleString() : ''"
        >
          {{ formatTimeAndDate(priorDep.deployedAt) }}
        </span>
        <PipelineAuthorBadge 
          :author="priorDep.author"
          :author-image-url="priorDep.authorImageUrl"
          :is-automated="priorDep.isAutomated"
          :trigger-reason="priorDep.triggerReason"
          compact
        />
      </template>
      <template v-else>
        <span class="text-[9px] text-slate-400 italic whitespace-nowrap">
          in {{ currentRunsCount }} {{ currentRunsCount === 1 ? 'run' : 'runs' }}
        </span>
        <button 
          v-if="canScanMore"
          type="button"
          @click.stop="handleDeepScan"
          :disabled="isScanning"
          class="px-1.5 py-0.5 rounded text-[9px] font-semibold text-blue-600 hover:text-blue-800 hover:bg-blue-50 border border-blue-200/70 transition-colors disabled:opacity-50 flex items-center gap-1 cursor-pointer shrink-0"
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
      </template>
    </div>

    <!-- Row 8: Prior Branch & SHA/PR -->
    <div 
      v-if="devopsStore.showRollbackTarget"
      class="h-6 px-3 flex items-center justify-between gap-1 text-[10px] whitespace-nowrap text-slate-700 bg-amber-50/40"
    >
      <div v-if="priorDep" class="flex items-center gap-1 min-w-0">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="18" cy="6" r="3"></circle>
          <circle cx="6" cy="18" r="3"></circle>
          <path d="M18 9a9 9 0 0 1-9 9"></path>
        </svg>
        <span class="whitespace-nowrap font-medium" :title="`Previous Branch: ${priorDep.branch}`">
          {{ priorCleanBranch || '-' }}
        </span>
      </div>
      <div v-if="priorDep && (priorDep.commitSha || priorDep.prNumber)" class="flex items-center gap-1 shrink-0">
        <button 
          v-if="priorDep.commitSha && priorDep.version"
          @click.stop="priorCommitUrl ? openUrlInNewTab(priorCommitUrl) : null"
          class="font-mono text-slate-700 hover:text-blue-600 hover:underline whitespace-nowrap"
          :title="`Previous Commit: ${priorDep.commitSha}`"
        >
          {{ priorDep.commitSha.substring(0, 7) }}
        </button>
        <span 
          v-if="priorDep.buildNumber && isSameRevision"
          class="text-slate-400 font-mono text-[9px] whitespace-nowrap"
          title="Build number"
        >
          (build #{{ priorDep.buildNumber }})
        </span>
        <button 
          v-if="priorDep.prNumber"
          @click.stop="priorPrUrl ? openUrlInNewTab(priorPrUrl) : null"
          class="font-semibold text-slate-600 hover:text-blue-600 hover:underline shrink-0 text-[9px]"
          :title="`Previous Pull Request #${priorDep.prNumber}`"
        >
          PR #{{ priorDep.prNumber }}
        </button>
      </div>
      <div v-else-if="!priorDep" class="flex items-center gap-1 text-slate-300 select-none">
        &mdash;
      </div>
    </div>
  </div>

  <!-- Empty State with On-Demand Deep Scan -->
  <EmptyEnvironmentCell v-else :row="row" :env="env" />
</template>
