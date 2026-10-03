<script setup lang="ts">
import { computed } from 'vue';
import { useFilterStore } from '../../stores/filterStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useUsageStore } from '../../stores/usageStore';

const filterStore = useFilterStore();
const settingsStore = useSettingsStore();
const usageStore = useUsageStore();

const activeFilters = computed(() => {
  const filters: { id: string; label: string; action: () => void }[] = [];

  // Regex Filter
  if (filterStore.nameFilter.trim()) {
    filters.push({
      id: 'regex',
      label: `Name: ${filterStore.nameFilter}`,
      action: () => {
        filterStore.setNameFilter('');
        filterStore.applyNameFilter();
      }
    });
  }

  // Reused Row
  if (settingsStore.uiSettings.securityByRow) {
    filters.push({
      id: 'reusedRow',
      label: 'Reused: Row',
      action: () => {
        settingsStore.uiSettings.securityByRow = false;
        settingsStore.saveUiSettings();
      }
    });
  }

  // Reused Col
  if (settingsStore.uiSettings.securityByCol) {
    filters.push({
      id: 'reusedCol',
      label: 'Reused: Col',
      action: () => {
        settingsStore.uiSettings.securityByCol = false;
        settingsStore.saveUiSettings();
      }
    });
  }

  // Max Results Limit
  if (settingsStore.uiSettings.resultLimit !== 0) {
    filters.push({
      id: 'maxResults',
      label: `Max: ${settingsStore.uiSettings.resultLimit}`,
      action: () => {
        settingsStore.uiSettings.resultLimit = 0;
        settingsStore.saveUiSettings();
      }
    });
  }

  // Criticality
  if (filterStore.inspectionFilter !== 'None') {
    filters.push({
      id: 'criticality',
      label: `Inspection: ${filterStore.inspectionFilter}`,
      action: () => {
        filterStore.setInspectionFilter('None');
      }
    });
  }

  // Usage Filter
  if (usageStore.filterMode !== 'None') {
    let label = `Usage: ${usageStore.filterMode}`;
    if (usageStore.filterMode === 'UsedBetween') {
      label = `Usage: ${usageStore.filterStartDate} to ${usageStore.filterEndDate}`;
    } else if (usageStore.filterMode !== 'Unused') {
      label = `Usage: ${usageStore.filterMode} ${usageStore.filterValue} ${usageStore.filterUnit}`;
    }
    
    filters.push({
      id: 'usage',
      label,
      action: () => {
        usageStore.clearFilters();
      }
    });
  }

  return filters;
});

const clearAllFilters = () => {
  activeFilters.value.forEach(f => f.action());
};
</script>

<template>
  <div v-if="activeFilters.length > 0" class="flex flex-wrap items-center justify-end gap-2 px-1 my-[4px]">
    <span class="text-xs font-medium text-slate-400">Filters:</span>
    <div 
      v-for="filter in activeFilters" 
      :key="filter.id"
      class="flex items-center gap-1 bg-slate-100 rounded text-xs font-medium text-slate-600 px-2 py-0.5"
    >
      <span>{{ filter.label }}</span>
      <button 
        @click="filter.action" 
        class="text-slate-400 hover:text-rose-500 focus:outline-none transition-colors rounded hover:bg-slate-200"
        title="Remove filter"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" viewBox="0 0 20 20" fill="currentColor">
          <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
        </svg>
      </button>
    </div>
    
    <button 
      v-if="activeFilters.length > 1" 
      @click="clearAllFilters"
      class="text-xs font-semibold text-slate-400 hover:text-rose-600 transition-colors focus:outline-none"
    >
      Clear All Filters
    </button>
  </div>
</template>
