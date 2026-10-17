<script setup lang="ts">
import { ref, computed, onUnmounted } from 'vue';
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
  const all = devopsStore.releaseMatrix?.environments || [];
  return all.filter(env => !devopsStore.hiddenEnvironments.some(h => h.toLowerCase() === env.toLowerCase()));
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

// Header Context Menu State
const headerContextMenu = ref<{ show: boolean; x: number; y: number; env: string }>({
  show: false,
  x: 0,
  y: 0,
  env: ''
});

const handleHeaderContextMenuEsc = (e: KeyboardEvent) => {
  if (e.key === 'Escape') hideHeaderContextMenu();
};

const openHeaderContextMenu = (e: MouseEvent, env: string) => {
  headerContextMenu.value = {
    show: true,
    x: e.clientX,
    y: e.clientY,
    env
  };
  setTimeout(() => {
    window.addEventListener('click', hideHeaderContextMenu);
    window.addEventListener('keydown', handleHeaderContextMenuEsc);
  }, 0);
};

const hideHeaderContextMenu = () => {
  headerContextMenu.value.show = false;
  window.removeEventListener('click', hideHeaderContextMenu);
  window.removeEventListener('keydown', handleHeaderContextMenuEsc);
};

const hideColumn = (env: string) => {
  devopsStore.hideEnvironment(env);
  hideHeaderContextMenu();
};

const hideOtherColumns = (env: string) => {
  devopsStore.hideOtherEnvironments(env);
  hideHeaderContextMenu();
};

const showAllColumns = () => {
  devopsStore.showAllHiddenEnvironments();
  hideHeaderContextMenu();
};

// Column Resizing for Pinned Service / Pipeline Column
const isResizing = ref(false);
let startX = 0;
let startWidth = 0;

const onResizerMouseDown = (e: MouseEvent) => {
  isResizing.value = true;
  startX = e.clientX;
  startWidth = devopsStore.pipelineColumnWidth;
  document.addEventListener('mousemove', onResizerMouseMove);
  document.addEventListener('mouseup', onResizerMouseUp);
  document.body.style.userSelect = 'none';
  document.body.style.cursor = 'col-resize';
};

const onResizerMouseMove = (e: MouseEvent) => {
  if (!isResizing.value) return;
  const delta = e.clientX - startX;
  const newWidth = Math.min(Math.max(startWidth + delta, 180), 800);
  devopsStore.setPipelineColumnWidth(newWidth);
};

const onResizerMouseUp = () => {
  isResizing.value = false;
  document.removeEventListener('mousemove', onResizerMouseMove);
  document.removeEventListener('mouseup', onResizerMouseUp);
  document.body.style.userSelect = '';
  document.body.style.cursor = '';
};

onUnmounted(() => {
  window.removeEventListener('click', hideHeaderContextMenu);
  window.removeEventListener('keydown', handleHeaderContextMenuEsc);
  document.removeEventListener('mousemove', onResizerMouseMove);
  document.removeEventListener('mouseup', onResizerMouseUp);
});
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
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all cursor-pointer"
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
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all cursor-pointer"
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
            class="px-2.5 py-0.5 rounded-md flex items-center gap-1.5 transition-all cursor-pointer"
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

        <!-- Show All Hidden Columns Button (if any environment is hidden) -->
        <button 
          v-if="devopsStore.hiddenEnvironments.length > 0"
          @click="devopsStore.showAllHiddenEnvironments()"
          class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md bg-blue-50 hover:bg-blue-100 border border-blue-200 text-blue-700 font-semibold text-[11px] shadow-2xs transition-colors cursor-pointer"
          title="Restore all hidden environment columns"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
          </svg>
          <span>Show all hidden columns ({{ devopsStore.hiddenEnvironments.length }})</span>
        </button>
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
            <th 
              class="px-3.5 py-2.5 font-bold bg-slate-100 border-r border-slate-200 sticky left-0 z-20 shadow-[2px_0_4px_rgba(0,0,0,0.03)] select-none relative"
              :style="{ width: `${devopsStore.pipelineColumnWidth}px`, minWidth: `${devopsStore.pipelineColumnWidth}px`, maxWidth: `${devopsStore.pipelineColumnWidth}px` }"
            >
              <span>Service / Pipeline</span>
              <div 
                class="absolute right-0 top-0 bottom-0 w-3 cursor-col-resize flex justify-end z-30 group/resizer"
                @mousedown.prevent="onResizerMouseDown"
              >
                <div class="h-full w-[2px] bg-slate-300 group-hover/resizer:bg-blue-400 transition-colors" :class="{'!bg-blue-500': isResizing}"></div>
              </div>
            </th>

            <!-- Dynamic Environment Headers -->
            <th 
              v-for="env in environments" 
              :key="env"
              class="px-3 py-2 font-bold bg-slate-100 border-r border-slate-200 text-center cursor-context-menu select-none hover:bg-slate-200/70 transition-colors"
              :class="[
                devopsStore.matrixLayoutMode === 'columns' 
                  ? 'w-[300px] min-w-[280px] max-w-[340px]' 
                  : devopsStore.matrixLayoutMode === 'rows'
                    ? 'w-[230px] min-w-[210px] max-w-[260px]'
                    : 'w-[250px] min-w-[230px] max-w-[280px]'
              ]"
              @contextmenu.prevent="openHeaderContextMenu($event, env)"
              :title="`Right-click to configure column options for ${env}`"
            >
              <div class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-md bg-white border border-slate-200 shadow-2xs text-slate-800 font-mono font-bold text-xs uppercase">
                <span class="w-2 h-2 rounded-full bg-blue-500"></span>
                {{ env }}
              </div>
            </th>

            <!-- Spacer column to absorb extra horizontal space when only a few columns are visible -->
            <th class="bg-slate-100/90 border-b border-slate-200 w-full min-w-0 p-0"></th>
          </tr>
        </thead>

        <tbody class="divide-y divide-slate-200 text-xs">
          <!-- Fallback when all environments are hidden -->
          <tr v-if="environments.length === 0">
            <td :colspan="100" class="p-8 text-center bg-slate-50 text-slate-500">
              <div class="flex flex-col items-center justify-center gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l18 18" />
                </svg>
                <span class="text-sm font-medium text-slate-700">All environment columns are currently hidden</span>
                <button 
                  @click="devopsStore.showAllHiddenEnvironments()"
                  class="mt-1 px-3.5 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-xs transition-colors cursor-pointer inline-flex items-center gap-1.5"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                  Show all hidden columns ({{ devopsStore.hiddenEnvironments.length }})
                </button>
              </div>
            </td>
          </tr>

          <tr 
            v-for="row in rows" 
            :key="row.pipelineId"
            class="hover:bg-slate-50/70 transition-colors border-b border-slate-200"
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
              class="border-r border-slate-200 select-none align-top"
              :class="[
                devopsStore.matrixLayoutMode === 'rows' ? 'p-0' : 'px-2.5 py-2',
                getDeploymentsForEnv(row, env).length ? 'bg-white' : 'bg-slate-50/40',
                devopsStore.matrixLayoutMode === 'columns' 
                  ? 'w-[300px] min-w-[280px] max-w-[340px]' 
                  : devopsStore.matrixLayoutMode === 'rows'
                    ? 'w-[230px] min-w-[210px] max-w-[260px]'
                    : 'w-[250px] min-w-[230px] max-w-[280px]'
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

            <!-- Spacer cell to absorb remaining width -->
            <td class="bg-slate-50/20 border-b border-slate-200 p-0"></td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Environment Column Header Context Menu -->
    <div 
      v-if="headerContextMenu.show" 
      :style="{ top: `${headerContextMenu.y}px`, left: `${headerContextMenu.x}px` }"
      class="fixed z-50 bg-white border border-slate-200 shadow-[0_4px_12px_rgba(0,0,0,0.1),_0_0_1px_rgba(0,0,0,0.2)] rounded py-1 min-w-[170px] text-[13px] text-slate-800 select-none animate-in fade-in duration-75"
      @click.stop
    >
      <div class="px-3 py-1 text-[11px] font-semibold text-slate-400 uppercase tracking-wider border-b border-slate-100 mb-0.5">
        Column: {{ headerContextMenu.env }}
      </div>
      <button 
        @click="hideColumn(headerContextMenu.env)" 
        class="w-full text-left px-3 py-1.5 hover:bg-slate-100 flex items-center justify-between group cursor-pointer"
      >
        <span>Hide this column</span>
      </button>
      <button 
        @click="hideOtherColumns(headerContextMenu.env)" 
        class="w-full text-left px-3 py-1.5 hover:bg-slate-100 flex items-center justify-between group cursor-pointer"
      >
        <span>Hide Others</span>
      </button>
      <div v-if="devopsStore.hiddenEnvironments.length > 0" class="h-px bg-slate-200 my-1"></div>
      <button 
        v-if="devopsStore.hiddenEnvironments.length > 0" 
        @click="showAllColumns" 
        class="w-full text-left px-3 py-1.5 hover:bg-slate-100 text-blue-600 font-medium flex items-center justify-between group cursor-pointer"
      >
        <span>Show hidden columns</span>
      </button>
    </div>
  </div>
</template>
