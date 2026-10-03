<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { useFilterStore } from '../../stores/filterStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useStagedStore } from '../../stores/stagedStore';
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
  (e: 'clear-filters'): void;
}>();

const filterStore = useFilterStore();
const { inspectionFilter } = storeToRefs(filterStore);

const settingsStore = useSettingsStore();
const { uiSettings } = storeToRefs(settingsStore);

const stagedStore = useStagedStore();
const { stagedChanges } = storeToRefs(stagedStore);

const dataStore = useDataStore();
const { vaultUris } = storeToRefs(dataStore);

const uiStateStore = useUiStateStore();
const { globalLoadingValues: loadingValues } = storeToRefs(uiStateStore);
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="font-bold text-slate-800 text-sm">4. Analyze & Inspect</div>
    <div class="flex items-center gap-2 w-full lg:max-w-xl">
      <button 
        v-if="hasInspectionsRun"
        @click="emit('clear-inspections')" 
        class="flex-[0.8] py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-lg text-sm font-semibold shadow-sm transition-colors flex items-center justify-center gap-2"
      >
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
        Clear Inspections
      </button>
      <button 
        v-else
        @click="emit('run-inspections')" 
        :disabled="loadingValues || vaultUris.length === 0 || filteredResultsLength === 0 || !hasFetchedValues"
        class="flex-[0.8] py-1.5 bg-slate-700 hover:bg-slate-800 text-white rounded-lg text-sm font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        Run Inspections
      </button>

      <button 
        @click="emit('show-report')" 
        :disabled="!hasInspectionsRun"
        class="flex-[0.5] py-1.5 bg-blue-50 border border-blue-200 hover:bg-blue-100 text-blue-700 rounded-lg text-sm font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:border-slate-200 disabled:text-slate-400 flex items-center justify-center gap-2"
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

      <label 
        class="flex items-center gap-1 text-xs text-amber-700 font-medium bg-amber-50 px-2 py-1.5 rounded border border-amber-200 transition-opacity"
        :class="stagedChanges.length === 0 ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer hover:bg-amber-100'"
      >
        <input 
          type="checkbox" 
          v-model="uiSettings.showStagedOnly" 
          class="rounded border-amber-300 text-amber-600 disabled:cursor-not-allowed" 
          :disabled="stagedChanges.length === 0"
        /> 
        Staged
      </label>
      <button @click="emit('clear-filters')" class="px-3 py-1.5 text-xs text-slate-600 border border-slate-300 rounded-lg hover:bg-slate-50">Clear Filters</button>
    </div>
  </div>
</template>
