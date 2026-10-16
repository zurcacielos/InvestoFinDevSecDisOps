<script setup lang="ts">
import { computed } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineMatrixRow, EnvironmentDeploymentInfo } from '../../stores/devopsDataStore';
import PipelineServiceColumn from './matrix/PipelineServiceColumn.vue';
import EnvironmentCellCards from './matrix/EnvironmentCellCards.vue';
import EnvironmentCellRows from './matrix/EnvironmentCellRows.vue';
import EnvironmentCellColumns from './matrix/EnvironmentCellColumns.vue';

const devopsStore = useDevopsDataStore();

const emit = defineEmits<{
  (e: 'open-context-menu', event: MouseEvent, row: PipelineMatrixRow, cell: any, envName: string): void;
  (e: 'open-all-yamls', row: PipelineMatrixRow): void;
}>();

const environments = computed(() => {
  return devopsStore.releaseMatrix?.environments || [];
});

const rows = computed(() => {
  return devopsStore.releaseMatrix?.rows || [];
});

const getDeploymentsForEnv = (row: PipelineMatrixRow, env: string): EnvironmentDeploymentInfo[] => {
  const history = row.environmentHistory?.[env];
  if (history && history.length > 0) return history;
  const current = row.environmentDeployments?.[env];
  return current ? [current] : [];
};
</script>

<template>
  <div class="flex-1 flex flex-col overflow-hidden">
    <!-- Sub-header Bar with Layout Mode Selector & Controls -->
    <div class="px-4 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between text-xs text-slate-500 shrink-0 flex-wrap gap-2">
      <div class="flex items-center gap-2.5 flex-wrap">
        <!-- Matrix Badge -->
        <span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-indigo-50 border border-indigo-200 text-indigo-700 font-semibold text-[11px]">
          <span class="w-1.5 h-1.5 rounded-full bg-indigo-500"></span>
          Environment Matrix
        </span>
        <span class="text-slate-300">&bull;</span>
        <span class="text-slate-600 font-medium">
          {{ rows.length }} Pipelines &times; {{ environments.length }} Environments
        </span>
        <span class="text-slate-300">|</span>

        <!-- Layout Mode Segmented Control (Cards / Rows / Columns) -->
        <div class="inline-flex p-0.5 bg-slate-200/80 rounded-lg text-[11px] font-medium shadow-2xs">
          <!-- Cards Mode -->
          <button 
            @click="devopsStore.setMatrixLayoutMode('cards')"
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all"
            :class="devopsStore.matrixLayoutMode === 'cards' 
              ? 'bg-white text-slate-900 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Cards Mode: Compact active card with docked previous run ribbon"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
            </svg>
            <span>Cards</span>
          </button>

          <!-- Rows Mode (Cross-Environment Comparison) -->
          <button 
            @click="devopsStore.setMatrixLayoutMode('rows')"
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all"
            :class="devopsStore.matrixLayoutMode === 'rows' 
              ? 'bg-white text-slate-900 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Rows Mode: Strict row alignment across all environments for direct horizontal version comparison"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
            </svg>
            <span>Rows (Cross-Env)</span>
          </button>

          <!-- Columns Mode (Temporal In-Environment Comparison) -->
          <button 
            @click="devopsStore.setMatrixLayoutMode('columns')"
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all"
            :class="devopsStore.matrixLayoutMode === 'columns' 
              ? 'bg-white text-slate-900 font-bold shadow-xs' 
              : 'text-slate-600 hover:text-slate-900'"
            title="Columns Mode: Side-by-side vertical split comparing active run vs prior run"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 4v16m6-16v16M4 4h16a1 1 0 011 1v14a1 1 0 01-1 1H4a1 1 0 01-1-1V5a1 1 0 011-1z" />
            </svg>
            <span>Columns (Temporal)</span>
          </button>
        </div>

        <span class="text-slate-300">|</span>

        <!-- Show Prior Run (N-1) Toggle -->
        <label 
          class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-white border border-slate-300 shadow-2xs text-[11px] font-semibold text-slate-700 cursor-pointer hover:bg-slate-50 select-none transition-colors"
          title="Display the previous installation (N-1) in each environment"
        >
          <input 
            type="checkbox" 
            v-model="devopsStore.showRollbackTarget"
            @change="devopsStore.setShowRollbackTarget(devopsStore.showRollbackTarget)"
            class="rounded text-amber-600 focus:ring-amber-500 h-3.5 w-3.5"
          />
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
          </svg>
          <span>Prior Run (N-1)</span>
        </label>
      </div>

      <!-- Status Legend -->
      <div class="flex items-center gap-3 text-[11px]">
        <span class="inline-flex items-center gap-1 text-emerald-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-emerald-500"></span> Succeeded
        </span>
        <span class="inline-flex items-center gap-1 text-rose-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-rose-500"></span> Failed
        </span>
        <span class="inline-flex items-center gap-1 text-blue-700 font-medium">
          <span class="w-2 h-2 rounded-full bg-blue-500 animate-pulse"></span> Running
        </span>
      </div>
    </div>

    <!-- Scrollable Matrix Table -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left border-collapse table-auto">
        <thead>
          <tr class="bg-slate-100/90 border-b border-slate-200 text-slate-600 text-xs uppercase tracking-wider sticky top-0 shadow-xs z-10">
            <!-- Pinned Pipeline & Repository Combined Header -->
            <th class="px-3.5 py-2.5 font-bold bg-slate-100 border-r border-slate-200 min-w-[220px] max-w-[260px] sticky left-0 z-20 shadow-[2px_0_4px_rgba(0,0,0,0.03)]">
              Service / Pipeline
            </th>

            <!-- Dynamic Environment Headers -->
            <th 
              v-for="env in environments" 
              :key="env"
              class="px-3 py-2 font-bold bg-slate-100 border-r border-slate-200 text-center"
              :class="devopsStore.matrixLayoutMode === 'columns' ? 'min-w-[270px]' : 'min-w-[210px]'"
            >
              <div class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md bg-white border border-slate-200 shadow-2xs text-slate-800 font-mono font-bold text-xs uppercase">
                <span class="w-2 h-2 rounded-full bg-blue-500"></span>
                {{ env }}
              </div>
            </th>
          </tr>
        </thead>

        <tbody class="divide-y divide-slate-100 text-xs">
          <tr 
            v-for="row in rows" 
            :key="row.pipelineId"
            class="hover:bg-slate-50/70 transition-colors"
          >
            <!-- 1. Pinned Service & Repository Integrated Column -->
            <PipelineServiceColumn 
              :row="row"
              @open-context-menu="(e, r, c, env) => emit('open-context-menu', e, r, c, env)"
              @open-all-yamls="(r) => emit('open-all-yamls', r)"
            />

            <!-- 2. Dynamic Environment Cells -->
            <td 
              v-for="(env, envIdx) in environments" 
              :key="env"
              class="px-2.5 py-2 border-r border-slate-200 select-none align-top"
              :class="[
                getDeploymentsForEnv(row, env).length ? 'bg-white' : 'bg-slate-50/40',
                devopsStore.matrixLayoutMode === 'columns' ? 'min-w-[270px]' : 'min-w-[210px]'
              ]"
            >
              <!-- Cards Mode -->
              <EnvironmentCellCards 
                v-if="devopsStore.matrixLayoutMode === 'cards'"
                :row="row"
                :deployments="getDeploymentsForEnv(row, env)"
                :env="env"
                :env-index="envIdx"
                @open-context-menu="(e, r, c, envName) => emit('open-context-menu', e, r, c, envName)"
              />

              <!-- Rows Mode (Cross-Environment Comparison) -->
              <EnvironmentCellRows 
                v-else-if="devopsStore.matrixLayoutMode === 'rows'"
                :row="row"
                :deployments="getDeploymentsForEnv(row, env)"
                :env="env"
                :env-index="envIdx"
                @open-context-menu="(e, r, c, envName) => emit('open-context-menu', e, r, c, envName)"
              />

              <!-- Columns Mode (Temporal In-Environment Comparison) -->
              <EnvironmentCellColumns 
                v-else-if="devopsStore.matrixLayoutMode === 'columns'"
                :row="row"
                :deployments="getDeploymentsForEnv(row, env)"
                :env="env"
                :env-index="envIdx"
                @open-context-menu="(e, r, c, envName) => emit('open-context-menu', e, r, c, envName)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
