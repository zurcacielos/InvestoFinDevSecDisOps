<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';

const devopsStore = useDevopsDataStore();

const searchQuery = ref('');
const isDropdownOpen = ref(false);
const copySuccessNotice = ref('');

const filteredPipelines = computed(() => {
  const q = searchQuery.value.trim().toLowerCase();
  let list = devopsStore.pipelines;
  if (q) {
    list = list.filter(p => {
      const nameMatch = p.name.toLowerCase().includes(q);
      const repoMatch = p.repository?.name?.toLowerCase().includes(q);
      return nameMatch || repoMatch;
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

const triggerCopyYaml = async (pipelineId: number, pipelineName: string) => {
  const result = await devopsStore.copyPipelineYamlToClipboard(pipelineId, pipelineName);
  if (result.success && result.length !== undefined) {
    copySuccessNotice.value = `Copied resolved YAML for ${pipelineName} (${result.length.toLocaleString()} chars)`;
    setTimeout(() => { copySuccessNotice.value = ''; }, 3000);
  }
};
</script>

<template>
  <div class="flex flex-col w-full min-h-[84px] px-2 py-1 relative">
    
    <!-- Top Description & Context Status -->
    <div class="flex items-center justify-between mb-1.5 flex-wrap gap-2">
      <div class="flex items-center gap-2">
        <span class="font-bold text-slate-800 text-sm">Azure DevOps Pipelines &amp; Release Matrix</span>
        <span class="text-xs text-slate-500">Track environment deployments, rollback targets &amp; resolved YAML ASTs</span>
      </div>

      <div v-if="copySuccessNotice" class="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-emerald-100 text-emerald-800 border border-emerald-200 animate-in fade-in flex items-center gap-1.5">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
        {{ copySuccessNotice }}
      </div>
    </div>

    <!-- Inputs & Actions Bar -->
    <div class="flex items-center gap-3 pt-1 flex-wrap">
      
      <!-- Organization -->
      <div class="flex items-center gap-1.5">
        <span class="text-xs font-medium text-slate-600">Org:</span>
        <input 
          v-model="devopsStore.organization" 
          @keyup.enter="devopsStore.fetchPipelines" 
          type="text" 
          placeholder="e.g. MyOrg" 
          class="w-36 text-xs bg-slate-100 border border-slate-200 rounded-md px-2.5 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" 
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
          class="w-44 text-xs bg-slate-100 border border-slate-200 rounded-md px-2.5 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors" 
        />
      </div>

      <!-- Fetch Pipelines Button -->
      <button 
        @click="devopsStore.fetchPipelines" 
        class="px-3 py-1.5 font-medium text-xs rounded-lg transition-colors shadow-sm flex items-center gap-1.5"
        :class="devopsStore.isPipelinesLoading ? 'bg-slate-100 text-slate-500 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700 text-white'"
        :disabled="devopsStore.isPipelinesLoading || !devopsStore.organization || !devopsStore.project"
      >
        <svg v-if="devopsStore.isPipelinesLoading" class="animate-spin h-3.5 w-3.5 text-slate-500" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
        {{ devopsStore.isPipelinesLoading ? 'Fetching...' : 'Fetch Pipelines' }}
      </button>

      <!-- Pipeline Search Combobox -->
      <div class="relative w-72">
        <div class="relative">
          <input 
            v-model="searchQuery" 
            @focus="isDropdownOpen = true"
            @blur="handleBlur"
            type="text" 
            :placeholder="devopsStore.pipelines.length ? `Search ${devopsStore.pipelines.length} pipelines...` : 'Fetch pipelines first...'"
            :disabled="!devopsStore.pipelines.length"
            class="w-full text-xs bg-slate-100 border border-slate-200 rounded-md pl-8 pr-3 py-1.5 text-slate-700 font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
          />
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-slate-400 absolute left-2.5 top-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>

        <!-- Combobox Dropdown Results -->
        <div 
          v-if="isDropdownOpen && filteredPipelines.length"
          class="absolute left-0 right-0 top-full mt-1 bg-white border border-slate-200 rounded-lg shadow-xl max-h-60 overflow-y-auto z-50 divide-y divide-slate-100"
        >
          <div 
            v-for="p in filteredPipelines" 
            :key="p.id"
            @mousedown="handleSelectPipeline(p.id)"
            class="px-3 py-2 hover:bg-blue-50 cursor-pointer flex items-center justify-between text-xs transition-colors"
            :class="devopsStore.selectedPipelineIds.includes(p.id) ? 'bg-blue-50/60 font-semibold' : ''"
          >
            <div class="flex flex-col min-w-0 pr-2">
              <span class="text-slate-800 truncate font-medium">{{ p.name }}</span>
              <span v-if="p.repository?.name" class="text-[10px] text-slate-400 truncate flex items-center gap-1">
                <span>repo:</span> {{ p.repository.name }}
              </span>
            </div>
            <span 
              v-if="devopsStore.selectedPipelineIds.includes(p.id)"
              class="px-1.5 py-0.5 rounded text-[10px] bg-blue-100 text-blue-700 shrink-0"
            >
              Selected
            </span>
          </div>
        </div>
      </div>

      <!-- Refresh Matrix Button -->
      <button 
        v-if="devopsStore.selectedPipelineIds.length"
        @click="devopsStore.fetchReleaseMatrix"
        class="px-3 py-1.5 font-medium text-xs rounded-lg transition-colors shadow-sm flex items-center gap-1.5 border border-slate-300 bg-white hover:bg-slate-50 text-slate-700"
        :disabled="devopsStore.isMatrixLoading"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-slate-500" :class="{'animate-spin': devopsStore.isMatrixLoading}" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
        </svg>
        {{ devopsStore.isMatrixLoading ? 'Updating Matrix...' : 'Refresh Matrix' }}
      </button>

      <!-- Clear Selected Button -->
      <button 
        v-if="devopsStore.selectedPipelineIds.length > 1"
        @click="devopsStore.selectedPipelineIds = []; devopsStore.saveSelectedPipelineIds(); devopsStore.releaseMatrix = null"
        class="text-xs text-slate-400 hover:text-slate-600 underline ml-auto"
      >
        Clear All ({{ devopsStore.selectedPipelineIds.length }})
      </button>
    </div>

    <!-- Selected Pipelines Chips Bar -->
    <div v-if="devopsStore.selectedPipelineIds.length" class="flex items-center gap-2 mt-2 flex-wrap max-h-16 overflow-y-auto">
      <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Matrix Rows:</span>
      <div 
        v-for="p in devopsStore.selectedPipelines" 
        :key="p.id"
        class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white border border-slate-200 text-xs text-slate-700 shadow-sm"
      >
        <span class="font-medium truncate max-w-[160px]">{{ p.name }}</span>

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

    <!-- Error notices -->
    <div v-if="devopsStore.pipelinesError" class="text-rose-500 text-xs font-semibold mt-1">
      {{ devopsStore.pipelinesError }}
    </div>
  </div>
</template>
