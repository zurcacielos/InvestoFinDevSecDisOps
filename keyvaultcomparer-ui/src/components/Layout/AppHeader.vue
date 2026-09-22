<script setup lang="ts">
import { storeToRefs } from 'pinia';
import Ribbon from '../Ribbon.vue';
import { useAuthStore } from '../../stores/authStore';
import { useStagedStore } from '../../stores/stagedStore';

const props = defineProps<{
  modelValue: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs';
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs'): void;
  (e: 'show-help'): void;
}>();

const authStore = useAuthStore();
const { globalError, profile } = storeToRefs(authStore);

const stagedStore = useStagedStore();
const { stagedChanges } = storeToRefs(stagedStore);

const handleTabChange = (val: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs') => {
  emit('update:modelValue', val);
};
</script>

<template>
  <div class="flex-shrink-0">
    <!-- Global Error Banner -->
    <div v-if="globalError" class="bg-rose-500 text-white px-2 py-1 text-sm flex items-center justify-between shrink-0 shadow-sm z-40 relative">
      <div class="flex items-center gap-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 opacity-90" viewBox="0 0 20 20" fill="currentColor">
          <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
        </svg>
        <span class="font-medium">{{ globalError }}</span>
      </div>
      <div class="flex items-center gap-3">
        <!-- Manual Refresh Button -->
        <button @click="authStore.retryAuth()" class="text-white hover:text-rose-200 transition-colors focus:outline-none flex items-center gap-1 text-xs font-semibold uppercase tracking-wider" title="Retry Auth & Fetch">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Retry
        </button>
        <button @click="authStore.setGlobalError(null)" class="text-white hover:text-rose-200 transition-colors focus:outline-none">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
          </svg>
        </button>
      </div>
    </div>

    <!-- Navigation Ribbon -->
    <Ribbon :modelValue="modelValue" @update:modelValue="handleTabChange" :stagedCount="stagedChanges.length">
      <template #actions-right>
        <button 
          @click="emit('show-help')" 
          class="px-3 py-1.5 text-sm font-semibold text-slate-500 hover:bg-slate-100 hover:text-slate-700 rounded-md transition-colors"
          title="Help & About"
        >
          Help
        </button>

        <a 
          href="https://github.com/zurcacielos/KeyVaultComparer" 
          target="_blank" 
          rel="noopener noreferrer"
          class="flex items-center gap-2 px-3 py-1.5 text-sm font-semibold text-slate-700 bg-white hover:bg-slate-50 border border-slate-300 hover:border-slate-400 rounded-full shadow-sm transition-colors"
          title="View source on GitHub"
        >
          <svg viewBox="0 0 16 16" class="h-4 w-4 text-slate-800" fill="currentColor" aria-hidden="true">
            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"></path>
          </svg>
          GitHub &middot; Fork &amp; Remix
        </a>

        <div class="h-5 w-px bg-slate-200 mx-1"></div>

        <div class="flex items-center gap-3">
          <template v-if="profile && profile.email !== 'Unknown User'">
            <div 
              class="h-8 w-8 rounded-full bg-blue-600 text-white flex items-center justify-center font-bold shadow-inner text-sm"
              :title="profile.email"
            >
              {{ profile.initials }}
            </div>
          </template>
        </div>
      </template>
      
      <template #select-vaults>
        <slot name="select-vaults"></slot>
      </template>
      
      <template #analyze-data>
        <slot name="analyze-data"></slot>
      </template>

      <template #staged>
        <slot name="staged"></slot>
      </template>

      <template #inspections>
        <slot name="inspections"></slot>
      </template>
    </Ribbon>
  </div>
</template>
