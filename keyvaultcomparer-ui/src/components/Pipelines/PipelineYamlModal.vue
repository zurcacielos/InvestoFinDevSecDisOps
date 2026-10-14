<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';

const devopsStore = useDevopsDataStore();
const copied = ref(false);

const yamlLinesCount = computed(() => {
  return devopsStore.yamlModal.yaml ? devopsStore.yamlModal.yaml.split('\n').length : 0;
});

const yamlSizeKb = computed(() => {
  if (!devopsStore.yamlModal.yaml) return '0.0';
  return (new Blob([devopsStore.yamlModal.yaml]).size / 1024).toFixed(1);
});

const copyToClipboard = async () => {
  if (!devopsStore.yamlModal.yaml) return;
  await navigator.clipboard.writeText(devopsStore.yamlModal.yaml);
  copied.value = true;
  setTimeout(() => { copied.value = false; }, 2000);
};

const downloadYaml = () => {
  if (!devopsStore.yamlModal.yaml) return;
  const blob = new Blob([devopsStore.yamlModal.yaml], { type: 'text/yaml' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${devopsStore.yamlModal.pipelineName.replace(/[^a-zA-Z0-9_-]/g, '_')}-resolved.yaml`;
  a.click();
  URL.revokeObjectURL(url);
};
</script>

<template>
  <div 
    v-if="devopsStore.yamlModal.show"
    class="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-sm flex items-center justify-center p-4 sm:p-6"
    @click.self="devopsStore.closeYamlModal()"
    @keydown.esc="devopsStore.closeYamlModal()"
  >
    <div class="bg-white rounded-xl shadow-2xl border border-slate-200 flex flex-col w-full max-w-5xl h-[85vh] overflow-hidden animate-in fade-in zoom-in-95 duration-150">
      
      <!-- Modal Header -->
      <div class="flex items-center justify-between px-5 py-3.5 bg-slate-50 border-b border-slate-200 shrink-0">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 rounded-lg bg-blue-100 text-blue-700 flex items-center justify-center font-bold">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h3 class="font-bold text-slate-800 text-base leading-tight">
                {{ devopsStore.yamlModal.pipelineName }}
              </h3>
              <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-emerald-100 text-emerald-800 border border-emerald-200">
                All External Templates Resolved
              </span>
            </div>
            <p class="text-xs text-slate-500 mt-0.5">
              Compiled via Azure DevOps Pipeline Preview API (includes nested templates, parameters &amp; service connections)
            </p>
          </div>
        </div>

        <!-- Header Actions -->
        <div class="flex items-center gap-2">
          <button 
            @click="copyToClipboard" 
            :disabled="devopsStore.yamlModal.isLoading || !devopsStore.yamlModal.yaml"
            class="px-3 py-1.5 text-xs font-semibold rounded-lg border transition-all flex items-center gap-1.5"
            :class="copied ? 'bg-emerald-600 text-white border-emerald-600' : 'bg-blue-600 text-white hover:bg-blue-700 border-blue-600 shadow-sm'"
          >
            <svg v-if="copied" xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
            </svg>
            {{ copied ? 'Copied to Clipboard!' : 'Copy Full YAML' }}
          </button>

          <button 
            @click="downloadYaml"
            :disabled="devopsStore.yamlModal.isLoading || !devopsStore.yamlModal.yaml"
            class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-300 bg-white hover:bg-slate-50 text-slate-700 transition-colors flex items-center gap-1.5 shadow-sm"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            Download
          </button>

          <button 
            @click="devopsStore.closeYamlModal()" 
            class="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-200 transition-colors ml-1"
            title="Close (Esc)"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>

      <!-- Modal Body -->
      <div class="flex-1 bg-slate-900 text-slate-100 overflow-auto p-4 font-mono text-xs leading-relaxed select-text relative">
        <div v-if="devopsStore.yamlModal.isLoading" class="flex flex-col items-center justify-center h-full gap-3 text-slate-400">
          <svg class="animate-spin h-8 w-8 text-blue-400" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <div class="text-sm font-sans font-medium text-slate-300">Expanding external YAML templates via Azure DevOps Preview API...</div>
          <div class="text-xs font-sans text-slate-400">Resolving service connections, template parameters &amp; stages</div>
        </div>

        <div v-else-if="devopsStore.yamlModal.error" class="p-4 bg-rose-950/60 border border-rose-800 rounded-lg text-rose-200">
          <div class="font-bold text-sm mb-1 flex items-center gap-2">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-rose-400" viewBox="0 0 20 20" fill="currentColor">
              <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
            </svg>
            Azure DevOps YAML Preview Failed
          </div>
          <pre class="whitespace-pre-wrap font-mono text-xs opacity-90 mt-2">{{ devopsStore.yamlModal.error }}</pre>
        </div>

        <pre v-else class="whitespace-pre overflow-x-auto text-emerald-300">{{ devopsStore.yamlModal.yaml }}</pre>
      </div>

      <!-- Modal Footer -->
      <div class="px-5 py-2.5 bg-slate-50 border-t border-slate-200 flex items-center justify-between text-xs text-slate-500 shrink-0">
        <span v-if="devopsStore.yamlModal.yaml">
          {{ yamlLinesCount.toLocaleString() }} lines &middot; {{ yamlSizeKb }} KB
        </span>
        <span v-else>Ready</span>
        <button 
          @click="devopsStore.closeYamlModal()"
          class="px-3 py-1 bg-white hover:bg-slate-100 text-slate-700 font-medium rounded border border-slate-300 shadow-sm transition-colors"
        >
          Close
        </button>
      </div>
    </div>
  </div>
</template>
