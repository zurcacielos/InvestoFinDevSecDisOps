<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue';
import { storeToRefs } from 'pinia';
import { useVaultStore } from '../../stores/vaultStore';
import { useUiStateStore } from '../../stores/uiStateStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useStagedStore } from '../../stores/stagedStore';
import { useClipboardStore } from '../../stores/clipboardStore';
import { useUsageStore } from '../../stores/usageStore';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import type { SecretComparisonRow, SecretValueStatus } from '../../composables/useSecurityAnalysis';
import type { AdoVariableGroup } from '../../stores/devopsDataStore';

const props = defineProps<{
  filteredResults: SecretComparisonRow[];
  allSortedNamesLength: number;
}>();

const emit = defineEmits<{
  (e: 'clear-filters'): void;
  (e: 'grant-access'): void;
}>();

const vaultStore = useVaultStore();
const { vaultUris, knownSecretNames } = storeToRefs(vaultStore);

const uiStateStore = useUiStateStore();
const { loadingCells, currentTab } = storeToRefs(uiStateStore);

const settingsStore = useSettingsStore();
const { uiSettings } = storeToRefs(settingsStore);

const stagedStore = useStagedStore();

const clipboardStore = useClipboardStore();
const { copiedCell, internalClipboard } = storeToRefs(clipboardStore);

const usageStore = useUsageStore();

const devopsDataStore = useDevopsDataStore();

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

const getRelativeTime = (timestamp: number) => {
  const diffInMs = Math.max(0, Date.now() - timestamp);
  const diffInMins = Math.floor(diffInMs / (1000 * 60));
  const diffInHours = Math.floor(diffInMs / (1000 * 60 * 60));
  const diffInDays = Math.floor(diffInMs / (1000 * 60 * 60 * 24));
  
  if (diffInMins < 60) return `${Math.max(1, diffInMins)}m`;
  if (diffInHours < 24) return `${diffInHours}h`;
  if (diffInDays < 365) return `${diffInDays}d`;
  
  const diffInYears = Math.floor(diffInDays / 365);
  return `${diffInYears}y`;
};

const getUsageForCell = (uri: string, secretName: string) => {
  const key = `${uri}_${secretName}`.toLowerCase();
  const d = usageStore.usageData[key];
  if (!d) return null;
  const dateStr = d.endsWith('Z') ? d : d + 'Z';
  const ms = new Date(dateStr).getTime();
  const fullDate = new Date(ms).toLocaleDateString() + ' ' + new Date(ms).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  return { text: getRelativeTime(ms), fullDate };
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

const codeColumns = [
  { id: 'any', label: 'Any Place', icon: '🌍', query: (key: string) => `"${key}"` },
  { id: 'pipelines', label: 'Pipelines', icon: '🚀', query: (key: string) => `"${key}" path:**/*azure-pipelines*.y*ml` },
  { id: 'helm', label: 'Helm', icon: '☸️', query: (key: string) => `"${key}" path:**/*values*.yaml` },
  { id: 'appsettings', label: 'Appsettings', icon: '⚙️', query: (key: string) => `"${key}" path:**/*appsettings*.json` },
  { id: 'csharp_gen', label: 'C# (All)', icon: '🔷', query: (key: string) => `"${key}" language:csharp` },
  { id: 'csharp_get', label: 'C# (.Get)', icon: '🎯', query: (key: string) => `"GetValue(\\"${key}\\")" language:csharp` }
];

const openGithubSearch = (key: string, columnId: string) => {
  const col = codeColumns.find(c => c.id === columnId);
  if (!col) return;
  const org = uiSettings.value.useGithubOrg && uiSettings.value.githubOrg 
    ? `org:${uiSettings.value.githubOrg} ` 
    : '';
  const query = org + col.query(key);
  const encodedQuery = encodeURIComponent(query).replace(/%20/g, '+');
  const baseUrl = uiSettings.value.githubBaseUrl || 'https://github.com';
  const finalUrl = `${baseUrl.replace(/\/$/, '')}/search?q=${encodedQuery}&type=code`;
  window.open(finalUrl, '_blank');
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

type ColumnDef = 
  | { type: 'vault'; id: string; name: string }
  | { type: 'group'; id: number; name: string; associatedVaultName?: string; group: AdoVariableGroup };

const allColumns = computed(() => {
  const cols: ColumnDef[] = [];
  const vaults = vaultUris.value.map(uri => ({ type: 'vault' as const, id: uri, name: getVaultName(uri) }));
  const groups = devopsDataStore.selectedGroups.map(g => ({ type: 'group' as const, id: g.id, name: g.name, associatedVaultName: g.providerData?.vault, group: g }));
  
  if (uiSettings.value.groupDevOpsColumns) {
    const associatedGroups = groups.filter(g => g.associatedVaultName);
    const unassociatedGroups = groups.filter(g => !g.associatedVaultName);
    
    cols.push(...unassociatedGroups);
    vaults.forEach(v => {
      const matchingGroups = associatedGroups.filter(g => g.associatedVaultName?.toLowerCase() === v.name.toLowerCase());
      cols.push(...matchingGroups);
      cols.push(v);
    });
    
    const remaining = associatedGroups.filter(g => !vaults.some(v => v.name.toLowerCase() === g.associatedVaultName?.toLowerCase()));
    cols.push(...remaining);
  } else {
    cols.push(...groups);
    cols.push(...vaults);
  }
  return cols;
});

const visibleColumns = computed(() => {
  return allColumns.value.filter((c: ColumnDef) => !uiSettings.value.hiddenColumns.includes(c.id.toString()));
});

const contextMenu = ref({ show: false, x: 0, y: 0, colId: '' });

const handleContextMenuEsc = (e: KeyboardEvent) => {
  if (e.key === 'Escape') hideContextMenu();
};

const showContextMenu = (e: MouseEvent, colId: string | number) => {
  contextMenu.value = { show: true, x: e.clientX, y: e.clientY, colId: colId.toString() };
  setTimeout(() => {
    window.addEventListener('click', hideContextMenu);
    window.addEventListener('keydown', handleContextMenuEsc);
  }, 0);
};

const hideContextMenu = () => { 
  contextMenu.value.show = false;
  window.removeEventListener('click', hideContextMenu);
  window.removeEventListener('keydown', handleContextMenuEsc);
};

const hideColumn = (colId: string) => {
  if (!uiSettings.value.hiddenColumns.includes(colId)) {
    uiSettings.value.hiddenColumns.push(colId);
    settingsStore.saveUiSettings();
  }
  hideContextMenu();
};

const hideAllButThis = (colId: string) => {
  uiSettings.value.hiddenColumns = allColumns.value.map((c: ColumnDef) => c.id.toString()).filter((id: string) => id !== colId);
  settingsStore.saveUiSettings();
  hideContextMenu();
};

const showAllHiddenColumns = () => {
  uiSettings.value.hiddenColumns = [];
  settingsStore.saveUiSettings();
  hideContextMenu();
};

const isLibraryVisible = (vaultName: string) => {
  const group = devopsDataStore.variableGroups.find(g => g.providerData?.vault?.toLowerCase() === vaultName.toLowerCase());
  if (!group) return false;
  return devopsDataStore.selectedGroupIds.includes(group.id) && !uiSettings.value.hiddenColumns.includes(group.id.toString());
};

const hasAssociatedLibrary = (vaultName: string) => {
  return devopsDataStore.variableGroups.some(g => g.providerData?.vault?.toLowerCase() === vaultName.toLowerCase());
};

const toggleAssociatedLibrary = (vaultName: string) => {
  const group = devopsDataStore.variableGroups.find(g => g.providerData?.vault?.toLowerCase() === vaultName.toLowerCase());
  if (!group) return;
  
  const isCurrentlyVisible = isLibraryVisible(vaultName);
  
  if (isCurrentlyVisible) {
    // Hide it
    const strId = group.id.toString();
    if (!uiSettings.value.hiddenColumns.includes(strId)) {
      uiSettings.value.hiddenColumns.push(strId);
      settingsStore.saveUiSettings();
    }
  } else {
    // Show it
    if (!devopsDataStore.selectedGroupIds.includes(group.id)) {
      devopsDataStore.toggleGroupSelection(group.id);
    }
    const strId = group.id.toString();
    if (uiSettings.value.hiddenColumns.includes(strId)) {
      uiSettings.value.hiddenColumns = uiSettings.value.hiddenColumns.filter(id => id !== strId);
      settingsStore.saveUiSettings();
    }
  }
};

const isMissingInGroupButInVault = (row: SecretComparisonRow, group: AdoVariableGroup) => {
  if (row.libraryValues?.[group.id] !== undefined && row.libraryValues?.[group.id] !== null) return false;
  const vaultName = group.providerData?.vault?.toLowerCase();
  if (!vaultName) return false;
  
  const vaultUri = vaultUris.value.find(uri => getVaultName(uri).toLowerCase() === vaultName);
  if (!vaultUri) return false;
  
  const vaultVal = row.vaultValues[vaultUri];
  return vaultVal && (vaultVal.status === 'Present' || vaultVal.status === 'Loading') && vaultVal.value !== null;
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
  window.removeEventListener('click', hideContextMenu);
});
</script>

<template>
  <div class="bg-white rounded-xl shadow-sm border border-slate-200 flex-1 min-h-0 flex flex-col relative z-10" @click="hideContextMenu">
    
    <!-- Context Menu -->
    <div v-if="contextMenu.show" 
         :style="{ top: `${contextMenu.y}px`, left: `${contextMenu.x}px` }"
         class="fixed z-50 bg-white border border-slate-200 shadow-xl rounded-md py-1 w-48 text-sm"
         @click.stop>
      <button @click="hideColumn(contextMenu.colId)" class="w-full text-left px-4 py-2 hover:bg-slate-100 text-slate-700">
        Hide this column
      </button>
      <button @click="hideAllButThis(contextMenu.colId)" class="w-full text-left px-4 py-2 hover:bg-slate-100 text-slate-700">
        Hide Others
      </button>
      <div v-if="uiSettings.hiddenColumns.length > 0" class="h-px bg-slate-200 my-1"></div>
      <button v-if="uiSettings.hiddenColumns.length > 0" @click="showAllHiddenColumns" class="w-full text-left px-4 py-2 hover:bg-slate-100 text-blue-600 font-medium">
        Show hidden columns
      </button>
    </div>

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

            <template v-if="currentTab !== 'code'">
              <th 
                v-for="col in visibleColumns" 
                :key="col.id" 
                class="px-3 py-1.5 text-xs font-semibold tracking-wider hover:bg-slate-100 transition-colors cursor-context-menu select-none"
                :class="col.type === 'group' ? 'bg-blue-50 text-blue-900 border-r border-blue-100 shadow-[inset_0_1px_0_0_#dbeafe]' : 'bg-slate-50'"
                :title="col.type === 'vault' ? knownSecretNames[col.id]?.errorMessage : ''"
                @contextmenu.prevent="showContextMenu($event, col.id)"
              >
                <!-- Group Header -->
                <div v-if="col.type === 'group'" class="flex items-center justify-center gap-1">
                  <span class="text-blue-600 font-bold">lib:</span>
                  <span>{{ col.name }}</span>
                </div>
                
                <!-- Vault Header -->
                <div v-else class="flex items-center justify-between">
                  <div class="flex items-center gap-1">
                    <span :class="knownSecretNames[col.id]?.errorMessage ? 'text-rose-600' : 'text-slate-900'">{{ col.name }}</span>
                    <button 
                      v-if="knownSecretNames[col.id]?.errorMessage" 
                      @click.stop="emit('grant-access')" 
                      class="text-[10px] text-blue-600 underline hover:text-blue-800 ml-1 mt-0.5"
                    >
                      Grant Access
                    </button>
                    <!-- Toggle Associated Library Button -->
                    <button 
                      v-if="currentTab === 'devops' && hasAssociatedLibrary(col.name)"
                      @click="toggleAssociatedLibrary(col.name)"
                      class="transition-colors p-0.5 ml-1 flex items-center justify-center rounded shadow-sm border"
                      :class="!isLibraryVisible(col.name) ? 'text-blue-600 border-blue-300 bg-blue-50 hover:bg-blue-100 hover:text-blue-800' : 'text-slate-400 border-slate-200 bg-white hover:text-slate-600 hover:bg-slate-50'"
                      :title="isLibraryVisible(col.name) ? 'Hide Associated Library' : 'Show Associated Library'"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="4" y="5" width="2" height="14" rx="0.5" />
                        <rect x="11" y="5" width="2" height="14" rx="0.5" />
                        <rect x="18" y="5" width="2" height="14" rx="0.5" transform="rotate(-15 19 12)" />
                      </svg>
                    </button>
                  </div>
                  <button 
                    @click="vaultStore.fetchValuesForVault(col.id)" 
                    class="text-slate-400 hover:text-blue-600 transition-colors bg-white rounded-full p-1 shadow-sm border border-slate-200 ml-2"
                    title="Fetch values for this vault"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                  </button>
                </div>
              </th>
            </template>
            <template v-else>
              <th v-for="col in codeColumns" :key="col.id" class="px-3 py-1.5 text-xs font-semibold tracking-wider bg-slate-50 text-slate-700 text-center border-l border-slate-100">
                <div class="flex items-center justify-center gap-1.5">
                  <span>{{ col.icon }}</span>
                  <span>{{ col.label }}</span>
                </div>
              </th>
            </template>
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
                <span class="pr-2 line-clamp-2 break-all whitespace-normal transition-all" :class="{'blur-[3px] opacity-60 select-none': uiSettings.demoMode}" :title="row.secretName">{{ row.secretName }}</span>
                <button 
                  @click="vaultStore.fetchValuesForRow(row.secretName)"
                  class="fetch-btn text-slate-400 hover:text-blue-600 transition-colors bg-white rounded-full p-1.5 shadow-sm border border-slate-200 flex-shrink-0"
                  title="Fetch values for this row"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                </button>
              </div>
            </td>

            <template v-if="currentTab !== 'code'">
              <template v-for="col in visibleColumns" :key="col.id">
                <!-- Group Cell -->
                <td 
                  v-if="col.type === 'group'"
                  class="px-2 py-1 text-xs border-r border-blue-50/50 bg-blue-50/20 group-hover:bg-blue-50/40 transition-colors relative text-center"
                >
                  <div v-if="row.libraryValues?.[col.id]" class="flex items-center justify-center gap-1 text-emerald-600 font-bold">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                    </svg>
                    <span v-if="row.libraryValues[col.id].enabled === false" class="text-amber-500 text-xs ml-1 flex items-center" title="Disabled in ADO">
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                      </svg>
                    </span>
                  </div>
                  <div v-else-if="isMissingInGroupButInVault(row, col.group)" class="text-rose-500 font-bold text-lg cursor-help" title="Missing in Library, but present in Vault">
                    -
                  </div>
                  <div v-else class="text-slate-300 font-bold text-lg">
                    -
                  </div>
                </td>
                
                <!-- Vault Cell -->
                <td 
                  v-else 
                  class="px-2 py-1 text-xs border-r border-slate-100 bg-white group-hover:bg-slate-50 transition-colors relative focus:outline-none focus:ring-2 focus:ring-inset focus:ring-blue-400 group/cell cursor-cell"
                  tabindex="0"
                  @dblclick="row.vaultValues[col.id]?.value && toggleHighlight(row.vaultValues[col.id]?.value)"
                  @keydown.ctrl.c.prevent="handleCopy(col.id, row.secretName, row.vaultValues[col.id]?.value)"
                  @keydown.meta.c.prevent="handleCopy(col.id, row.secretName, row.vaultValues[col.id]?.value)"
                  @keydown.ctrl.v.prevent="handlePaste(col.id, row.secretName, row.vaultValues[col.id])"
                  @keydown.meta.v.prevent="handlePaste(col.id, row.secretName, row.vaultValues[col.id])"
                  :class="[getCellClasses(row.vaultValues[col.id]), copiedCell?.uri === col.id && copiedCell?.secretName === row.secretName ? '!outline-dashed !outline-2 !outline-blue-500 !outline-offset-[-2px] z-30' : '']"
                >
                <button 
                  v-if="row.vaultValues[col.id]?.isStaged"
                  @click.stop="stagedStore.revertChange(col.id, row.secretName)"
                  class="absolute right-2 top-1/2 -translate-y-1/2 opacity-0 group-hover/cell:opacity-100 bg-white shadow border border-slate-200 rounded p-1.5 text-slate-500 hover:text-amber-600 hover:bg-amber-50 hover:border-amber-300 transition-all z-20"
                  title="Revert Change"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
                  </svg>
                </button>
                <div class="flex items-center justify-center gap-2">
                  <button 
                    v-if="row.vaultValues[col.id]?.status === 'Not Retrieved'" 
                    @click.stop="vaultStore.fetchValuesForVaultAndNames(col.id, [row.secretName])" 
                    class="text-slate-300 hover:text-blue-600 transition-colors hover:bg-slate-50 rounded-full p-1.5 border border-transparent hover:border-slate-200 mx-auto"
                    :disabled="loadingCells[col.id]?.[row.secretName]"
                    :class="{'opacity-50 cursor-not-allowed': loadingCells[col.id]?.[row.secretName]}"
                    title="Fetch value"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                  </button>
                  <span v-else-if="loadingCells[col.id]?.[row.secretName] && !row.vaultValues[col.id]?.value && row.vaultValues[col.id]?.status !== 'Missing' && row.vaultValues[col.id]?.status !== 'Error'" class="text-blue-500 italic text-sm font-medium flex items-center gap-1 mx-auto">
                    <svg class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                  </span>
                  <span v-else-if="row.vaultValues[col.id]?.status === 'Missing'" class="text-slate-300 font-bold mx-auto text-lg">
                    -
                  </span>
                  <span v-else-if="row.vaultValues[col.id]?.status === 'Forbidden'" class="text-red-600 font-bold text-sm bg-red-50 px-2 py-1 rounded cursor-help shadow-sm border border-red-200" :title="row.vaultValues[col.id]?.errorMessage">
                    [403 Forbidden]
                  </span>
                  <span v-else-if="row.vaultValues[col.id]?.status === 'Error'" class="text-rose-500 italic text-sm font-medium">
                    Error
                  </span>
                  <span v-else class="font-mono tracking-widest font-semibold flex items-center gap-2 px-1.5 py-0.5 rounded transition-all duration-200" :class="[uiSettings.colorMatchByRow ? getValueColor(row.vaultValues[col.id]?.colorIndex) : '', {'bg-yellow-100 ring-2 ring-yellow-400 shadow-sm': highlightedValue === row.vaultValues[col.id]?.value, 'opacity-40 grayscale': loadingCells[col.id]?.[row.secretName]}]">
                    <template v-if="visibleSecrets.has(row.secretName)">
                      <span class="tracking-normal block max-w-[250px] overflow-x-auto align-bottom secret-scroll pb-0.5 transition-all" :class="{'blur-[3px] opacity-60 select-none': uiSettings.demoMode}">{{ row.vaultValues[col.id]?.value }}</span>
                      <span 
                        v-if="row.vaultValues[col.id]?.identiconEmoji"  
                        class="cursor-pointer hover:scale-125 transition-transform text-lg drop-shadow-sm ml-1"
                        title="Value Identicon"
                        @click.stop="toggleHighlight(row.vaultValues[col.id]?.value)"
                      >
                        {{ row.vaultValues[col.id]?.identiconEmoji }}
                      </span>
                    </template>
                    <template v-else>
                      <span class="block max-w-[250px] overflow-x-auto align-bottom secret-scroll pb-0.5">******</span>
                      <span 
                        v-if="row.vaultValues[col.id]?.identiconEmoji" 
                        class="cursor-pointer hover:scale-125 transition-transform text-lg drop-shadow-sm ml-1"
                        title="Value Identicon"
                        @click.stop="toggleHighlight(row.vaultValues[col.id]?.value)"
                      >
                        {{ row.vaultValues[col.id]?.identiconEmoji }}
                      </span>
                    </template>
                    
                    <span 
                      v-if="row.vaultValues[col.id]?.inspections?.length"
                      class="ml-1.5 cursor-help flex items-center justify-center rounded-full transition-transform hover:scale-110 drop-shadow-sm w-5 h-5 ring-1 bg-black ring-green-400 shrink-0"
                      :class="{
                        'text-[#00FFFF]': row.vaultValues[col.id]?.highestSeverity === 'Low',
                        'text-[#FFFF00]': row.vaultValues[col.id]?.highestSeverity === 'Medium',
                        'text-[#FF8800]': row.vaultValues[col.id]?.highestSeverity === 'High',
                        'text-[#FF0000]': row.vaultValues[col.id]?.highestSeverity === 'Critical'
                      }"
                      :title="(row.vaultValues[col.id]?.inspections || []).map(i => `• [${i.severity}] ${i.ruleName}: ${i.message}`).join('\n')"
                    >
                      <span class="text-[11px] font-bold uppercase leading-none flex items-center justify-center h-full w-full pb-[1px]">
                        {{ row.vaultValues[col.id]?.highestSeverity?.substring(0, 1) }}
                      </span>
                    </span>
                    
                    <svg v-if="loadingCells[col.id]?.[row.secretName]" class="animate-spin h-3.5 w-3.5 text-blue-500 ml-1 shrink-0" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                  </span>
                </div>
                
                <div 
                  v-if="getUsageForCell(col.id, row.secretName)"
                  class="absolute bottom-0 right-0 text-[10px] text-slate-500 font-medium bg-slate-100/80 px-1 py-0.5 rounded-tl-md border-t border-l border-slate-200 cursor-help backdrop-blur-sm"
                  :title="getUsageForCell(col.id, row.secretName)?.fullDate"
                >
                  {{ getUsageForCell(col.id, row.secretName)?.text }}
                </div>
              </td>
            </template>
            </template>
            <template v-else>
              <td 
                v-for="col in codeColumns" 
                :key="col.id"
                class="px-2 py-1 border-r border-slate-100 bg-white group-hover:bg-slate-50 transition-colors relative"
              >
                <div class="flex items-center justify-center h-full w-full">
                  <button 
                    @click.stop="openGithubSearch(row.secretName, col.id)"
                    class="text-slate-400 hover:text-slate-900 bg-slate-50 hover:bg-white border border-slate-200 hover:border-slate-300 shadow-sm rounded-md px-2 py-1 flex items-center justify-center gap-1.5 transition-all opacity-40 group-hover:opacity-100 focus:opacity-100 outline-none focus:ring-2 focus:ring-blue-500"
                    title="Search across GitHub"
                  >
                    <svg viewBox="0 0 24 24" class="h-4 w-4 fill-current" aria-hidden="true"><path fill-rule="evenodd" clip-rule="evenodd" d="M12 2C6.477 2 2 6.477 2 12c0 4.42 2.865 8.166 6.839 9.489.5.092.682-.217.682-.482 0-.237-.008-.866-.013-1.7-2.782.603-3.369-1.34-3.369-1.34-.454-1.156-1.11-1.462-1.11-1.462-.908-.62.069-.608.069-.608 1.003.07 1.531 1.03 1.531 1.03.892 1.529 2.341 1.087 2.91.831.092-.646.35-1.086.636-1.336-2.22-.253-4.555-1.11-4.555-4.943 0-1.091.39-1.984 1.029-2.683-.103-.253-.446-1.27.098-2.647 0 0 .84-.269 2.75 1.025A9.578 9.578 0 0112 6.836c.85.004 1.705.114 2.504.336 1.909-1.294 2.747-1.025 2.747-1.025.546 1.379.203 2.394.1 2.647.64.699 1.028 1.592 1.028 2.683 0 3.842-2.339 4.687-4.566 4.935.359.309.678.919.678 1.852 0 1.336-.012 2.415-.012 2.743 0 .267.18.578.688.48C19.138 20.161 22 16.416 22 12c0-5.523-4.477-10-10-10z"></path></svg>
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 opacity-60" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                    </svg>
                  </button>
                </div>
              </td>
            </template>
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
