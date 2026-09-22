<script setup lang="ts">
const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm">
    <div class="bg-white rounded-xl shadow-xl max-w-lg w-full overflow-hidden flex flex-col">
      <div class="p-6 border-b border-slate-100 flex items-center justify-between">
        <div>
          <h2 class="text-2xl font-bold text-slate-900">Search Filter (CSV Regex)</h2>
          <p class="text-slate-500 text-sm mt-1">How the secret name filter works.</p>
        </div>
        <button @click="emit('close')" class="text-slate-400 hover:text-slate-600 hover:bg-slate-100 p-1.5 rounded-lg transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
          </svg>
        </button>
      </div>
      <div class="p-6 space-y-4 text-sm">
        <div class="space-y-1">
          <span class="text-slate-500 font-medium">Secrets starting with "prod-" OR ending with "-key":</span>
          <code class="block bg-slate-100 p-2 rounded text-blue-600 font-mono">^prod-, -key$</code>
        </div>
        <div class="space-y-1">
          <span class="text-slate-500 font-medium">Secrets that contain any of the terms in any place:</span>
          <code class="block bg-slate-100 p-2 rounded text-blue-600 font-mono">db, token, api</code>
        </div>
        <div class="space-y-1">
          <span class="text-slate-500 font-medium">Exact match for the word "password" (word boundaries):</span>
          <code class="block bg-slate-100 p-2 rounded text-blue-600 font-mono">\bpassword\b</code>
        </div>
        <div class="space-y-1">
          <span class="text-slate-500 font-medium">Match specific patterns (e.g., tokens with 3+ digits):</span>
          <code class="block bg-slate-100 p-2 rounded text-blue-600 font-mono">token-\d{3,}</code>
        </div>
      </div>
      <div class="p-4 bg-slate-50 border-t border-slate-100 text-right">
        <button @click="emit('close')" class="px-4 py-2 bg-blue-600 text-white rounded-lg font-medium text-sm hover:bg-blue-700 transition-colors">Got it</button>
      </div>
    </div>
  </div>
</template>
