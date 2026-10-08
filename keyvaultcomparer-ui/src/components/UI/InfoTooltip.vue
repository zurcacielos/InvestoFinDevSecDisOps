<script setup lang="ts">
import { ref } from 'vue';

const props = withDefaults(defineProps<{
  align?: 'left' | 'center' | 'right'
}>(), {
  align: 'center'
});

const showTooltip = ref(false);
</script>

<template>
  <div class="relative inline-flex items-center" @mouseenter="showTooltip = true" @mouseleave="showTooltip = false">
    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-slate-400 hover:text-blue-500 cursor-help transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
    
    <div 
      v-if="showTooltip" 
      class="absolute top-full mt-2 w-64 bg-slate-800 text-white text-xs rounded-lg shadow-xl p-3 z-[100] normal-case tracking-normal font-normal pointer-events-none transition-opacity duration-200"
      :class="{
        'left-1/2 -translate-x-1/2': props.align === 'center',
        'left-0 -translate-x-2': props.align === 'left',
        'right-0 translate-x-2': props.align === 'right'
      }"
    >
      <slot></slot>
      <!-- Arrow pointing up -->
      <div 
        class="absolute bottom-full border-4 border-transparent border-b-slate-800"
        :class="{
          'left-1/2 -translate-x-1/2': props.align === 'center',
          'left-3': props.align === 'left',
          'right-3': props.align === 'right'
        }"
      ></div>
    </div>
  </div>
</template>
