<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import { formatTimeAgo } from '../../utils/pipelineUrlHelpers';

const devopsStore = useDevopsDataStore();

onMounted(() => {
  devopsStore.fetchBackendSettings();
});

const searchQuery = ref('');
const isDropdownOpen = ref(false);
const copySuccessNotice = ref('');

const isSearching = computed(() => searchQuery.value.trim().length > 0);
const recentPipelines = computed(() => devopsStore.recentPipelines);

const filteredPipelines = computed(() => {
  const q = searchQuery.value.trim().toLowerCase();
  let list = devopsStore.pipelines;
  if (q) {
    list = list.filter(p => {
      const nameMatch = p.name.toLowerCase().includes(q);
      const cleanMatch = devopsStore.cleanPipelineName(p.name, p.repository?.name).toLowerCase().includes(q);
      const repoMatch = p.repository?.name?.toLowerCase().includes(q);
      const pathMatch = p.path?.toLowerCase().includes(q);
      return nameMatch || cleanMatch || repoMatch || pathMatch;
    });
  }
  return list;
});

const handleSelectPipeline = (id: number) => {
  devopsStore.selectPipeline(id);
  searchQuery.value = '';
  isDropdownOpen.value = false;
};

const handleBlur = () => {
  setTimeout(() => {
    isDropdownOpen.value = false;
  }, 200);
};

const handleFilterChange = () => {
  devopsStore.hasPendingChanges = true;
};

const triggerCopyYaml = async (pipelineId: number, pipelineName: string) => {
  const result = await devopsStore.copyPipelineYamlToClipboard(pipelineId, pipelineName);
  if (result.success && result.length !== undefined) {
    copySuccessNotice.value = `Copied resolved YAML for ${pipelineName} (${result.length.toLocaleString()} chars)`;
    setTimeout(() => { copySuccessNotice.value = ''; }, 3000);
  }
};

const matrixRowsCount = computed(() => {
  return devopsStore.releaseMatrix?.rows?.length ?? devopsStore.selectedPipelineIds.length;
});

const matrixEnvsCount = computed(() => {
  const allEnvs = devopsStore.releaseMatrix?.environments || [];
  return allEnvs.filter(e => !devopsStore.hiddenEnvironments.includes(e)).length;
});
</script>

<template>
  <div class="flex flex-col w-full min-h-[96px] px-2 py-1.5 gap-2 relative">
    
    <!-- Row 1: Context Title & Top Actions / Catalog Fetch -->
    <div class="flex items-center justify-between flex-wrap gap-2">
      <div class="flex items-center gap-3 flex-wrap">
        <span class="font-bold text-slate-800 text-sm">ADO</span>
        
        <!-- Organization -->
        <div class="flex items-center gap-1.5">
          <span class="text-xs font-medium text-slate-600">Org:</span>
          <input 
            v-model="devopsStore.organization" 
            @keyup.enter="devopsStore.fetchPipelines" 
            type="text" 
            placeholder="e.g. MyOrg" 
            class="w-32 text-xs bg-slate-100 border border-slate-200 rounded-md px-2.5 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" 
          />
        </div>

        <!-- Project -->
        <div class="flex items-center gap-1.5">
          <span class="text-xs font-medium text-slate-600">Project:</span>
          <input 
            v-model="devopsStore.project" 
            @keyup.enter="devopsStore.fetchPipelines" 
            type="text" 
            placeholder="e.g. MyProject" 
            class="w-36 text-xs bg-slate-100 border border-slate-200 rounded-md px-2.5 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" 
          />
        </div>

        <!-- Fetch / Re-fetch Pipelines Button (Hover hint only) -->
        <button 
          @click="devopsStore.fetchPipelines" 
          class="px-3 py-1 font-medium text-xs rounded-lg transition-colors shadow-2xs flex items-center gap-1.5"
          :class="devopsStore.isPipelinesLoading ? 'bg-slate-100 text-slate-500 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700 text-white'"
          :disabled="devopsStore.isPipelinesLoading || !devopsStore.organization || !devopsStore.project"
          :title="devopsStore.pipelines.length > 0 && devopsStore.pipelinesLastFetched 
            ? `Re-fetch all pipelines from Azure DevOps\nCached: ${devopsStore.pipelines.length} pipelines · Last fetched ${formatTimeAgo(devopsStore.pipelinesLastFetched)} (${new Date(devopsStore.pipelinesLastFetched).toLocaleString()})` 
            : (devopsStore.pipelines.length > 0 ? 'Re-fetch all pipeline definitions from Azure DevOps' : 'Fetch pipeline definitions from Azure DevOps')"
        >
          <svg v-if="devopsStore.isPipelinesLoading" class="animate-spin h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          {{ devopsStore.isPipelinesLoading ? 'Fetching...' : (devopsStore.pipelines.length > 0 ? 'Re-fetch Pipelines' : 'Fetch Pipelines') }}
        </button>

        <!-- Pipeline Search Combobox (Hover hint only) -->
        <div 
          class="relative w-64"
          :title="devopsStore.pipelines.length > 0 && devopsStore.pipelinesLastFetched 
            ? `Cached: ${devopsStore.pipelines.length} pipelines from Azure DevOps · Last fetched ${formatTimeAgo(devopsStore.pipelinesLastFetched)} (${new Date(devopsStore.pipelinesLastFetched).toLocaleString()})` 
            : (devopsStore.pipelines.length ? `Search ${devopsStore.pipelines.length} pipelines` : 'Fetch pipelines first')"
        >
          <div class="relative">
            <input 
              v-model="searchQuery" 
              @focus="isDropdownOpen = true"
              @blur="handleBlur"
              type="text" 
              :placeholder="devopsStore.pipelines.length ? `Search ${devopsStore.pipelines.length} pipelines...` : 'Fetch pipelines first...'"
              :disabled="!devopsStore.pipelines.length"
              class="w-full text-xs bg-slate-100 border border-slate-200 rounded-md pl-8 pr-7 py-1 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
            />
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-slate-400 absolute left-2.5 top-1 pointer-events-none" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <button 
              v-if="devopsStore.pipelines.length"
              type="button"
              @mousedown.prevent="isDropdownOpen = !isDropdownOpen"
              class="absolute right-2 top-1.5 text-slate-400 hover:text-slate-600 transition-colors"
              title="Toggle pipelines dropdown"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 transition-transform" :class="{'rotate-180': isDropdownOpen}" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
              </svg>
            </button>
          </div>

          <!-- Combobox Dropdown Results -->
          <div 
            v-if="isDropdownOpen"
            class="absolute left-0 right-0 top-full mt-1 bg-white border border-slate-200 rounded-lg shadow-xl max-h-72 overflow-y-auto z-50 divide-y divide-slate-100"
          >
            <!-- Empty Search State -->
            <div v-if="isSearching && filteredPipelines.length === 0" class="px-3 py-4 text-center text-xs text-slate-400">
              No pipelines matching "{{ searchQuery }}"
            </div>

            <!-- State A: Not searching and has recents -->
            <template v-else-if="!isSearching && recentPipelines.length > 0">
              <!-- Recent Section Sticky Header -->
              <div class="px-3 py-1.5 bg-slate-50 border-b border-slate-200/80 flex items-center justify-between sticky top-0 z-10 select-none">
                <div class="flex items-center gap-1.5 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <span>Recent ({{ recentPipelines.length }})</span>
                </div>
              </div>

              <!-- Recent Items -->
              <div 
                v-for="p in recentPipelines" 
                :key="'recent-' + p.id"
                @mousedown="handleSelectPipeline(p.id)"
                class="px-3 py-2 hover:bg-blue-50 cursor-pointer flex items-center justify-between text-xs transition-colors"
                :class="devopsStore.selectedPipelineIds.includes(p.id) ? 'bg-blue-50/60 font-semibold' : ''"
              >
                <div class="flex flex-col min-w-0 pr-2">
                  <div class="flex items-center gap-1.5 min-w-0">
                    <span class="text-slate-800 truncate font-medium">{{ devopsStore.cleanPipelineName(p.name, p.repository?.name) }}</span>
                    <span v-if="p.runCount !== undefined && p.runCount !== null" class="text-[10px] text-slate-400 font-mono shrink-0">
                      ({{ p.runCount }} {{ p.runCount === 1 ? 'run' : 'runs' }})
                    </span>
                    <span v-else-if="p.hasRuns === false" class="text-[10px] text-slate-400 font-mono shrink-0">
                      (0 runs)
                    </span>
                  </div>
                  <div class="text-[10px] text-slate-400 truncate flex items-center gap-1 mt-0.5">
                    <span v-if="p.path && p.path !== '\\'" class="text-slate-500 font-mono">{{ p.path }} &bull;</span>
                    <span v-if="p.repository?.name">repo: {{ p.repository.name }}</span>
                  </div>
                </div>
                <span 
                  v-if="devopsStore.selectedPipelineIds.includes(p.id)"
                  class="px-1.5 py-0.5 rounded text-[10px] bg-blue-100 text-blue-700 shrink-0"
                >
                  Selected
                </span>
                <span 
                  v-else
                  class="text-slate-400 shrink-0"
                  title="Recently selected"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </span>
              </div>

              <!-- All Pipelines Section Sticky Header -->
              <div class="px-3 py-1.5 bg-slate-50 border-y border-slate-200/80 flex items-center justify-between sticky top-0 z-10 select-none">
                <span class="text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                  All Pipelines ({{ filteredPipelines.length }})
                </span>
              </div>

              <!-- All Pipelines Items -->
              <div 
                v-for="p in filteredPipelines" 
                :key="'all-' + p.id"
                @mousedown="handleSelectPipeline(p.id)"
                class="px-3 py-2 hover:bg-blue-50 cursor-pointer flex items-center justify-between text-xs transition-colors"
                :class="devopsStore.selectedPipelineIds.includes(p.id) ? 'bg-blue-50/60 font-semibold' : ''"
              >
                <div class="flex flex-col min-w-0 pr-2">
                  <div class="flex items-center gap-1.5 min-w-0">
                    <span class="text-slate-800 truncate font-medium">{{ devopsStore.cleanPipelineName(p.name, p.repository?.name) }}</span>
                    <span v-if="p.runCount !== undefined && p.runCount !== null" class="text-[10px] text-slate-400 font-mono shrink-0">
                      ({{ p.runCount }} {{ p.runCount === 1 ? 'run' : 'runs' }})
                    </span>
                    <span v-else-if="p.hasRuns === false" class="text-[10px] text-slate-400 font-mono shrink-0">
                      (0 runs)
                    </span>
                  </div>
                  <div class="text-[10px] text-slate-400 truncate flex items-center gap-1 mt-0.5">
                    <span v-if="p.path && p.path !== '\\'" class="text-slate-500 font-mono">{{ p.path }} &bull;</span>
                    <span v-if="p.repository?.name">repo: {{ p.repository.name }}</span>
                  </div>
                </div>
                <span 
                  v-if="devopsStore.selectedPipelineIds.includes(p.id)"
                  class="px-1.5 py-0.5 rounded text-[10px] bg-blue-100 text-blue-700 shrink-0"
                >
                  Selected
                </span>
              </div>
            </template>

            <!-- State B: Searching OR no recents yet -->
            <template v-else>
              <div 
                v-for="p in filteredPipelines" 
                :key="p.id"
                @mousedown="handleSelectPipeline(p.id)"
                class="px-3 py-2 hover:bg-blue-50 cursor-pointer flex items-center justify-between text-xs transition-colors"
                :class="devopsStore.selectedPipelineIds.includes(p.id) ? 'bg-blue-50/60 font-semibold' : ''"
              >
                <div class="flex flex-col min-w-0 pr-2">
                  <div class="flex items-center gap-1.5 min-w-0">
                    <span class="text-slate-800 truncate font-medium">{{ devopsStore.cleanPipelineName(p.name, p.repository?.name) }}</span>
                    <span v-if="p.runCount !== undefined && p.runCount !== null" class="text-[10px] text-slate-400 font-mono shrink-0">
                      ({{ p.runCount }} {{ p.runCount === 1 ? 'run' : 'runs' }})
                    </span>
                    <span v-else-if="p.hasRuns === false" class="text-[10px] text-slate-400 font-mono shrink-0">
                      (0 runs)
                    </span>
                    <span 
                      v-if="devopsStore.recentPipelineIds.includes(p.id)" 
                      class="px-1 py-0.2 rounded text-[9px] bg-slate-100 text-slate-500 border border-slate-200 shrink-0 font-normal flex items-center gap-0.5 ml-1"
                      title="Recently selected pipeline"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-2.5 w-2.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                      Recent
                    </span>
                  </div>
                  <div class="text-[10px] text-slate-400 truncate flex items-center gap-1 mt-0.5">
                    <span v-if="p.path && p.path !== '\\'" class="text-slate-500 font-mono">{{ p.path }} &bull;</span>
                    <span v-if="p.repository?.name">repo: {{ p.repository.name }}</span>
                  </div>
                </div>
                <span 
                  v-if="devopsStore.selectedPipelineIds.includes(p.id)"
                  class="px-1.5 py-0.5 rounded text-[10px] bg-blue-100 text-blue-700 shrink-0"
                >
                  Selected
                </span>
              </div>
            </template>
          </div>
        </div>
      </div>

      <!-- Right: Copy notice & Clear Cache button at Top-Right of Ribbon -->
      <div class="flex items-center gap-2 ml-auto shrink-0">
        <div v-if="copySuccessNotice" class="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-emerald-100 text-emerald-800 border border-emerald-200 animate-in fade-in flex items-center gap-1.5 shrink-0">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
          </svg>
          {{ copySuccessNotice }}
        </div>

        <button 
          @click="devopsStore.clearAllAndCache()"
          class="px-2.5 py-1 text-xs font-semibold rounded-lg transition-colors shadow-2xs flex items-center gap-1.5 border border-slate-300 bg-white hover:bg-rose-50 hover:text-rose-700 hover:border-rose-200 text-slate-600 shrink-0"
          title="Clear all selected pipelines, matrix state, and server/local cache"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
          </svg>
          <span>Clear Cache</span>
        </button>
      </div>
    </div>

    <!-- Row 2: Selected Pipelines Chips Bar (directly under source combobox, grows as needed) -->
    <div v-if="devopsStore.selectedPipelineIds.length" class="flex items-center gap-2 pt-0.5 flex-wrap">
      <div 
        v-for="p in devopsStore.selectedPipelines" 
        :key="p.id"
        class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-white border border-slate-200 text-xs text-slate-700 shadow-2xs"
      >
        <span class="font-medium" :title="p.name">{{ devopsStore.cleanPipelineName(p.name, p.repository?.name) }}</span>

        <!-- Quick Copy YAML context button -->
        <button 
          @click.stop="triggerCopyYaml(p.id, p.name)"
          title="Copy full pipeline YAML (resolved external templates) to clipboard"
          class="text-slate-400 hover:text-blue-600 transition-colors p-0.5 rounded"
          :disabled="devopsStore.copyingYamlPipelineId === p.id"
        >
          <svg v-if="devopsStore.copyingYamlPipelineId === p.id" class="animate-spin h-3.5 w-3.5 text-blue-500" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
        </button>

        <!-- Remove chip button -->
        <button 
          @click="devopsStore.removePipeline(p.id)" 
          class="text-slate-400 hover:text-rose-500 transition-colors rounded-full hover:bg-slate-100 p-0.5"
          title="Remove from matrix"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>

    <!-- Row 3: View Mode (Environment / Expanded), Filters & Refresh Matrix + Clear All on the right -->
    <div class="flex items-center justify-between gap-3 pt-0.5 flex-wrap">
      
      <!-- Left: View Mode Toggle, Scan, Only Succeeded, Parametrization -->
      <div class="flex items-center gap-2.5 flex-wrap">
        
        <!-- View Mode: Environment vs Expanded -->
        <div class="flex items-center gap-1 p-0.5 bg-slate-200/80 rounded-lg">
          <button 
            @click="devopsStore.setViewMode('environments')"
            class="px-3 py-1 rounded-md text-xs font-semibold transition-all"
            :class="devopsStore.viewMode === 'environments' 
              ? 'bg-white text-slate-800 shadow-2xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Switch to cross-environment matrix view"
          >
            Environment
          </button>
          <button 
            @click="devopsStore.setViewMode('stages')"
            class="px-3 py-1 rounded-md text-xs font-semibold transition-all"
            :class="devopsStore.viewMode === 'stages' 
              ? 'bg-white text-slate-800 shadow-2xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Switch to expanded stages DAG view"
          >
            Expanded
          </button>
        </div>

        <!-- Configure Target Environments (Columns) & Presets Group (Immediately to the right of View Mode) -->
        <div class="inline-flex items-center p-0.5 bg-slate-200/80 rounded-lg shadow-2xs gap-0.5">
          <!-- Columns Config Modal Button -->
          <button 
            @click="devopsStore.setProfileModalOpen(true)"
            class="px-2 py-0.5 rounded-md text-[11px] font-medium text-slate-700 hover:text-slate-900 hover:bg-white/80 transition-all flex items-center gap-1.5 cursor-pointer"
            title="Configure target environment columns order and presets"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
            </svg>
            <span>Columns</span>
          </button>

          <div class="h-3.5 w-px bg-slate-300 mx-0.5"></div>

          <!-- Preset: Default -->
          <button 
            @click="devopsStore.applyEnvironmentPreset('default')"
            class="px-2 py-0.5 rounded-md text-[11px] transition-all flex items-center gap-1 cursor-pointer"
            :class="devopsStore.activePreset === 'default'
              ? 'bg-white text-blue-700 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900 hover:bg-white/50 font-medium'"
            title="Preset Default: dev, uat, uat1, uat2, stg, stg1, prd, prd1"
          >
            <span v-if="devopsStore.activePreset === 'default'" class="w-1.5 h-1.5 rounded-full bg-blue-500"></span>
            <span>Default</span>
          </button>

          <!-- Preset: Prod -->
          <button 
            @click="devopsStore.applyEnvironmentPreset('prod')"
            class="px-2 py-0.5 rounded-md text-[11px] transition-all flex items-center gap-1 cursor-pointer"
            :class="devopsStore.activePreset === 'prod'
              ? 'bg-white text-amber-700 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900 hover:bg-white/50 font-medium'"
            title="Preset Prod: prd, prd1"
          >
            <span v-if="devopsStore.activePreset === 'prod'" class="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
            <span>Prod</span>
          </button>
        </div>

        <!-- Only Succeeded Runs Toggle (Immediately to the right of Columns / Presets) -->
        <label 
          class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-white border border-slate-300 shadow-2xs text-xs font-medium text-slate-700 cursor-pointer hover:bg-slate-50 select-none transition-colors"
          title="Only scan successful pipeline runs when resolving deployments"
        >
          <input 
            type="checkbox" 
            v-model="devopsStore.onlySucceeded" 
            @change="handleFilterChange"
            class="rounded text-blue-600 focus:ring-blue-500 h-3.5 w-3.5"
          />
          <span class="text-[11px] font-medium">Only Succeeded (✓)</span>
        </label>

        <!-- Prior Run Comparison Mode Combobox -->
        <div class="flex items-center gap-1.5 bg-white border border-slate-300 rounded-lg px-2.5 py-1 shadow-2xs text-xs">
          <span class="text-slate-500 font-medium text-[11px]">Prior:</span>
          <select 
            v-model="devopsStore.priorRunMode"
            @change="devopsStore.setPriorRunMode(devopsStore.priorRunMode)"
            class="bg-transparent text-slate-700 text-xs font-semibold focus:outline-none cursor-pointer"
            title="Comparison mode for prior run: Distinct Revision (skips reruns of same SHA) vs Show SHA + Build (shows immediate previous run)"
          >
            <option value="distinct">Distinct Revision</option>
            <option value="consecutive">Show SHA + Build</option>
          </select>
        </div>

        <!-- Scan Depth Selector -->
        <div class="flex items-center gap-1.5 bg-white border border-slate-300 rounded-lg px-2.5 py-1 shadow-2xs text-xs">
          <span class="text-slate-500 font-medium text-[11px]">Scan:</span>
          <select 
            v-model.number="devopsStore.scanDepth"
            @change="handleFilterChange"
            class="bg-transparent text-slate-700 text-xs font-semibold focus:outline-none cursor-pointer"
            title="Number of recent builds scanned per pipeline"
          >
            <option :value="15">15 runs</option>
            <option :value="25">25 runs</option>
            <option :value="50">50 runs</option>
            <option :value="100">100 runs</option>
          </select>
        </div>
      </div>

      <!-- Right: Refresh Matrix Button (aligned vertically under Clear Cache) -->
      <div class="flex items-center gap-2 ml-auto shrink-0">
        <button 
          @click="devopsStore.fetchReleaseMatrix(undefined, true)"
          class="px-3 py-1 text-xs rounded-lg transition-all shadow-2xs flex items-center gap-1.5"
          :class="devopsStore.hasPendingChanges 
            ? 'border-2 border-blue-500 ring-2 ring-blue-300 bg-blue-50 text-blue-700 font-bold' 
            : 'border border-slate-300 bg-white hover:bg-slate-50 text-slate-700 font-semibold'"
          :disabled="devopsStore.isMatrixLoading || devopsStore.selectedPipelineIds.length === 0"
          :title="devopsStore.hasPendingChanges 
            ? 'Pending changes detected: click to refresh pipeline runs from Azure DevOps' 
            : (devopsStore.selectedPipelineIds.length ? 'Refresh pipeline runs from Azure DevOps' : 'Select at least one pipeline to fetch runs')"
        >
          <span v-if="devopsStore.hasPendingChanges && !devopsStore.isMatrixLoading" class="w-1.5 h-1.5 rounded-full bg-blue-600 animate-pulse"></span>
          <svg 
            xmlns="http://www.w3.org/2000/svg" 
            class="h-3.5 w-3.5" 
            :class="devopsStore.isMatrixLoading ? 'animate-spin text-blue-600' : (devopsStore.hasPendingChanges ? 'text-blue-600' : 'text-slate-500')" 
            fill="none" 
            viewBox="0 0 24 24" 
            stroke="currentColor"
          >
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          <span class="text-[11px]">{{ devopsStore.isMatrixLoading ? 'Updating Runs...' : "Refresh Pipeline's Run" }}</span>
        </button>
      </div>
    </div>

    <!-- Row 4: Environment Matrix Controls & Metrics Bar (Last line of Ribbon) -->
    <div 
      v-if="devopsStore.viewMode === 'environments'" 
      class="flex items-center justify-between gap-2 pt-1 border-t border-slate-200/80 text-xs text-slate-500 flex-wrap"
    >
      <div class="flex items-center gap-2.5 flex-wrap">
        <!-- Matrix Badge -->
        <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-indigo-50 border border-indigo-200 text-indigo-700 font-semibold text-[11px]">
          <span class="w-1.5 h-1.5 rounded-full bg-indigo-500"></span>
          Environment Matrix
        </span>
        <span class="text-slate-300">&bull;</span>
        <span class="text-slate-600 font-medium text-[11px]">
          {{ matrixRowsCount }} Pipelines &times; {{ matrixEnvsCount }} Environments
        </span>
        <span class="text-slate-300">|</span>

        <!-- Layout Mode Segmented Control (Cards / Rows / Columns) -->
        <div class="inline-flex p-0.5 bg-slate-200/80 rounded-lg text-[11px] font-medium shadow-2xs">
          <!-- Cards Mode -->
          <button 
            @click="devopsStore.setMatrixLayoutMode('cards')"
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all cursor-pointer"
            :class="devopsStore.matrixLayoutMode === 'cards' 
              ? 'bg-white text-slate-900 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Cards Mode: Compact active card with docked previous run ribbon"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
            </svg>
            <span>Cards</span>
          </button>

          <!-- Rows Mode (Cross-Environment Comparison) -->
          <button 
            @click="devopsStore.setMatrixLayoutMode('rows')"
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all cursor-pointer"
            :class="devopsStore.matrixLayoutMode === 'rows' 
              ? 'bg-white text-slate-900 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Rows Mode: Strict row alignment across all environments for direct horizontal version comparison"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
            </svg>
            <span>Rows (Cross-Env)</span>
          </button>

          <!-- Prior/Active Mode (Temporal In-Environment Comparison) -->
          <button 
            @click="devopsStore.setMatrixLayoutMode('columns')"
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all cursor-pointer"
            :class="devopsStore.matrixLayoutMode === 'columns' 
              ? 'bg-white text-slate-900 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Prior/Active Mode: Side-by-side vertical split comparing active run vs prior run"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 4v16m6-16v16M4 4h16a1 1 0 011 1v14a1 1 0 01-1 1H4a1 1 0 01-1-1V5a1 1 0 011-1z" />
            </svg>
            <span>Prior/Active</span>
          </button>
        </div>

        <span class="text-slate-300">|</span>

        <!-- Show Prior Run (N-1) Toggle -->
        <label 
          class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-white border border-slate-300 shadow-2xs text-[11px] font-semibold text-slate-700 cursor-pointer hover:bg-slate-50 select-none transition-colors"
          title="Display the previous installation (N-1) in each environment"
        >
          <input 
            type="checkbox" 
            v-model="devopsStore.showRollbackTarget"
            @change="devopsStore.setShowRollbackTarget(devopsStore.showRollbackTarget)"
            class="rounded text-amber-600 focus:ring-amber-500 h-3.5 w-3.5"
          />
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
          </svg>
          <span>Prior Run (N-1)</span>
        </label>

        <!-- Show All Hidden Columns Button (if any environment is hidden) -->
        <button 
          v-if="devopsStore.hiddenEnvironments.length > 0"
          @click="devopsStore.showAllHiddenEnvironments()"
          class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md bg-blue-50 hover:bg-blue-100 border border-blue-200 text-blue-700 font-semibold text-[11px] shadow-2xs transition-colors cursor-pointer"
          title="Restore all hidden environment columns"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
          </svg>
          <span>Show all hidden columns ({{ devopsStore.hiddenEnvironments.length }})</span>
        </button>
      </div>

      <!-- Status Indicators without text legend -->
      <div class="flex items-center gap-2 ml-auto shrink-0 select-none" title="Status: Succeeded (green), Failed (red), Running (blue)">
        <span class="w-2.5 h-2.5 rounded-full bg-emerald-500" title="Succeeded"></span>
        <span class="w-2.5 h-2.5 rounded-full bg-rose-500" title="Failed"></span>
        <span class="w-2.5 h-2.5 rounded-full bg-blue-500 animate-pulse" title="Running"></span>
      </div>
    </div>

    <!-- Error notices -->
    <div v-if="devopsStore.pipelinesError" class="text-rose-500 text-xs font-semibold mt-1">
      {{ devopsStore.pipelinesError }}
    </div>
  </div>
</template>
