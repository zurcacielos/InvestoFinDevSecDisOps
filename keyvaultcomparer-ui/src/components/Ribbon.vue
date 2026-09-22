<script setup lang="ts">
const props = defineProps<{
  modelValue: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs'
  stagedCount?: number
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs'): void
}>()

const setTab = (tab: 'select' | 'analyze' | 'staged' | 'inspections' | 'logs') => {
  emit('update:modelValue', tab)
}
</script>

<template>
  <div class="bg-white rounded-b-xl shadow-sm border-b border-slate-200 flex flex-col shrink-0 relative z-30 mb-4">
    <!-- Ribbon Tabs and Header -->
    <div class="flex items-center justify-between px-2 pt-1 bg-slate-50 border-b border-slate-200">
      
      <div class="flex items-center gap-6">
        <!-- Logo -->
        <div class="flex items-center gap-2 font-bold text-slate-800 tracking-tight shrink-0 pb-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 11V7a4 4 0 118 0m-4 8v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2z" />
          </svg>
          <span class="hidden sm:inline-block">KV Comparer</span>
        </div>

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
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2"
            :class="modelValue === 'analyze' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Analyze Data
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
            @click="setTab('inspections')"
            class="px-3 py-1 font-medium text-[13px] transition-colors border-b-2"
            :class="modelValue === 'inspections' ? 'border-blue-600 text-blue-700 bg-white rounded-t-md shadow-[0_-2px_4px_rgba(0,0,0,0.02)]' : 'border-transparent text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-t-md'"
          >
            Inspections Report
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
    <div v-show="modelValue !== 'logs'" class="px-3 py-2 bg-white rounded-b-xl min-h-[100px]">
      <div v-show="modelValue === 'select'">
        <slot name="select-vaults"></slot>
      </div>
      <div v-show="modelValue === 'analyze'">
        <slot name="analyze-data"></slot>
      </div>
      <div v-show="modelValue === 'staged'">
        <slot name="staged"></slot>
      </div>
      <div v-show="modelValue === 'inspections'">
        <slot name="inspections"></slot>
      </div>
    </div>
  </div>
</template>
