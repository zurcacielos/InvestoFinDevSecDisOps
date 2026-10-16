<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  status?: string;
  showLabel?: boolean;
  scanPosition?: number;
}>();

const normalized = computed(() => (props.status || 'unknown').toLowerCase());
const tooltipText = computed(() => {
  const base = `Status: ${props.status || 'Unknown'}`;
  return props.scanPosition ? `${base} · Scanned build position #${props.scanPosition}` : base;
});
</script>

<template>
  <div 
    class="inline-flex items-center gap-1 select-none shrink-0" 
    :title="tooltipText"
  >
    <!-- Succeeded (Azure-style Green Circle Check) -->
    <span 
      v-if="normalized === 'succeeded'"
      class="w-3.5 h-3.5 rounded-full bg-emerald-500 text-white flex items-center justify-center shadow-2xs shrink-0"
    >
      <svg class="w-2.5 h-2.5 stroke-[3]" viewBox="0 0 24 24" fill="none" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
      </svg>
    </span>

    <!-- Failed (Red Circle Cross) -->
    <span 
      v-else-if="normalized === 'failed'"
      class="w-3.5 h-3.5 rounded-full bg-rose-500 text-white flex items-center justify-center shadow-2xs shrink-0"
    >
      <svg class="w-2.5 h-2.5 stroke-[3]" viewBox="0 0 24 24" fill="none" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
      </svg>
    </span>

    <!-- Running / In Progress (Pulsing Blue Indicator) -->
    <span 
      v-else-if="normalized === 'inprogress'"
      class="w-3.5 h-3.5 rounded-full bg-blue-500 text-white flex items-center justify-center shadow-2xs animate-pulse shrink-0"
    >
      <span class="w-1.5 h-1.5 rounded-full bg-white"></span>
    </span>

    <!-- Partially Succeeded / Warning (Amber Dot) -->
    <span 
      v-else-if="normalized === 'partiallysucceeded'"
      class="w-3.5 h-3.5 rounded-full bg-amber-500 text-white flex items-center justify-center shadow-2xs shrink-0 text-[9px] font-bold"
    >
      !
    </span>

    <!-- Canceled / Unknown (Neutral Dot) -->
    <span 
      v-else
      class="w-3.5 h-3.5 rounded-full bg-slate-300 text-slate-600 flex items-center justify-center shadow-2xs shrink-0"
    >
      <span class="w-1 h-1 rounded-full bg-slate-600"></span>
    </span>

    <span 
      v-if="showLabel && normalized !== 'succeeded'" 
      class="text-[10px] font-medium capitalize"
      :class="{
        'text-rose-700': normalized === 'failed',
        'text-blue-700': normalized === 'inprogress',
        'text-amber-700': normalized === 'partiallysucceeded',
        'text-slate-500': !['failed', 'inprogress', 'partiallysucceeded'].includes(normalized)
      }"
    >
      {{ props.status }}
    </span>
  </div>
</template>
