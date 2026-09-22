<script setup lang="ts">
import { ref } from 'vue';
import { storeToRefs } from 'pinia';
import { useFilterStore } from '../../stores/filterStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useStagedStore } from '../../stores/stagedStore';
import { useDataStore } from '../../stores/dataStore';
import { useUiStateStore } from '../../stores/uiStateStore';

const props = defineProps<{
  hasFetchedValues: boolean;
  filteredResultsLength: number;
  allSortedNamesLength: number;
  hasInspectionsRun: boolean;
  inspectionCounts: Record<string, number>;
}>();

const emit = defineEmits<{
  (e: 'fetch-comparison'): void;
  (e: 'run-inspections'): void;
  (e: 'clear-inspections'): void;
  (e: 'show-report'): void;
  (e: 'clear-filters'): void;
  (e: 'show-regex-help'): void;
}>();

const filterStore = useFilterStore();
const { nameFilter, availableRecentFilters, inspectionFilter } = storeToRefs(filterStore);
const hoveredFilter = ref<string | null>(null);

const settingsStore = useSettingsStore();
const { uiSettings } = storeToRefs(settingsStore);

const stagedStore = useStagedStore();
const { stagedChanges } = storeToRefs(stagedStore);

const dataStore = useDataStore();
const { vaultUris } = storeToRefs(dataStore);

const uiStateStore = useUiStateStore();
const { globalLoadingValues: loadingValues } = storeToRefs(uiStateStore);

const showHistoryDropdown = ref(false);

const hideHistoryDropdown = () => {
  filterStore.applyNameFilter();
  setTimeout(() => { showHistoryDropdown.value = false; }, 150);
};
</script>

<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-3 divide-y lg:divide-y-0 lg:divide-x divide-slate-100">
    <!-- Section 2: Filter -->
    <div class="lg:col-span-4 flex flex-col gap-3 lg:pr-4">
      <div class="flex items-center gap-1.5">
        <div class="font-bold text-slate-800 text-sm">2. Filter Secrets by Name (Regex)</div>
      </div>
      <div class="flex items-center gap-2">
        <div class="relative flex-1 max-w-xs">
          <div class="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-auto cursor-pointer text-slate-400 hover:text-slate-600" @mousedown.prevent="showHistoryDropdown = !showHistoryDropdown">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" /></svg>
          </div>
          <input 
            type="text"
            v-model="nameFilter"
            @focus="showHistoryDropdown = true"
            @click="showHistoryDropdown = true"
            placeholder="Regex (CSV)..."
            class="w-full border border-slate-300 rounded-lg pl-6 pr-7 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500"
            @keyup.enter="filterStore.applyNameFilter(); showHistoryDropdown = false"
            @keydown.esc="showHistoryDropdown = false"
            @blur="hideHistoryDropdown"
          />
          <div 
            v-if="nameFilter" 
            class="absolute inset-y-0 right-0 pr-2 flex items-center cursor-pointer text-slate-400 hover:text-slate-600"
            @mousedown.prevent="filterStore.setNameFilter(''); filterStore.applyNameFilter(); showHistoryDropdown = false"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 20 20" fill="currentColor">
              <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
            </svg>
          </div>
          <div v-if="showHistoryDropdown && availableRecentFilters.length > 0" class="absolute z-50 w-full mt-1 bg-white border border-slate-200 shadow-lg rounded-md overflow-hidden">
            <ul class="max-h-60 overflow-y-auto">
              <li v-for="f in availableRecentFilters" :key="f" @mousedown.prevent="filterStore.setNameFilter(f); filterStore.applyNameFilter(); showHistoryDropdown = false;" @mouseenter="hoveredFilter = f" @mouseleave="hoveredFilter = null" class="pl-2 pr-6 py-1 text-xs text-slate-700 hover:bg-slate-100 cursor-pointer font-mono truncate relative">
                {{ f }}
                <span v-show="hoveredFilter === f" @mousedown.prevent.stop="filterStore.forgetRecentFilter(f)" class="absolute right-2 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-700 font-bold text-sm px-1.5 py-0.5 rounded hover:bg-slate-200 transition-opacity flex items-center justify-center h-4 w-4 leading-none">&times;</span>
              </li>
            </ul>
          </div>
        </div>
        <svg @click="emit('show-regex-help')" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-slate-400 hover:text-blue-600 cursor-pointer transition-colors shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      </div>
      <div class="flex items-center gap-3 flex-wrap">
        <div class="flex items-center gap-2 lg:gap-3 text-xs text-slate-600 bg-slate-50 border border-slate-200 px-2 lg:px-3 py-1.5 rounded-lg w-max shrink-0">
          <div class="flex items-center gap-1.5 lg:gap-2">
            <span class="font-semibold text-slate-700">Identicons:</span>
            <label class="flex items-center gap-1 cursor-pointer hover:text-slate-900"><input type="checkbox" v-model="uiSettings.identiconsByRow" class="rounded border-slate-300 text-blue-600 focus:ring-blue-500" /> Row</label>
            <label class="flex items-center gap-1 cursor-pointer hover:text-slate-900"><input type="checkbox" v-model="uiSettings.identiconsByCol" class="rounded border-slate-300 text-blue-600 focus:ring-blue-500" /> Col</label>
          </div>
          <div class="w-px h-4 bg-slate-300"></div>
          <div class="flex items-center gap-1.5 lg:gap-2">
            <span class="font-semibold text-slate-700">Reused:</span>
            <label class="flex items-center gap-1 cursor-pointer hover:text-slate-900"><input type="checkbox" v-model="uiSettings.securityByRow" class="rounded border-slate-300 text-blue-600 focus:ring-blue-500" /> Row</label>
            <label class="flex items-center gap-1 cursor-pointer hover:text-slate-900"><input type="checkbox" v-model="uiSettings.securityByCol" class="rounded border-slate-300 text-blue-600 focus:ring-blue-500" /> Col</label>
          </div>
        </div>
        <div class="flex items-center gap-1.5 shrink-0">
          <span class="text-xs text-slate-500 font-medium">Max:</span>
          <select v-model="uiSettings.resultLimit" class="border border-slate-300 rounded-lg px-2 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white">
            <option :value="10">10</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
            <option :value="0">All</option>
          </select>
        </div>
      </div>
    </div>

    <!-- Section 3: Fetch -->
    <div class="lg:col-span-4 flex flex-col gap-3 lg:px-4 pt-4 lg:pt-0">
      <div class="font-bold text-slate-800 text-sm">3. Fetch Values for Visible Rows</div>
      <button 
        @click="emit('fetch-comparison')" 
        :disabled="loadingValues || vaultUris.length === 0 || filteredResultsLength === 0"
        class="w-full py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-bold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        <svg v-if="loadingValues" class="animate-spin -ml-1 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
        <span v-else>Fetch Values ({{ filteredResultsLength }} of {{ allSortedNamesLength }})</span>
      </button>
      <div class="text-xs text-slate-500 leading-tight mt-1.5">
        Refine your filter to optimize fetch performance, or click 
        <span class="inline-flex items-center justify-center bg-white rounded-full p-1 shadow-sm border border-slate-200 mx-0.5 align-middle -mt-0.5">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
        </span>
        on columns/rows to load only those.
      </div>
    </div>

    <!-- Section 4: Analyze -->
    <div class="lg:col-span-4 flex flex-col gap-3 lg:pl-4 pt-4 lg:pt-0">
      <div class="font-bold text-slate-800 text-sm">4. Analyze & Inspect</div>
      <div class="flex items-center gap-2 w-full">
        <button 
          v-if="hasInspectionsRun"
          @click="emit('clear-inspections')" 
          class="flex-1 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded-lg text-sm font-semibold shadow-sm transition-colors flex items-center justify-center gap-2"
        >
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-4 h-4"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
          Clear Inspections
        </button>
        <button 
          v-else
          @click="emit('run-inspections')" 
          :disabled="loadingValues || vaultUris.length === 0 || filteredResultsLength === 0 || !hasFetchedValues"
          class="flex-1 py-1.5 bg-slate-700 hover:bg-slate-800 text-white rounded-lg text-sm font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
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
          class="flex items-center gap-1 text-xs text-amber-700 font-medium bg-amber-50 px-2 py-1.5 rounded border border-amber-200 ml-auto transition-opacity"
          :class="stagedChanges.length === 0 ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer hover:bg-amber-100'"
        >
          <input 
            type="checkbox" 
            v-model="uiSettings.showStagedOnly" 
            class="rounded border-amber-300 text-amber-600 disabled:cursor-not-allowed" 
            :disabled="stagedChanges.length === 0"
          /> 
          Show Staged Only
        </label>
        <button @click="emit('clear-filters')" class="px-3 py-1.5 text-xs text-slate-600 border border-slate-300 rounded-lg hover:bg-slate-50">Clear Filters</button>
      </div>
    </div>
  </div>
</template>
