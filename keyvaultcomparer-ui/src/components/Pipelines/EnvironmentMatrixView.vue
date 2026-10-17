<script setup lang="ts">
import { ref, computed, onUnmounted } from 'vue';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { PipelineMatrixRow, EnvironmentDeploymentInfo } from '../../stores/devopsDataStore';
import PipelineServiceColumn from './matrix/PipelineServiceColumn.vue';
import EnvironmentCellCards from './matrix/EnvironmentCellCards.vue';
import EnvironmentCellRows from './matrix/EnvironmentCellRows.vue';
import EnvironmentCellPrior from './matrix/EnvironmentCellPrior.vue';
import EnvironmentCellActive from './matrix/EnvironmentCellActive.vue';
import EmptyEnvironmentCell from './matrix/EmptyEnvironmentCell.vue';

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
    <!-- Scrollable Matrix Table -->
    <div class="flex-1 overflow-auto">
      <table class="w-full text-left border-collapse table-auto">
        <thead class="sticky top-0 z-10 shadow-[0_1px_0_0_#e2e8f0]">
          <!-- Row 1: Pinned Pipeline Column & Environment Headers -->
          <tr class="bg-slate-50 border-b border-slate-200 text-slate-700 text-xs">
            <!-- Pinned Pipeline & Repository Combined Header -->
            <th 
              :rowspan="devopsStore.matrixLayoutMode === 'columns' ? 2 : 1"
              class="px-3 py-1.5 font-semibold text-slate-700 bg-slate-50 border-r border-slate-200 sticky left-0 z-30 shadow-[1px_0_0_0_#e2e8f0] select-none relative align-middle uppercase tracking-wider"
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
              :colspan="devopsStore.matrixLayoutMode === 'columns' ? 2 : 1"
              class="px-3 py-1.5 font-semibold text-slate-700 bg-slate-50 border-r border-slate-200 text-center cursor-context-menu select-none hover:bg-slate-100 transition-colors uppercase tracking-wider text-xs"
              :class="[
                devopsStore.matrixLayoutMode === 'columns' 
                  ? 'whitespace-nowrap' 
                  : devopsStore.matrixLayoutMode === 'rows'
                    ? 'min-w-[230px] whitespace-nowrap'
                    : 'min-w-[250px] whitespace-nowrap'
              ]"
              @contextmenu.prevent="openHeaderContextMenu($event, env)"
              :title="`Right-click to configure column options for ${env}`"
            >
              {{ env }}
            </th>

            <!-- Spacer column to absorb extra horizontal space when only a few columns are visible -->
            <th 
              :rowspan="devopsStore.matrixLayoutMode === 'columns' ? 2 : 1"
              class="bg-slate-50 border-b border-slate-200 w-full min-w-0 p-0"
            ></th>
          </tr>

          <!-- Row 2: Sub-headers for Prior/Active mode -->
          <tr 
            v-if="devopsStore.matrixLayoutMode === 'columns'"
            class="bg-slate-50/90 border-b border-slate-200 text-[10px] uppercase tracking-wider text-slate-500"
          >
            <template v-for="env in environments" :key="`sub-${env}`">
              <!-- Prior Sub-header -->
              <th class="px-2 py-1 font-medium text-center text-amber-900/80 bg-amber-50/40 border-r border-slate-200 whitespace-nowrap min-w-max">
                Prior
              </th>

              <!-- Active Sub-header -->
              <th class="px-2 py-1 font-medium text-center text-slate-600 border-r border-slate-200 whitespace-nowrap min-w-max">
                Active
              </th>
            </template>
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
            v-for="(row, rowIdx) in rows" 
            :key="row.pipelineId"
            class="hover:bg-slate-50/70 transition-colors border-b border-slate-200"
          >
            <!-- 1. Pinned Service & Repository Integrated Column -->
            <PipelineServiceColumn 
              :row="row"
              :is-subsequent-row="rowIdx > 0"
              @open-context-menu="(e, r, c, env) => emit('open-context-menu', e, r, c, env)"
              @open-all-yamls="(r) => emit('open-all-yamls', r)"
            />

            <!-- 2. Dynamic Environment Cells -->
            <template v-for="(env, envIdx) in environments" :key="env">
              <!-- Prior/Active Mode: Two Distinct Table Columns (Prior then Active) -->
              <template v-if="devopsStore.matrixLayoutMode === 'columns'">
                <template v-if="getDeploymentsForEnv(row, env).length > 0">
                  <!-- Prior Column Cell -->
                  <td 
                    class="border-r border-slate-200 select-none align-top p-0 bg-amber-50/40 whitespace-nowrap min-w-max"
                    :class="rowIdx > 0 ? 'pt-1' : ''"
                  >
                    <EnvironmentCellPrior 
                      :row="row"
                      :deployments="getDeploymentsForEnv(row, env)"
                      :env="env"
                      :env-index="envIdx"
                      @open-context-menu="(e, r, c, envName) => emit('open-context-menu', e, r, c, envName)"
                    />
                  </td>

                  <!-- Active Column Cell -->
                  <td 
                    class="border-r border-slate-200 select-none align-top p-0 bg-white whitespace-nowrap min-w-max"
                    :class="rowIdx > 0 ? 'pt-1' : ''"
                  >
                    <EnvironmentCellActive 
                      :row="row"
                      :deployments="getDeploymentsForEnv(row, env)"
                      :env="env"
                      :env-index="envIdx"
                      @open-context-menu="(e, r, c, envName) => emit('open-context-menu', e, r, c, envName)"
                    />
                  </td>
                </template>

                <!-- If Environment has no deployments at all -->
                <td 
                  v-else 
                  :colspan="2" 
                  class="border-r border-slate-200 bg-slate-50/40 select-none align-top p-0"
                  :class="rowIdx > 0 ? 'pt-1' : ''"
                >
                  <EmptyEnvironmentCell :row="row" :env="env" />
                </td>
              </template>

              <!-- Cards / Rows Modes: Single Column per Environment -->
              <td 
                v-else
                class="border-r border-slate-200 select-none align-top"
                :class="[
                  devopsStore.matrixLayoutMode === 'rows' 
                    ? (rowIdx > 0 ? 'pt-1 p-0' : 'p-0')
                    : (rowIdx > 0 ? 'px-2.5 pt-3 pb-2' : 'px-2.5 py-2'),
                  getDeploymentsForEnv(row, env).length ? 'bg-white' : 'bg-slate-50/40',
                  devopsStore.matrixLayoutMode === 'rows'
                    ? 'min-w-[230px] whitespace-nowrap'
                    : 'min-w-[250px] whitespace-nowrap'
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
              </td>
            </template>

            <!-- Spacer cell to absorb remaining width -->
            <td 
              class="bg-slate-50/20 border-b border-slate-200 p-0"
              :class="rowIdx > 0 ? 'pt-1' : ''"
            ></td>
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
