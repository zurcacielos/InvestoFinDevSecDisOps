<script setup lang="ts">
import { computed } from 'vue';
import { useDevopsDataStore } from '../../../stores/devopsDataStore';
import type { PipelineMatrixRow } from '../../../stores/devopsDataStore';

const props = defineProps<{
  row: PipelineMatrixRow;
  env: string;
}>();

const devopsStore = useDevopsDataStore();

const isScanning = computed(() => {
  return devopsStore.deepScanningPipelineIds.includes(props.row.pipelineId) ||
         devopsStore.isSinglePipelineRefreshing(props.row.pipelineId);
});

const isUnscanned = computed(() => {
  return props.row.scannedRunsCount === 0 || 
         (props.row.scannedRunsCount === undefined && (!props.row.recentRuns || props.row.recentRuns.length === 0));
});

const currentRunsCount = computed(() => {
  return props.row.scannedRunsCount ?? props.row.recentRuns?.length ?? devopsStore.scanDepth;
});

const nextDepth = computed(() => {
  if (isUnscanned.value) return devopsStore.scanDepth;
  return Math.min(currentRunsCount.value + 50, 200);
});

const handleScanAction = () => {
  if (isUnscanned.value) {
    devopsStore.refreshSinglePipeline(props.row.pipelineId);
  } else {
    devopsStore.deepScanPipeline(props.row.pipelineId, 50);
  }
};
</script>

<template>
  <div 
    v-if="isScanning" 
    class="h-full min-h-[48px] flex flex-col items-center justify-center py-2 px-1 text-blue-600 gap-1 select-none"
  >
    <svg class="animate-spin h-3.5 w-3.5 text-blue-500" fill="none" viewBox="0 0 24 24">
      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
    </svg>
    <span class="text-[9px] font-semibold text-blue-600">{{ isUnscanned ? 'Scanning...' : 'Scanning +50...' }}</span>
  </div>

  <div 
    v-else
    class="h-full min-h-[48px] flex flex-col items-center justify-center py-2 px-1 text-slate-300 hover:bg-slate-50/70 rounded transition-colors group/empty"
    :title="isUnscanned 
      ? `Not yet scanned. Click to scan recent ${devopsStore.scanDepth} runs.` 
      : `No deployment found in the last ${currentRunsCount} runs for '${row.pipelineName}' on '${env}'. Click 'Scan deeper' to search up to ${nextDepth} runs.`"
  >
    <span class="text-xs text-slate-300 font-mono select-none leading-none">&mdash;</span>
    <button 
      type="button"
      @click.stop="handleScanAction"
      class="text-[9px] font-medium text-blue-500 hover:text-blue-700 hover:underline transition-colors mt-1 cursor-pointer select-none"
      :title="isUnscanned 
        ? `Scan recent ${devopsStore.scanDepth} runs for ${row.pipelineName}` 
        : `Search up to ${nextDepth} older runs for ${row.pipelineName} in Azure DevOps`"
    >
      {{ isUnscanned ? 'Scan pipeline' : 'Scan deeper' }}
    </button>
  </div>
</template>
