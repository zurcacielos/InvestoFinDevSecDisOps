<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useAuthStore } from '../../stores/authStore';
import { useDataStore } from '../../stores/dataStore';
import { useUiStateStore } from '../../stores/uiStateStore';
import { apiFetch } from '../../services/apiClient';

const emit = defineEmits<{
  (e: 'grant-access'): void;
}>();

const authStore = useAuthStore();
const { subscriptions } = storeToRefs(authStore);

const dataStore = useDataStore();
const { vaultUris, knownSecretNames, lastFetched } = storeToRefs(dataStore);

const uiStateStore = useUiStateStore();
const { fetchingVaults } = storeToRefs(uiStateStore);

const isFetchingAnyVault = computed(() => Object.values(fetchingVaults.value).some(v => v));

const selectedSubscriptionId = ref(localStorage.getItem('selectedSub') || '');
const searchQuery = ref('');
const showDropdown = ref(false);
const availableVaults = ref<{ name: string; uri: string }[]>([]);
const loadingVaults = ref(false);
const totalVaultsCount = ref<number | null>(null);
let debounceTimer: ReturnType<typeof setTimeout> | null = null;
const loadingNames = ref(false);

const getVaultName = (uri: string) => {
  try {
    return new URL(uri).hostname.split('.')[0];
  } catch {
    return uri;
  }
};

const getRelativeTime = (timestamp: number) => {
  const diff = Math.floor((Date.now() - timestamp) / 60000);
  if (diff < 1) return 'just now';
  if (diff === 1) return '1 min ago';
  if (diff < 60) return `${diff} mins ago`;
  const hours = Math.floor(diff / 60);
  if (hours === 1) return '1 hour ago';
  if (hours < 24) return `${hours} hours ago`;
  return '1+ day ago';
};

watch(selectedSubscriptionId, async (newId) => {
  localStorage.setItem('selectedSub', newId);
  availableVaults.value = [];
  totalVaultsCount.value = null;
  if (newId) {
    try {
      const response = await apiFetch(`/api/vaults?subscriptionId=${encodeURIComponent(newId)}`);
      if (response.ok) {
        const vaults = await response.json();
        totalVaultsCount.value = vaults.length;
      }
    } catch(e) {}
  }
}, { immediate: true });

const unselectedAvailableVaults = computed(() => {
  return availableVaults.value.filter(v => !vaultUris.value.includes(v.uri));
});

const searchVaults = async () => {
  if (debounceTimer) clearTimeout(debounceTimer);
  
  if (!(await authStore.connectToAzure())) return;
  
  const query = searchQuery.value.trim();
  if (query.length < 2) {
    availableVaults.value = [];
    showDropdown.value = false;
    return;
  }

  showDropdown.value = true;
  loadingVaults.value = true;

  debounceTimer = setTimeout(async () => {
    try {
      let url = `/api/vaults?query=${encodeURIComponent(query)}`;
      if (selectedSubscriptionId.value) {
        url += `&subscriptionId=${encodeURIComponent(selectedSubscriptionId.value)}`;
      }
      const response = await apiFetch(url);
      if (response.ok) {
        availableVaults.value = await response.json();
      }
    } catch (error) {
      console.error('Failed to search vaults', error);
    } finally {
      loadingVaults.value = false;
    }
  }, 350);
};

const selectVault = (vault: { name: string; uri: string }) => {
  if (!vaultUris.value.includes(vault.uri)) {
    dataStore.addVaultUri(vault.uri);
    dataStore.fetchVaultKeys([vault.uri]);
  }
  // Do not clear search query or hide dropdown, so the user can continue selecting
};

const hideDropdown = () => {
  setTimeout(() => { showDropdown.value = false; }, 200);
};

const handleRefetchNames = async () => {
  loadingNames.value = true;
  await dataStore.refetchNames();
  loadingNames.value = false;
};

const forgetAllNames = () => {
  dataStore.clearAll();
};
</script>

<template>
  <div class="flex flex-col md:flex-row gap-3 items-start w-full">
    <!-- Subscription and Search -->
    <div class="flex flex-col gap-3 min-w-[250px] shrink-0">
      <div class="font-bold text-slate-800 text-sm">1. Select Subscriptions & Search</div>
      <div class="space-y-2">
        <select 
          v-model="selectedSubscriptionId"
          class="w-full border border-slate-300 rounded-lg px-2 py-1 text-xs text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
        >
          <option value="">All Subscriptions</option>
          <option v-for="sub in subscriptions" :key="sub.id" :value="sub.id">
            {{ sub.name }} <template v-if="totalVaultsCount !== null && selectedSubscriptionId === sub.id">({{ totalVaultsCount }})</template>
          </option>
        </select>
      </div>
      <!-- Search Input -->
      <div class="relative w-full">
        <input 
          type="text"
          v-model="searchQuery"
          @input="searchVaults"
          @focus="showDropdown = true"
          @blur="hideDropdown"
          @keydown.esc="showDropdown = false"
          placeholder="Search vaults..."
          class="w-full border border-slate-300 rounded-lg px-2 py-1 pr-10 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <div class="absolute right-3 inset-y-0 flex items-center pointer-events-none text-slate-400">
          <svg v-if="loadingVaults" class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        <ul 
          v-if="showDropdown && unselectedAvailableVaults.length > 0" 
          class="absolute z-50 w-full mt-1 bg-white border border-slate-200 shadow-lg max-h-60 rounded-md overflow-auto py-1"
        >
          <li 
            v-for="vault in unselectedAvailableVaults" 
            :key="vault.uri" 
            @mousedown.prevent="selectVault(vault)"
            class="px-2 py-1 hover:bg-blue-50 cursor-pointer text-sm text-slate-700"
          >
            {{ vault.name }}
          </li>
        </ul>
        <div 
          v-else-if="showDropdown && !loadingVaults && unselectedAvailableVaults.length === 0"
          class="absolute z-50 w-full mt-1 bg-white border border-slate-200 shadow-lg rounded-md p-3 text-sm text-slate-500 text-center"
        >
          No unselected vaults found
        </div>
      </div>
    </div>
    
    <!-- Divider -->
    <div class="hidden md:block w-px bg-slate-200 h-32 mx-2"></div>
    
    <!-- Selected Vaults -->
    <div class="flex-1 flex flex-col min-w-[300px]">
      <div class="font-bold text-slate-800 text-sm mb-1">Manage Selected Vaults</div>
      <div class="flex flex-wrap gap-2 max-h-32 overflow-y-auto pr-2">
        <div 
          v-for="(uri, index) in vaultUris" 
          :key="uri" 
          class="flex items-center justify-between px-2 py-1 bg-blue-50 text-blue-800 rounded-lg text-sm border border-blue-200 shadow-sm w-full md:w-auto min-w-[200px]"
        >
          <div class="flex flex-col min-w-0 pr-2">
            <span class="font-medium truncate">{{ getVaultName(uri) }}</span>
            <div v-if="knownSecretNames[uri]?.errorMessage" class="text-[11px] text-rose-500 font-bold mt-0.5 whitespace-normal leading-tight">
              {{ knownSecretNames[uri]?.errorMessage }}
              <button @click="emit('grant-access')" class="text-blue-600 underline hover:text-blue-800 ml-1">Grant Access</button>
            </div>
            <span v-else class="text-xs text-blue-500 mt-0.5 truncate flex items-center gap-1">
              {{ knownSecretNames[uri]?.secrets?.length || 0 }} secrets
              <template v-if="lastFetched[uri]"><span class="text-[10px]">- {{ getRelativeTime(lastFetched[uri]) }}</span></template>
            </span>
          </div>
          <div class="flex items-center gap-1 shrink-0">
            <button 
              @click="dataStore.fetchVaultKeys([uri])" 
              :disabled="fetchingVaults[uri]"
              class="text-blue-400 hover:text-blue-700 focus:outline-none transition-colors p-1 disabled:opacity-50 disabled:cursor-not-allowed"
              title="Refetch secret names"
            >
              <svg v-if="fetchingVaults[uri]" class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
              <svg v-else xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
            </button>
            <button 
              @click="dataStore.removeVault(index)" 
              class="text-blue-400 hover:text-rose-500 focus:outline-none transition-colors p-1"
              title="Remove"
            >
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Actions -->
    <div class="hidden md:block w-px bg-slate-200 h-32 mx-2"></div>
    
    <div class="flex flex-col justify-between min-w-[200px] shrink-0 self-stretch py-1">
      <div class="flex flex-col items-center gap-1">
        <button 
          @click="handleRefetchNames"
          :disabled="loadingNames || vaultUris.length === 0"
          class="w-full py-1.5 px-4 bg-blue-600 text-sm hover:bg-blue-700 text-white rounded-lg font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
        >
          <svg v-if="loadingNames" class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
          <template v-else>
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
          </template>
          Refetch Names
          <svg v-if="!loadingNames" xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 opacity-80" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 10h16M4 14h16M4 18h16" />
          </svg>
        </button>

        <button 
          @click="forgetAllNames"
          :disabled="vaultUris.length === 0 || Object.keys(knownSecretNames).length === 0"
          class="text-xs text-slate-400 hover:text-slate-600 underline decoration-slate-300 hover:decoration-slate-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-1"
          title="Clear all cached names from local storage"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
          </svg>
          Forget All Names
        </button>
      </div>

      <button 
        @click="uiStateStore.setCurrentTab('analyze')"
        :disabled="Object.keys(knownSecretNames).length === 0 || isFetchingAnyVault"
        class="w-full py-1.5 px-4 bg-emerald-600 text-sm hover:bg-emerald-700 text-white rounded-lg font-semibold shadow-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 5l7 7-7 7M5 5l7 7-7 7" />
        </svg>
        Analyze Data
      </button>
    </div>
  </div>
</template>
