<script setup lang="ts">
import { computed } from 'vue';
import type { EnvironmentDeploymentInfo, PipelineMatrixRow } from '../../../stores/devopsDataStore';
import { useSettingsStore } from '../../../stores/settingsStore';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import { openUrlInNewTab } from '../../../utils/urlOpener';
import { formatTimeAgo, getCommitUrl, getPrUrl, getBuildUrl } from '../../../utils/pipelineUrlHelpers';
import PipelineAuthorBadge from '../PipelineAuthorBadge.vue';

const props = defineProps<{
  row: PipelineMatrixRow;
  deployment: EnvironmentDeploymentInfo;
  env: string;
}>();

const emit = defineEmits<{
  (e: 'open-context-menu', event: MouseEvent, row: PipelineMatrixRow, cell: EnvironmentDeploymentInfo, env: string): void;
}>();

const settingsStore = useSettingsStore();
const devopsStore = useDevopsDataStore();

const priorBuildUrl = computed(() => {
  return getBuildUrl(
    devopsStore.organization,
    devopsStore.project,
    props.deployment.buildId,
    props.deployment.buildUrl
  );
});

const commitUrl = computed(() => {
  return getCommitUrl(
    props.row.repository,
    props.deployment.commitSha,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
});

const prUrl = computed(() => {
  return getPrUrl(
    props.row.repository,
    props.deployment.prNumber,
    devopsStore.organization,
    devopsStore.project,
    settingsStore.uiSettings.githubBaseUrl,
    settingsStore.uiSettings.useGithubOrg,
    settingsStore.uiSettings.githubOrg
  );
});

const formattedFullDate = computed(() => {
  if (!props.deployment.deployedAt) return '';
  const d = new Date(props.deployment.deployedAt);
  return d.toLocaleString();
});
</script>

<template>
  <div 
    @contextmenu.stop="emit('open-context-menu', $event, row, deployment, env)"
    class="flex items-center justify-between gap-1.5 px-2 py-1 rounded bg-amber-50/70 hover:bg-amber-100/70 border border-amber-200/70 text-[10px] text-amber-900 transition-colors shadow-2xs select-none"
    :title="`Previous run #${deployment.buildNumber} deployed ${formattedFullDate}`"
  >
    <!-- Left: Return Icon + Previous Version / Commit -->
    <div class="flex items-center gap-1.5 min-w-0 flex-wrap">
      <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-amber-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
      </svg>

      <!-- Previous Version Tag (Clickable to open previous run in Azure DevOps) -->
      <button 
        v-if="deployment.version"
        @click.stop="priorBuildUrl ? openUrlInNewTab(priorBuildUrl) : null"
        class="font-mono font-bold text-amber-950 truncate px-1 py-0.2 rounded bg-white/70 border border-amber-200/70 transition-colors"
        :class="priorBuildUrl ? 'hover:underline hover:text-amber-800 hover:bg-amber-100 cursor-pointer' : 'cursor-default'"
        :title="priorBuildUrl ? `Open Previous Run #${deployment.buildNumber} in Azure DevOps (Tag: v${deployment.version})` : `Previous Version/Tag: v${deployment.version}`"
      >
        v{{ deployment.version }}
      </button>

      <!-- Previous Commit Link -->
      <button 
        v-if="deployment.commitSha"
        @click.stop="commitUrl ? openUrlInNewTab(commitUrl) : null"
        class="font-mono text-[9px] px-1 py-0.2 rounded bg-white/90 text-indigo-700 hover:underline border border-amber-200/60"
        :title="`Previous Commit: ${deployment.commitSha}`"
      >
        {{ deployment.commitSha.substring(0, 7) }}
      </button>

      <!-- Previous PR Link -->
      <button 
        v-if="deployment.prNumber"
        @click.stop="prUrl ? openUrlInNewTab(prUrl) : null"
        class="text-[9px] font-semibold px-1 py-0.2 rounded bg-violet-100/90 text-violet-800 hover:underline border border-violet-200/50"
        :title="`Previous PR #${deployment.prNumber}`"
      >
        PR #{{ deployment.prNumber }}
      </button>
    </div>

    <!-- Right: Elapsed time with full date on hover & Author avatar -->
    <div class="flex items-center gap-1.5 shrink-0 text-slate-500">
      <span 
        :title="formattedFullDate" 
        class="text-[9px] font-mono text-amber-800/80 cursor-help"
      >
        {{ formatTimeAgo(deployment.deployedAt) }}
      </span>

      <!-- Compact Author Badge -->
      <PipelineAuthorBadge 
        :author="deployment.author"
        :author-image-url="deployment.authorImageUrl"
        :is-automated="deployment.isAutomated"
        :trigger-reason="deployment.triggerReason"
        compact
      />
    </div>
  </div>
</template>
