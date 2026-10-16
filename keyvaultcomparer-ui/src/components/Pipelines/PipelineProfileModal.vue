<script setup lang="ts">
import { ref, watch } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineProfileConfig } from '../../stores/devopsDataStore';

const devopsStore = useDevopsDataStore();

const environmentsList = ref<string[]>([]);
const newEnvInput = ref('');

const syncFromStore = () => {
  environmentsList.value = [...devopsStore.profileConfig.environments];
};

watch(() => devopsStore.isProfileModalOpen, (isOpen) => {
  if (isOpen) {
    syncFromStore();
  }
});

const addEnvironment = () => {
  const val = newEnvInput.value.trim().toLowerCase();
  if (val && !environmentsList.value.includes(val)) {
    environmentsList.value.push(val);
    newEnvInput.value = '';
  }
};

const removeEnvironment = (index: number) => {
  environmentsList.value.splice(index, 1);
};

const moveEnvironment = (index: number, direction: 'up' | 'down') => {
  if (direction === 'up' && index > 0) {
    const temp = environmentsList.value[index];
    environmentsList.value[index] = environmentsList.value[index - 1];
    environmentsList.value[index - 1] = temp;
  } else if (direction === 'down' && index < environmentsList.value.length - 1) {
    const temp = environmentsList.value[index];
    environmentsList.value[index] = environmentsList.value[index + 1];
    environmentsList.value[index + 1] = temp;
  }
};

const applyPreset = (preset: 'standard' | 'simple' | 'extended' | 'prod') => {
  if (preset === 'standard') {
    environmentsList.value = ['dev', 'uat', 'uat1', 'uat2', 'stg', 'stg1', 'prd', 'prd1'];
  } else if (preset === 'simple') {
    environmentsList.value = ['dev', 'test', 'staging', 'prod'];
  } else if (preset === 'extended') {
    environmentsList.value = ['dev', 'qa', 'uat', 'uat1', 'uat2', 'stg', 'preprod', 'prd'];
  } else if (preset === 'prod') {
    environmentsList.value = ['prd', 'prd1'];
  }
};

const saveAndClose = () => {
  const newConfig: PipelineProfileConfig = {
    environmentParameterName: devopsStore.profileConfig.environmentParameterName || localStorage.getItem('adoEnvParamName') || 'Environment',
    operationParameterName: devopsStore.profileConfig.operationParameterName || localStorage.getItem('adoOpParamName') || 'Operation',
    environments: environmentsList.value.filter(e => e.trim().length > 0),
  };
  devopsStore.updateProfileConfig(newConfig);
  devopsStore.setProfileModalOpen(false);
};

const closeModal = () => {
  devopsStore.setProfileModalOpen(false);
};
</script>

<template>
  <div 
    v-if="devopsStore.isProfileModalOpen"
    class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150"
  >
    <div 
      class="bg-white rounded-2xl shadow-2xl border border-slate-200 w-full max-w-xl overflow-hidden flex flex-col max-h-[90vh]"
      @click.stop
    >
      <!-- Modal Header -->
      <div class="px-6 py-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="w-9 h-9 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center border border-blue-100">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
            </svg>
          </div>
          <div>
            <h3 class="text-sm font-bold text-slate-800">Target Environments (Columns)</h3>
            <p class="text-xs text-slate-500">Configure target environments and column display order for release matching</p>
          </div>
        </div>
        <button 
          @click="closeModal"
          class="p-1 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-200/60 transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
          </svg>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 overflow-y-auto space-y-5 text-xs">
        
        <!-- Environments Configuration -->
        <div>
          <div class="flex items-center justify-between mb-2">
            <div>
              <label class="font-semibold text-slate-700 block">
                Target Environments Order (Release Matrix Columns)
              </label>
              <span class="text-[11px] text-slate-400">
                Determines column order and topological tie-breaking (ordered left to right)
              </span>
            </div>
            
            <!-- Presets -->
            <div class="flex items-center gap-1.5">
              <span class="text-[10px] text-slate-400 uppercase font-bold tracking-wider mr-1">Presets:</span>
              <button 
                @click="applyPreset('standard')"
                class="px-2 py-0.5 rounded text-[10px] font-medium bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-slate-600 border border-slate-200 transition-colors"
              >
                Standard
              </button>
              <button 
                @click="applyPreset('simple')"
                class="px-2 py-0.5 rounded text-[10px] font-medium bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-slate-600 border border-slate-200 transition-colors"
              >
                Simple
              </button>
              <button 
                @click="applyPreset('extended')"
                class="px-2 py-0.5 rounded text-[10px] font-medium bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-slate-600 border border-slate-200 transition-colors"
              >
                Extended
              </button>
              <button 
                @click="applyPreset('prod')"
                class="px-2 py-0.5 rounded text-[10px] font-medium bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-slate-600 border border-slate-200 transition-colors"
              >
                Prod
              </button>
            </div>
          </div>

          <!-- Add new environment input -->
          <div class="flex items-center gap-2 mb-3">
            <input 
              v-model="newEnvInput"
              @keydown.enter.prevent="addEnvironment"
              type="text"
              class="flex-1 px-3 py-1.5 bg-slate-50 border border-slate-300 rounded-lg text-slate-800 text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/30 focus:border-blue-500 font-mono"
              placeholder="Add environment (e.g. sandbox, dr, perf) and press Enter"
            />
            <button 
              @click="addEnvironment"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-900 text-white font-medium text-xs rounded-lg shadow-xs transition-colors shrink-0"
            >
              Add
            </button>
          </div>

          <!-- Environment Chips / List -->
          <div class="border border-slate-200 rounded-xl divide-y divide-slate-100 max-h-56 overflow-y-auto bg-slate-50/50">
            <div 
              v-for="(env, idx) in environmentsList" 
              :key="env"
              class="flex items-center justify-between px-3 py-1.5 hover:bg-white transition-colors"
            >
              <div class="flex items-center gap-2">
                <span class="w-5 text-[10px] font-mono font-bold text-slate-400">{{ idx + 1 }}</span>
                <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-blue-50 border border-blue-200 text-blue-800 font-mono font-bold text-xs">
                  <span class="w-1.5 h-1.5 rounded-full bg-blue-500"></span>
                  {{ env }}
                </span>
              </div>

              <div class="flex items-center gap-1">
                <button 
                  @click="moveEnvironment(idx, 'up')"
                  :disabled="idx === 0"
                  class="p-1 rounded text-slate-400 hover:text-slate-700 hover:bg-slate-100 disabled:opacity-20 transition-colors"
                  title="Move Up"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7" />
                  </svg>
                </button>
                <button 
                  @click="moveEnvironment(idx, 'down')"
                  :disabled="idx === environmentsList.length - 1"
                  class="p-1 rounded text-slate-400 hover:text-slate-700 hover:bg-slate-100 disabled:opacity-20 transition-colors"
                  title="Move Down"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                  </svg>
                </button>
                <button 
                  @click="removeEnvironment(idx)"
                  class="p-1 rounded text-rose-400 hover:text-rose-600 hover:bg-rose-50 transition-colors ml-1"
                  title="Remove"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Modal Footer -->
      <div class="px-6 py-3.5 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
        <button 
          @click="applyPreset('standard')"
          class="text-xs text-slate-500 hover:text-slate-700 underline font-medium"
        >
          Reset to Defaults
        </button>
        <div class="flex items-center gap-2">
          <button 
            @click="closeModal"
            class="px-4 py-2 border border-slate-300 hover:bg-slate-100 text-slate-700 font-medium text-xs rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button 
            @click="saveAndClose"
            class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-semibold text-xs rounded-lg shadow-sm transition-colors flex items-center gap-1.5"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            Save
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
