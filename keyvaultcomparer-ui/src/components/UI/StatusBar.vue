<script setup lang="ts">
import { computed } from 'vue';
import { useInspectionStore } from '../../stores/inspectionStore';
import { useFilterStore } from '../../stores/filterStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useUsageStore } from '../../stores/usageStore';
import ActiveFiltersChips from '../Filters/ActiveFiltersChips.vue';

const inspectionStore = useInspectionStore();
const filterStore = useFilterStore();
const settingsStore = useSettingsStore();
const usageStore = useUsageStore();

const hasFilters = computed(() => {
  return filterStore.nameFilter.trim() !== '' ||
         settingsStore.uiSettings.securityByRow ||
         settingsStore.uiSettings.securityByCol ||
         settingsStore.uiSettings.resultLimit !== 0 ||
         filterStore.inspectionFilter !== 'None' ||
         usageStore.filterMode !== 'None';
});
</script>

<template>
  <div v-if="hasFilters || inspectionStore.hasInspectionsRun" class="flex flex-wrap items-center justify-center gap-2 px-2 my-[4px] min-h-[28px] w-full">
    
    <!-- Inspections indicator (First) -->
    <div 
      v-if="inspectionStore.hasInspectionsRun" 
      class="flex items-center gap-1 bg-amber-100 rounded text-xs font-semibold text-amber-700 px-2 py-0.5 shadow-sm border border-amber-200 shrink-0"
    >
      <span>Inspections</span>
      <button 
        @click="inspectionStore.clearInspections()" 
        class="text-amber-500 hover:text-rose-600 focus:outline-none transition-colors rounded hover:bg-amber-200"
        title="Clear Inspections"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 20 20" fill="currentColor">
          <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
        </svg>
      </button>
    </div>

    <!-- Divider if both exist -->
    <div v-if="inspectionStore.hasInspectionsRun && hasFilters" class="h-4 w-px bg-slate-300 mx-1"></div>

    <!-- Filters (Second) -->
    <ActiveFiltersChips v-if="hasFilters" />

  </div>
  <div v-else class="h-[20px]"></div>
</template>
