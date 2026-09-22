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
          <h2 class="text-2xl font-bold text-slate-900">Key Vault Comparer</h2>
          <p class="text-slate-500 text-sm mt-1">Compare and sync secrets across environments.</p>
        </div>
        <button @click="emit('close')" class="text-slate-400 hover:text-slate-600 hover:bg-slate-100 p-1.5 rounded-lg transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
          </svg>
        </button>
      </div>
      <div class="p-6 space-y-4 text-sm text-slate-600">
        <p>This tool allows you to easily compare secret values across multiple Azure Key Vaults side-by-side.</p>
        <ul class="list-disc pl-5 space-y-2">
          <li>Search and select multiple vaults from your Azure Subscriptions.</li>
          <li>Identify missing, mismatched, or identical secret values instantly.</li>
          <li><strong>Identicons:</strong> same value = same identicon, table wise, by row, by column. Click an identicon to highlight all cells with the same value.</li>
          <li><strong>Reused:</strong> by row or by column.</li>
          <li><strong>Same values per row</strong> have always the same font color</li>
          <li>Analyze value entropy, duplication, and potential vulnerabilities.</li>
          <li>
            Stage changes and review them before deployment. Use <strong>Ctrl+C / Ctrl+V</strong> (or Cmd on Mac) to copy values from one cell to another. These pasted values will enter as "Staged changes". Then, in the <strong>Staged Changes</strong> tab, you can apply them up to 5 at a time, or generate a PowerShell script to review and run manually.
          </li>
        </ul>
      </div>
      <div class="p-4 bg-slate-50 border-t border-slate-100 text-right">
        <button @click="emit('close')" class="px-2 py-1 bg-blue-600 text-white rounded-lg font-medium text-sm hover:bg-blue-700 transition-colors">Got it</button>
      </div>
    </div>
  </div>
</template>
