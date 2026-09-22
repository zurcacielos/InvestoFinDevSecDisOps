<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { storeToRefs } from 'pinia';
import { useDataStore } from '../../stores/dataStore';
import { useUiStateStore } from '../../stores/uiStateStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useStagedStore } from '../../stores/stagedStore';
import { useClipboardStore } from '../../stores/clipboardStore';
import type { SecretComparisonRow, SecretValueStatus } from '../../composables/useSecurityAnalysis';

const props = defineProps<{
  filteredResults: SecretComparisonRow[];
  allSortedNamesLength: number;
}>();

const emit = defineEmits<{
  (e: 'clear-filters'): void;
}>();

const dataStore = useDataStore();
const { vaultUris, knownSecretNames } = storeToRefs(dataStore);

const uiStateStore = useUiStateStore();
const { loadingCells } = storeToRefs(uiStateStore);

const settingsStore = useSettingsStore();
const { uiSettings } = storeToRefs(settingsStore);

const stagedStore = useStagedStore();

const clipboardStore = useClipboardStore();
const { copiedCell, internalClipboard } = storeToRefs(clipboardStore);

const secretNameColumnWidth = ref(250);
const isResizing = ref(false);
const visibleSecrets = ref(new Set<string>());
const highlightedValue = ref<string | null>(null);

const toggleVisibility = (secretName: string) => {
  if (visibleSecrets.value.has(secretName)) {
    visibleSecrets.value.delete(secretName);
  } else {
    visibleSecrets.value.add(secretName);
  }
};

const toggleHighlight = (val: string | null | undefined) => {
  if (!val) return;
  highlightedValue.value = highlightedValue.value === val ? null : val;
};

const handleGridEscape = (e: KeyboardEvent) => {
  highlightedValue.value = null;
  if (e.target instanceof HTMLElement) {
    e.target.blur();
  }
};

const getVaultName = (uri: string) => {
  try {
    return new URL(uri).hostname.split('.')[0];
  } catch {
    return uri;
  }
};

const getValueColor = (colorIndex: number | undefined) => {
  switch (colorIndex) {
    case 1: return 'text-emerald-500';
    case 2: return 'text-blue-500';
    case 3: return 'text-amber-500';
    case 4: return 'text-fuchsia-500';
    default: return 'text-slate-500';
  }
};

const getCellClasses = (statusObj: SecretValueStatus | undefined) => {
  if (!statusObj) return '';
  let baseClass = '';
  if (statusObj.isStaged) {
    baseClass = 'bg-amber-50 border-l-[3px] border-l-amber-400 !border-r !border-r-amber-100 shadow-[inset_0_0_8px_rgba(251,191,36,0.15)]';
  } else {
    switch (statusObj.status?.toLowerCase()) {
      case 'match': baseClass = 'bg-emerald-50/50'; break;
      case 'mismatch': baseClass = 'bg-amber-50/50'; break;
      case 'missing': baseClass = ''; break;
      case 'forbidden': baseClass = 'bg-red-50/50'; break;
    }
  }
  if (statusObj.highestSeverity === 'Critical') {
    baseClass += ' underline decoration-rose-500 decoration-wavy underline-offset-4';
  }
  return baseClass;
};

const handleCopy = async (uri: string, secretName: string, value: string | null | undefined) => {
  if (!value) return;
  clipboardStore.copy(uri, secretName, value);
  try {
    await navigator.clipboard.writeText(value);
  } catch (e) {
    console.warn('Clipboard write failed, using internal clipboard only');
  }
};

const handlePaste = async (uri: string, secretName: string, currentStatus: SecretValueStatus | undefined) => {
  if (!currentStatus) return;
  let pasteValue = internalClipboard.value;
  try {
    const text = await navigator.clipboard.readText();
    if (text) pasteValue = text;
  } catch (e) {
    // Fallback to internal clipboard
  }

  if (!pasteValue || pasteValue === currentStatus.value) return;

  const originalValue = currentStatus.isStaged 
    ? stagedStore.stagedChanges.find(s => s.vaultUri === uri && s.secretName === secretName)?.originalValue || null
    : currentStatus.value;

  stagedStore.addChange(uri, secretName, originalValue, pasteValue, currentStatus.status);
};

const onResizerMouseDown = () => {
  isResizing.value = true;
  document.addEventListener('mousemove', onResizerMouseMove);
  document.addEventListener('mouseup', onResizerMouseUp);
  document.body.style.userSelect = 'none';
  document.body.style.cursor = 'col-resize';
};

const onResizerMouseMove = (e: MouseEvent) => {
  if (!isResizing.value) return;
  const newWidth = e.clientX - 58;
  if (newWidth > 150 && newWidth < 800) {
    secretNameColumnWidth.value = newWidth;
  }
};

const onResizerMouseUp = () => {
  isResizing.value = false;
  document.removeEventListener('mousemove', onResizerMouseMove);
  document.removeEventListener('mouseup', onResizerMouseUp);
  document.body.style.userSelect = '';
  document.body.style.cursor = '';
};

const keydownHandler = (e: KeyboardEvent) => {
  if (e.key === 'Escape') {
    clipboardStore.clear();
  }
};
const focusinHandler = (e: FocusEvent) => {
  if (!(e.target as HTMLElement).closest('table')) {
    clipboardStore.clear();
  }
};
const clickHandler = (e: MouseEvent) => {
  if (!(e.target as HTMLElement).closest('td')) {
    clipboardStore.clear();
  }
};

onMounted(() => {
  document.addEventListener('keydown', keydownHandler);
  document.addEventListener('focusin', focusinHandler);
  document.addEventListener('click', clickHandler);
});

onUnmounted(() => {
  document.removeEventListener('keydown', keydownHandler);
  document.removeEventListener('focusin', focusinHandler);
  document.removeEventListener('click', clickHandler);
});
</script>

<template>
  <div class="bg-white rounded-xl shadow-sm border border-slate-200 flex-1 min-h-0 flex flex-col relative z-10">
    <div class="overflow-auto flex-1">
      <table class="w-full text-left text-xs whitespace-nowrap border-collapse" @keydown.esc="handleGridEscape">
        <thead class="bg-slate-50 text-slate-600 sticky top-0 z-20 shadow-[0_1px_0_0_#e2e8f0]">
          <tr>
            <th class="w-10 min-w-[30px] max-w-[30px] px-1 py-1.5 text-center sticky left-0 z-30 bg-slate-50 shadow-[1px_0_0_0_#e2e8f0] text-xs text-slate-400">#</th>
            <th class="w-10 min-w-[30px] max-w-[30px] px-1 py-1.5 text-center sticky left-[29px] z-30 bg-slate-50 shadow-[1px_0_0_0_#e2e8f0]">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mx-auto text-slate-400" viewBox="0 0 20 20" fill="currentColor"><path d="M10 12a2 2 0 100-4 2 2 0 000 4z" /><path fill-rule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clip-rule="evenodd" /></svg>
            </th>
            <th 
              class="px-3 py-1.5 text-xs font-semibold tracking-wider sticky z-30 bg-slate-50 shadow-[1px_0_0_0_#e2e8f0]"
              :style="{ left: '58px', width: `${secretNameColumnWidth}px`, minWidth: `${secretNameColumnWidth}px`, maxWidth: `${secretNameColumnWidth}px` }"
            >
              Secret Name
              <div 
                class="absolute right-0 top-0 bottom-0 w-3 cursor-col-resize flex justify-end z-40 group/resizer"
                @mousedown.prevent="onResizerMouseDown"
              >
                <div class="h-full w-[2px] bg-slate-300 group-hover/resizer:bg-blue-400 transition-colors" :class="{'!bg-blue-500': isResizing}"></div>
              </div>
            </th>
            <th v-for="uri in vaultUris" :key="uri" class="px-3 py-1.5 text-xs font-semibold tracking-wider bg-slate-50" :title="knownSecretNames[uri]?.errorMessage">
              <div class="flex items-center justify-between">
                <span :class="knownSecretNames[uri]?.errorMessage ? 'text-rose-600' : 'text-slate-900'">{{ getVaultName(uri) }}</span>
                <button 
                  @click="dataStore.fetchValuesForVault(uri)" 
                  class="text-slate-400 hover:text-blue-600 transition-colors bg-white rounded-full p-1 shadow-sm border border-slate-200"
                  title="Fetch values for this vault"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                </button>
              </div>
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-slate-100">
          <tr v-for="(row, index) in filteredResults" :key="row.secretName" class="hover:bg-slate-50/50 transition-colors group">
            <td class="w-10 min-w-[30px] max-w-[30px] px-1 py-1.5 text-center text-xs text-slate-400 font-normal whitespace-nowrap border-r border-slate-100 sticky left-0 z-10 bg-white group-hover:bg-slate-50 shadow-[1px_0_0_0_#f1f5f9]">
              {{ index + 1 }}
            </td>
            <td class="w-10 min-w-[30px] max-w-[30px] px-1 py-1.5 text-center border-r border-slate-100 sticky left-[29px] z-10 bg-white group-hover:bg-slate-50 shadow-[1px_0_0_0_#f1f5f9]">
              <button 
                @click="toggleVisibility(row.secretName)"
                class="text-slate-400 hover:text-slate-700 focus:outline-none transition-colors"
                title="Show/hide secret"
              >
                <svg v-if="visibleSecrets.has(row.secretName)" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 mx-auto" viewBox="0 0 20 20" fill="currentColor">
                  <path d="M10 12a2 2 0 100-4 2 2 0 000 4z" />
                  <path fill-rule="evenodd" d="M.458 10C1.732 5.943 5.522 3 10 3s8.268 2.943 9.542 7c-1.274 4.057-5.064 7-9.542 7S1.732 14.057.458 10zM14 10a4 4 0 11-8 0 4 4 0 018 0z" clip-rule="evenodd" />
                </svg>
                <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 mx-auto opacity-50" viewBox="0 0 20 20" fill="currentColor">
                  <path fill-rule="evenodd" d="M3.707 2.293a1 1 0 00-1.414 1.414l14 14a1 1 0 001.414-1.414l-1.473-1.473A10.014 10.014 0 0019.542 10C18.268 5.943 14.478 3 10 3a9.958 9.958 0 00-4.512 1.074l-1.78-1.781zm4.261 4.26l1.514 1.515a2.003 2.003 0 012.45 2.45l1.514 1.514a4 4 0 00-5.478-5.478z" clip-rule="evenodd" />
                  <path d="M12.454 16.697L9.75 13.992a4 4 0 01-3.742-3.741L2.335 6.578A9.98 9.98 0 00.458 10c1.274 4.057 5.065 7 9.542 7 .847 0 1.669-.105 2.454-.303z" />
                </svg>
              </button>
            </td>
            <td 
              class="name-cell-container px-3 py-1 text-xs font-medium text-slate-900 border-r border-slate-100 sticky z-10 bg-white group-hover:bg-slate-50 shadow-[1px_0_0_0_#f1f5f9] group"
              :style="{ left: '58px', width: `${secretNameColumnWidth}px`, minWidth: `${secretNameColumnWidth}px`, maxWidth: `${secretNameColumnWidth}px` }"
            >
              <div class="flex items-center justify-between w-full h-full">
                <span class="pr-2 line-clamp-2 break-all whitespace-normal" :title="row.secretName">{{ row.secretName }}</span>
                <button 
                  @click="dataStore.fetchValuesForRow(row.secretName)"
                  class="fetch-btn text-slate-400 hover:text-blue-600 transition-colors bg-white rounded-full p-1.5 shadow-sm border border-slate-200 flex-shrink-0"
                  title="Fetch values for this row"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                </button>
              </div>
            </td>
            <td 
              v-for="uri in vaultUris" 
              :key="uri"
              class="px-3 py-1 text-xs border-r border-slate-100 last:border-r-0 relative focus:outline-none focus:ring-2 focus:ring-inset focus:ring-blue-400 group/cell transition-colors cursor-cell"
              tabindex="0"
              @keydown.ctrl.c.prevent="handleCopy(uri, row.secretName, row.vaultValues[uri]?.value)"
              @keydown.meta.c.prevent="handleCopy(uri, row.secretName, row.vaultValues[uri]?.value)"
              @keydown.ctrl.v.prevent="handlePaste(uri, row.secretName, row.vaultValues[uri])"
              @keydown.meta.v.prevent="handlePaste(uri, row.secretName, row.vaultValues[uri])"
              :class="[getCellClasses(row.vaultValues[uri]), copiedCell?.uri === uri && copiedCell?.secretName === row.secretName ? '!outline-dashed !outline-2 !outline-blue-500 !outline-offset-[-2px] z-30' : '']"
            >
              <button 
                v-if="row.vaultValues[uri]?.isStaged"
                @click.stop="stagedStore.revertChange(uri, row.secretName)"
                class="absolute right-2 top-1/2 -translate-y-1/2 opacity-0 group-hover/cell:opacity-100 bg-white shadow border border-slate-200 rounded p-1.5 text-slate-500 hover:text-amber-600 hover:bg-amber-50 hover:border-amber-300 transition-all z-20"
                title="Revert Change"
              >
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
                </svg>
              </button>
              <div class="flex items-center justify-center gap-2">
                <button 
                  v-if="row.vaultValues[uri]?.status === 'Not Retrieved'" 
                  @click.stop="dataStore.fetchValuesForVaultAndNames(uri, [row.secretName])" 
                  class="text-slate-300 hover:text-blue-600 transition-colors hover:bg-slate-50 rounded-full p-1.5 border border-transparent hover:border-slate-200 mx-auto"
                  :disabled="loadingCells[uri]?.[row.secretName]"
                  :class="{'opacity-50 cursor-not-allowed': loadingCells[uri]?.[row.secretName]}"
                  title="Fetch value"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                </button>
                <span v-else-if="loadingCells[uri]?.[row.secretName] && !row.vaultValues[uri]?.value && row.vaultValues[uri]?.status !== 'Missing' && row.vaultValues[uri]?.status !== 'Error'" class="text-blue-500 italic text-sm font-medium flex items-center gap-1 mx-auto">
                  <svg class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                </span>
                <span v-else-if="row.vaultValues[uri]?.status === 'Missing'" class="text-slate-300 font-bold mx-auto text-lg">
                  -
                </span>
                <span v-else-if="row.vaultValues[uri]?.status === 'Forbidden'" class="text-red-600 font-bold text-sm bg-red-50 px-2 py-1 rounded cursor-help shadow-sm border border-red-200" :title="row.vaultValues[uri]?.errorMessage">
                  [403 Forbidden]
                </span>
                <span v-else-if="row.vaultValues[uri]?.status === 'Error'" class="text-rose-500 italic text-sm font-medium">
                  Error
                </span>
                <span v-else class="font-mono tracking-widest font-semibold flex items-center gap-2 px-1.5 py-0.5 rounded transition-all duration-200" :class="[uiSettings.colorMatchByRow ? getValueColor(row.vaultValues[uri]?.colorIndex) : '', {'bg-yellow-100 ring-2 ring-yellow-400 shadow-sm': highlightedValue === row.vaultValues[uri]?.value, 'opacity-40 grayscale': loadingCells[uri]?.[row.secretName]}]">
                  <template v-if="visibleSecrets.has(row.secretName)">
                    <span class="tracking-normal block max-w-[250px] overflow-x-auto align-bottom secret-scroll pb-0.5">{{ row.vaultValues[uri]?.value }}</span>
                    <span 
                      v-if="row.vaultValues[uri]?.identiconEmoji"  
                      class="cursor-pointer hover:scale-125 transition-transform text-lg drop-shadow-sm ml-1"
                      title="Value Identicon"
                      @click.stop="toggleHighlight(row.vaultValues[uri]?.value)"
                    >
                      {{ row.vaultValues[uri]?.identiconEmoji }}
                    </span>
                  </template>
                  <template v-else>
                    <span class="block max-w-[250px] overflow-x-auto align-bottom secret-scroll pb-0.5">******</span>
                    <span 
                      v-if="row.vaultValues[uri]?.identiconEmoji" 
                      class="cursor-pointer hover:scale-125 transition-transform text-lg drop-shadow-sm ml-1"
                      title="Value Identicon"
                      @click.stop="toggleHighlight(row.vaultValues[uri]?.value)"
                    >
                      {{ row.vaultValues[uri]?.identiconEmoji }}
                    </span>
                  </template>
                  
                  <span 
                    v-if="row.vaultValues[uri]?.inspections?.length"
                    class="ml-1.5 cursor-help flex items-center justify-center rounded-full transition-transform hover:scale-110 drop-shadow-sm w-5 h-5 ring-1 bg-black ring-green-400 shrink-0"
                    :class="{
                      'text-[#00FFFF]': row.vaultValues[uri]?.highestSeverity === 'Low',
                      'text-[#FFFF00]': row.vaultValues[uri]?.highestSeverity === 'Medium',
                      'text-[#FF8800]': row.vaultValues[uri]?.highestSeverity === 'High',
                      'text-[#FF0000]': row.vaultValues[uri]?.highestSeverity === 'Critical'
                    }"
                    :title="(row.vaultValues[uri]?.inspections || []).map(i => `• [${i.severity}] ${i.ruleName}: ${i.message}`).join('\n')"
                  >
                    <span class="text-[11px] font-bold uppercase leading-none flex items-center justify-center h-full w-full pb-[1px]">
                      {{ row.vaultValues[uri]?.highestSeverity?.substring(0, 1) }}
                    </span>
                  </span>
                  
                  <svg v-if="loadingCells[uri]?.[row.secretName]" class="animate-spin h-3.5 w-3.5 text-blue-500 ml-1 shrink-0" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                </span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    
    <!-- Empty State Overlay -->
    <div v-if="filteredResults.length === 0 && !uiStateStore.globalLoadingValues" class="absolute inset-0 z-20 flex flex-col items-center justify-center bg-white/60 backdrop-blur-sm rounded-xl text-center border-2 border-dashed border-slate-200 m-4">
      
      <!-- Case 1: No data loaded at all -->
      <div v-if="allSortedNamesLength === 0" class="bg-white p-8 rounded-2xl shadow-sm border border-slate-100 flex flex-col items-center">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 text-slate-300 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
        <h3 class="text-xl font-bold text-slate-800">No comparisons yet</h3>
        <p class="mt-2 text-slate-500 max-w-sm">Select Key Vaults from the side panel to view their contents and begin comparing.</p>
      </div>

      <!-- Case 2: Data loaded but completely filtered out -->
      <div v-else class="bg-white p-8 rounded-2xl shadow-sm border border-slate-100 flex flex-col items-center">
        <div class="h-16 w-16 bg-blue-50 text-blue-500 rounded-full flex items-center justify-center mb-4">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
          </svg>
        </div>
        <h3 class="text-xl font-bold text-slate-800">No secrets match your filters</h3>
        <p class="mt-2 text-slate-500 max-w-sm mb-6">Your current filter settings are hiding all {{ allSortedNamesLength }} loaded secrets.</p>
        <button @click="emit('clear-filters')" class="px-6 py-1.5 bg-blue-600 text-xs hover:bg-blue-700 text-white font-medium rounded-lg shadow-sm transition-colors flex items-center gap-2">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
          </svg>
          Clear all filters
        </button>
      </div>

    </div>
  </div>
</template>
