<script setup lang="ts">
import { computed } from 'vue';
import type { EnvironmentDeploymentInfo, PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { formatTimeAgo, getCommitUrl, getPrUrl } from '../../../utils/pipelineUrlHelpers';
import DeploymentStatusIndicator from './DeploymentStatusIndicator.vue';
import PipelineAuthorBadge from '../PipelineAuthorBadge.vue';
import RollbackRibbon from './RollbackRibbon.vue';

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
  return d.toLocaleString();
});

// Upstream sync status check
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
    class="flex flex-col gap-1.5 p-2 rounded-lg bg-white border border-slate-200/90 shadow-2xs hover:border-blue-300 hover:shadow-xs transition-all"
  >
    <!-- Line 1: Primary Version Anchor + Status + Run # Link -->
    <div class="flex items-center justify-between gap-1.5 min-w-0">
      <div class="flex items-center gap-1.5 min-w-0">
        <DeploymentStatusIndicator :status="activeDep.status" />

        <!-- Primary Version / Tag -->
        <span 
          v-if="activeDep.version"
          class="font-mono font-bold text-xs text-violet-700 bg-violet-50/90 px-1.5 py-0.5 rounded border border-violet-200/80 truncate shadow-2xs"
          :title="`Version/Tag: ${activeDep.version}`"
        >
          v{{ activeDep.version }}
        </span>
        <span 
          v-else-if="activeDep.commitSha"
          class="font-mono font-bold text-xs text-slate-700 bg-slate-100 px-1.5 py-0.5 rounded border border-slate-200 truncate"
          :title="`Commit: ${activeDep.commitSha}`"
        >
          {{ activeDep.commitSha.substring(0, 7) }}
        </span>

        <!-- Operation badge (e.g. RELEASE) -->
        <span 
          v-if="activeDep.operation"
          class="px-1 py-0.2 rounded text-[9px] font-semibold uppercase tracking-wider bg-slate-100 text-slate-600 border border-slate-200 shrink-0"
        >
          {{ activeDep.operation }}
        </span>
      </div>

      <!-- Build Number Link -->
      <button 
        v-if="activeDep.buildUrl"
        @click.stop="openUrlInNewTab(activeDep.buildUrl!)"
        class="text-[11px] font-mono text-blue-600 hover:underline hover:text-blue-800 shrink-0"
        :title="`Open Run #${activeDep.buildNumber} in Azure DevOps`"
      >
        #{{ activeDep.buildNumber }}
      </button>
    </div>

    <!-- Line 2: Branch Badge + Commit SHA + PR -->
    <div class="flex items-center gap-1 flex-wrap text-[10px]">
      <!-- Branch -->
      <span 
        v-if="cleanBranch"
        class="inline-flex items-center gap-1 font-medium text-slate-700 bg-slate-100 hover:bg-slate-200/70 px-1.5 py-0.5 rounded border border-slate-200 max-w-[130px] truncate transition-colors cursor-default"
        :title="`Branch: ${activeDep.branch}`"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-500 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="18" cy="6" r="3"></circle>
          <circle cx="6" cy="18" r="3"></circle>
          <path d="M18 9a9 9 0 0 1-9 9"></path>
        </svg>
        <span class="truncate">{{ cleanBranch }}</span>
      </span>

      <!-- Commit Link -->
      <button 
        v-if="activeDep.commitSha"
        @click.stop="commitUrl ? openUrlInNewTab(commitUrl) : null"
        class="font-mono px-1.5 py-0.5 rounded transition-colors"
        :class="commitUrl ? 'bg-indigo-50 text-indigo-700 hover:bg-indigo-100 hover:underline border border-indigo-100' : 'bg-slate-100 text-slate-600'"
        :title="`Commit: ${activeDep.commitSha}`"
      >
        {{ activeDep.commitSha.substring(0, 7) }}
      </button>

      <!-- PR Link -->
      <button 
        v-if="activeDep.prNumber"
        @click.stop="prUrl ? openUrlInNewTab(prUrl) : null"
        class="font-semibold px-1.5 py-0.5 rounded bg-violet-100 text-violet-800 hover:bg-violet-200 transition-colors border border-violet-200"
        :title="`Pull Request #${activeDep.prNumber}`"
      >
        PR #{{ activeDep.prNumber }}
      </button>
    </div>

    <!-- Line 3: Deployed Time Ago + Compact Author Badge + Upstream Lag -->
    <div class="flex items-center justify-between text-[10px] text-slate-400 pt-1 border-t border-slate-100">
      <div class="flex items-center gap-1.5">
        <span 
          :title="formattedFullDate" 
          class="text-[9px] font-mono text-slate-500 cursor-help"
        >
          {{ formatTimeAgo(activeDep.deployedAt) }}
        </span>

        <!-- Compact Author Badge -->
        <PipelineAuthorBadge 
          :author="activeDep.author"
          :author-image-url="activeDep.authorImageUrl"
          :is-automated="activeDep.isAutomated"
          :trigger-reason="activeDep.triggerReason"
          compact
        />
      </div>

      <!-- Upstream Lag Dot -->
      <div v-if="lagStatus" class="flex items-center gap-1" :title="lagStatus.label">
        <span 
          class="w-1.5 h-1.5 rounded-full" 
          :class="lagStatus.isSynced ? 'bg-emerald-500' : 'bg-amber-500'"
        ></span>
        <span 
          class="text-[9px] font-medium"
          :class="lagStatus.isSynced ? 'text-emerald-700' : 'text-amber-700 font-semibold'"
        >
          {{ lagStatus.isSynced ? 'Sync' : 'Lag' }}
        </span>
      </div>
    </div>

    <!-- Line 4: Compact Rollback Ribbon (when enabled) -->
    <div v-if="devopsStore.showRollbackTarget" class="mt-0.5">
      <RollbackRibbon 
        v-if="priorDep"
        :row="row"
        :deployment="priorDep"
        :env="env"
        @open-context-menu="(e, r, d, ev) => emit('open-context-menu', e, r, d, ev)"
      />
      <div 
        v-else
        class="px-2 py-0.5 rounded bg-slate-50/80 border border-dashed border-slate-200 text-center text-[9px] text-slate-400 italic"
      >
        No prior version in {{ devopsStore.scanDepth }} runs
      </div>
    </div>
  </div>

  <!-- Empty Environment Cell -->
  <div v-else class="h-full flex items-center justify-center py-4 text-slate-300">
    <span class="text-xs">&mdash;</span>
  </div>
</template>
