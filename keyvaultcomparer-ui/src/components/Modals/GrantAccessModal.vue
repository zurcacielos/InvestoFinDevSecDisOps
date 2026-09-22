<script setup lang="ts">
const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'download'): void;
}>();
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-md overflow-hidden transform transition-all">
      <div class="p-6">
        <div class="flex items-center gap-2 mb-4 text-blue-600">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
          </svg>
          <h3 class="text-xl font-bold text-slate-900">Grant Permissions</h3>
        </div>
        <p class="text-slate-600 mb-4 text-sm">
          You lack access to one or more vaults. Download the script below to automatically grant yourself <code class="font-mono text-slate-800 bg-slate-100 px-1 rounded">get</code>, <code class="font-mono text-slate-800 bg-slate-100 px-1 rounded">list</code>, and <code class="font-mono text-slate-800 bg-slate-100 px-1 rounded">set</code> permissions.
        </p>
        <ol class="text-sm text-slate-700 list-decimal pl-5 space-y-2 mb-6">
          <li>Click <strong>Download Script</strong> below.</li>
          <li>Open a terminal and run <code class="bg-slate-100 px-1 rounded font-mono">az login</code> if you aren't logged in.</li>
          <li>Run the downloaded <code class="bg-slate-100 px-1 rounded font-mono">grant-vault-permissions.ps1</code> script.</li>
          <li>Refresh this dashboard.</li>
        </ol>
        <div class="flex justify-end gap-3">
          <button 
            @click="emit('close')" 
            class="bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 font-medium py-2 px-4 rounded-lg transition-colors shadow-sm text-sm"
          >
            Cancel
          </button>
          <button 
            @click="emit('download')" 
            class="bg-blue-600 hover:bg-blue-700 text-white font-medium py-2 px-4 rounded-lg transition-colors shadow-sm text-sm flex items-center gap-2"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            Download Script
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
