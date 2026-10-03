<script setup lang="ts">
import { storeToRefs } from 'pinia';

import { useFilterStore } from '../../stores/filterStore';
import { useDataStore } from '../../stores/dataStore';
import { useUiStateStore } from '../../stores/uiStateStore';

const props = defineProps<{
  hasFetchedValues: boolean;
  filteredResultsLength: number;
  hasInspectionsRun: boolean;
  inspectionCounts: Record<string, number>;
}>();

const emit = defineEmits<{
  (e: 'run-inspections'): void;
  (e: 'clear-inspections'): void;
  (e: 'show-report'): void;
}>();

const filterStore = useFilterStore();
const { inspectionFilter } = storeToRefs(filterStore);


const dataStore = useDataStore();
const { vaultUris } = storeToRefs(dataStore);

const uiStateStore = useUiStateStore();
const { globalLoadingValues: loadingValues } = storeToRefs(uiStateStore);
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="font-bold text-slate-800 text-sm">4. Analyze & Inspect</div>
    <div class="flex items-center gap-2 w-full sm:w-auto flex-wrap">
      <button 
        v-if="hasInspectionsRun"
        @click="emit('clear-inspections')" 
        class="w-full sm:w-auto px-6 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-lg text-sm font-semibold shadow-sm transition-colors flex items-center justify-center gap-2"
      >
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
        Clear Inspections
      </button>
      <button 
        v-else
        @click="emit('run-inspections')" 
        :disabled="loadingValues || vaultUris.length === 0 || filteredResultsLength === 0 || !hasFetchedValues"
        class="w-full sm:w-auto px-8 py-1.5 bg-slate-700 hover:bg-slate-800 text-white rounded-lg text-sm font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        Run Inspections
      </button>

      <button 
        @click="emit('show-report')" 
        :disabled="!hasInspectionsRun"
        class="w-full sm:w-auto px-6 py-1.5 bg-blue-50 border border-blue-200 hover:bg-blue-100 text-blue-700 rounded-lg text-sm font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:border-slate-200 disabled:text-slate-400 flex items-center justify-center gap-2"
      >
        See Report
      </button>
    </div>
    <div class="flex flex-wrap items-center gap-3 mt-auto">
      <select 
        v-model="inspectionFilter"
        :disabled="!hasInspectionsRun || inspectionCounts.Any === 0"
        class="bg-white border border-slate-300 rounded-lg pl-3 pr-8 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 text-slate-700 disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed"
      >
        <option value="None">All Secrets</option>
        <option value="Any">{{ hasInspectionsRun ? `Any warning (${inspectionCounts.Any})` : 'Any warning' }}</option>
        <option value="Critical">{{ hasInspectionsRun ? `Critical (${inspectionCounts.Critical})` : 'Critical' }}</option>
        <option value="High">{{ hasInspectionsRun ? `High (${inspectionCounts.High})` : 'High' }}</option>
        <option value="Medium">{{ hasInspectionsRun ? `Medium (${inspectionCounts.Medium})` : 'Medium' }}</option>
        <option value="Low">{{ hasInspectionsRun ? `Low (${inspectionCounts.Low})` : 'Low' }}</option>
      </select>
    </div>
  </div>
</template>
