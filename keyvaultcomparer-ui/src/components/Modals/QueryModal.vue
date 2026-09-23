<script setup lang="ts">
import BaseModal from './BaseModal.vue';
import { useUsageStore } from '../../stores/usageStore';
import { computed, ref } from 'vue';

const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const usageStore = useUsageStore();

const queryText = computed(() => {
  let daysToFetch = usageStore.queryLimitValue;
  if (usageStore.queryLimitUnit === 'months') daysToFetch = usageStore.queryLimitValue * 30;
  else if (usageStore.queryLimitUnit === 'years') daysToFetch = usageStore.queryLimitValue * 365;

  return `AzureDiagnostics
| where ResourceProvider == 'MICROSOFT.KEYVAULT'
| where OperationName == 'SecretGet'
| where clientInfo_s !contains 'KeyVaultComparerApp'
| where TimeGenerated >= ago(${daysToFetch}d)
| summarize LastUsed = max(TimeGenerated) by id_s`;
});

const copied = ref(false);

const copyToClipboard = async () => {
  try {
    await navigator.clipboard.writeText(queryText);
    copied.value = true;
    setTimeout(() => { copied.value = false; }, 2000);
  } catch (err) {
    console.error('Failed to copy: ', err);
  }
};
</script>

<template>
  <BaseModal :show="show" title="Azure Log Analytics Query" max-width="max-w-2xl" @close="emit('close')">
    <div class="mb-4 text-sm text-slate-600">
      This is the KQL (Kusto Query Language) query used internally by the backend to fetch the usage statistics from Azure Monitor Logs. Note that it specifically filters out access made by this application itself.
    </div>
    
    <div class="relative bg-slate-900 rounded-lg p-4 font-mono text-sm text-blue-300 overflow-x-auto shadow-inner">
      <button 
        @click="copyToClipboard" 
        class="absolute top-2 right-2 p-1.5 rounded-md transition-colors"
        :class="copied ? 'bg-emerald-500/20 text-emerald-400' : 'bg-slate-800 text-slate-400 hover:text-white hover:bg-slate-700'"
        :title="copied ? 'Copied!' : 'Copy to clipboard'"
      >
        <svg v-if="!copied" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
        </svg>
        <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
        </svg>
      </button>
      <pre class="whitespace-pre-wrap pr-8"><code>{{ queryText }}</code></pre>
    </div>

    <template #footer>
      <button 
        @click="emit('close')" 
        class="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-medium rounded-lg transition-colors shadow-sm"
      >
        Close
      </button>
    </template>
  </BaseModal>
</template>
