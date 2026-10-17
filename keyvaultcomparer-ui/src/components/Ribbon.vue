<script setup lang="ts">
import { ref } from 'vue'
import { useFilterStore } from '../stores/filterStore'

const props = defineProps<{
  modelValue: 'select' | 'analyze' | 'inspections-tool' | 'staged' | 'logs' | 'code' | 'devops' | 'pipelines'
  stagedCount?: number
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: 'select' | 'analyze' | 'inspections-tool' | 'staged' | 'logs' | 'code' | 'devops' | 'pipelines'): void
}>()

const setTab = (tab: 'select' | 'analyze' | 'inspections-tool' | 'staged' | 'logs' | 'code' | 'devops' | 'pipelines') => {
  emit('update:modelValue', tab)
}

const filterStore = useFilterStore()

const isRibbonCollapsed = ref(localStorage.getItem('kvcRibbonCollapsed') === 'true')

const toggleRibbonCollapse = () => {
  isRibbonCollapsed.value = !isRibbonCollapsed.value
  localStorage.setItem('kvcRibbonCollapsed', String(isRibbonCollapsed.value))
}
</script>

<template>
  <div class="bg-white rounded-b-xl shadow-sm border-b border-slate-200 flex flex-col shrink-0 relative z-30">
    <!-- Ribbon Tabs and Header -->
    <div class="flex items-center justify-between px-2 pt-1 bg-slate-50 border-b border-slate-200">
      
      <div class="flex items-center gap-4">
        <!-- Collapse / Show Ribbon Button (replaces logo) -->
        <button 
          @click="toggleRibbonCollapse"
          class="p-1 rounded-md text-slate-500 hover:text-blue-600 hover:bg-slate-200/70 transition-colors shrink-0 mb-1 cursor-pointer flex items-center justify-center"
          :title="isRibbonCollapsed ? 'Show ribbon' : 'Collapse ribbon'"
          :aria-label="isRibbonCollapsed ? 'Show ribbon' : 'Collapse ribbon'"
        >
          <!-- Double Chevron Up (when expanded, clicking collapses) -->
          <svg v-if="!isRibbonCollapsed" xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5 11l7-7 7 7M5 19l7-7 7 7" />
          </svg>
          <!-- Double Chevron Down (when collapsed, clicking shows) -->
          <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19 13l-7 7-7-7m14-8l-7 7-7-7" />
          </svg>
        </button>

        <!-- Tabs -->
        <div class="flex gap-1">
          <button 
            @click="setTab('select')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2"
            :class="modelValue === 'select' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Select Vaults
          </button>
          <button 
            @click="setTab('analyze')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2 flex items-center gap-1.5"
            :class="modelValue === 'analyze' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Fetch Values
            <span v-if="filterStore.nameFilter.trim()" class="flex h-1.5 w-1.5 rounded-full bg-blue-500" title="Active Filter"></span>
          </button>
          <button 
            @click="setTab('devops')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2 flex items-center gap-1.5"
            :class="modelValue === 'devops' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            ADO Libraries
          </button>
          <button 
            @click="setTab('pipelines')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2 flex items-center gap-1.5"
            :class="modelValue === 'pipelines' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
            </svg>
            Pipelines &amp; Releases
          </button>
          <button 
            @click="setTab('inspections-tool')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2 flex items-center gap-1.5"
            :class="modelValue === 'inspections-tool' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Inspections
            <span v-if="filterStore.inspectionFilter !== 'None'" class="flex h-1.5 w-1.5 rounded-full bg-rose-500" title="Active Filter"></span>
          </button>

          <button 
            @click="setTab('code')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2 flex items-center gap-1.5"
            :class="modelValue === 'code' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
            </svg>
            Code
          </button>
          <button 
            @click="setTab('staged')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2 flex items-center gap-2"
            :class="modelValue === 'staged' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Staged Changes
            <span 
              v-if="stagedCount && stagedCount > 0" 
              class="px-2 py-0.5 rounded-full text-xs font-bold"
              :class="modelValue === 'staged' ? 'bg-blue-200 text-blue-800' : 'bg-amber-500 text-white shadow-sm animate-pulse'"
            >
              {{ stagedCount }}
            </span>
          </button>

          <button 
            @click="setTab('logs')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2"
            :class="modelValue === 'logs' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Logs
          </button>
        </div>
      </div>

      <!-- Right Actions (Help, GitHub, Profile) -->
      <div class="flex items-center gap-3 pb-2 shrink-0">
        <slot name="actions-right"></slot>
      </div>

    </div>

    <!-- Ribbon Content -->
    <div v-show="!isRibbonCollapsed && modelValue !== 'logs'" class="px-3 py-2 bg-white rounded-b-xl min-h-[100px]">
      <div v-show="modelValue === 'select'">
        <slot name="select-vaults"></slot>
      </div>
      <div v-show="modelValue === 'analyze'">
        <slot name="analyze-data"></slot>
      </div>
      <div v-show="modelValue === 'inspections-tool'">
        <slot name="inspections-tool"></slot>
      </div>

      <div v-show="modelValue === 'code'">
        <slot name="code"></slot>
      </div>
      <div v-show="modelValue === 'staged'">
        <slot name="staged"></slot>
      </div>

      <div v-show="modelValue === 'devops'">
        <slot name="devops"></slot>
      </div>

      <div v-show="modelValue === 'pipelines'">
        <slot name="pipelines"></slot>
      </div>
    </div>
  </div>
</template>
