<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useSettingsStore } from '../../stores/settingsStore';
import { useDevopsDataStore } from '../../stores/devopsDataStore';
import { useAuthStore } from '../../stores/authStore';
import { useAzureConnectionStore } from '../../stores/azureConnectionStore';
import CurrentUserAvatar from '../Layout/CurrentUserAvatar.vue';

type SectionTab = 'azure-cloud' | 'azure-devops' | 'github' | 'security-storage' | 'cache' | 'ui-appearance';

const props = withDefaults(defineProps<{
  show: boolean;
  initialTab?: SectionTab;
}>(), {
  initialTab: 'azure-cloud'
});

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const settingsStore = useSettingsStore();
const devopsDataStore = useDevopsDataStore();
const authStore = useAuthStore();
const azureConnectionStore = useAzureConnectionStore();

const azureAuthTimeoutSeconds = ref(4);

// Navigation Tabs
const activeTab = ref<SectionTab>(props.initialTab || 'azure-cloud');
const searchQuery = ref('');

// Azure Cloud Form State
const azureTenantId = ref('');
const azureClientId = ref('');
const azureClientSecret = ref('');
const showAzureClientSecret = ref(false);
const hasAzureClientSecret = ref(false);
const clearAzureClientSecret = ref(false);
const showAdvancedSp = ref(false);

// Azure DevOps Form State
const adoOrganization = ref('');
const adoProject = ref('');
const adoPat = ref('');
const showAdoPat = ref(false);
const hasAdoPat = ref(false);
const clearAdoPat = ref(false);

// GitHub Form State
const githubBaseUrl = ref('https://github.com');
const githubOrganization = ref('');
const githubToken = ref('');
const showGithubToken = ref(false);
const hasGithubToken = ref(false);
const clearGithubToken = ref(false);

interface StorageProviderInfo {
  name: string;
  displayName: string;
  description: string;
  isSupported: boolean;
  isActive: boolean;
}

const activeStorageProvider = ref('WindowsCredentialManager');
const storageProviders = ref<StorageProviderInfo[]>([]);
const isWindows = ref(true);

// Encrypted Cache State (Option 1)
interface CacheCategoryStat {
  category: string;
  itemCount: number;
}
interface CacheStats {
  isEnabled: boolean;
  isUnlocked: boolean;
  totalItems: number;
  fileSizeBytes: number;
  filePath: string;
  categories: CacheCategoryStat[];
}

const cacheEnabled = ref(true);
const cachePipelinesTtlMinutes = ref(60);
const cachePipelineRunsTtlMinutes = ref(30);
const cacheVaultKeysTtlMinutes = ref(120);
const cacheVariableGroupsTtlMinutes = ref(60);

const cacheStats = ref<CacheStats | null>(null);
const isLoadingCacheStats = ref(false);
const isClearingCache = ref(false);
const clearingCategory = ref<string | null>(null);
const cacheActionMessage = ref<{ type: 'success' | 'error'; text: string } | null>(null);

const formatBytes = (bytes: number): string => {
  if (!bytes || bytes <= 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
};

const fetchCacheStats = async () => {
  if (!azureConnectionStore.canAccessEncryptedCache) {
    cacheStats.value = null;
    return;
  }
  isLoadingCacheStats.value = true;
  try {
    const res = await fetch('/api/cache/stats');
    if (res.ok) {
      cacheStats.value = await res.json();
    }
  } catch (err: any) {
    console.warn('Could not load cache stats:', err);
  } finally {
    isLoadingCacheStats.value = false;
  }
};

const handleClearCache = async (category?: string) => {
  isClearingCache.value = true;
  clearingCategory.value = category || 'all';
  cacheActionMessage.value = null;
  try {
    const url = category ? `/api/cache/clear?category=${encodeURIComponent(category)}` : '/api/cache/clear';
    const res = await fetch(url, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to clear cache');
    const data = await res.json();
    cacheActionMessage.value = {
      type: 'success',
      text: data.message || (category ? `Cache category "${category}" cleared.` : 'All cached data cleared.')
    };
    await fetchCacheStats();
    setTimeout(() => {
      if (cacheActionMessage.value?.type === 'success') {
        cacheActionMessage.value = null;
      }
    }, 4000);
  } catch (err: any) {
    cacheActionMessage.value = {
      type: 'error',
      text: err.message || 'Error clearing cache'
    };
  } finally {
    isClearingCache.value = false;
    clearingCategory.value = null;
  }
};

// Action States
const isSaving = ref(false);
const saveSuccess = ref(false);
const saveError = ref('');

// Test Connection States
const isTestingAzure = ref(false);
const azureTestResult = ref<{ success: boolean; message: string } | null>(null);

const isTestingAdo = ref(false);
const adoTestResult = ref<{ success: boolean; message: string; authMethod?: string } | null>(null);

const isTestingGithub = ref(false);
const githubTestResult = ref<{ success: boolean; message: string } | null>(null);

const loadSettingsFromBackend = async () => {
  try {
    const res = await fetch('/api/settings');
    if (!res.ok) return;
    const data = await res.json();

    azureTenantId.value = data.azureTenantId || '';
    azureClientId.value = data.azureClientId || '';
    hasAzureClientSecret.value = !!data.hasAzureClientSecret;
    clearAzureClientSecret.value = false;
    azureClientSecret.value = '';

    if (data.azureTenantId || data.azureClientId || data.hasAzureClientSecret) {
      showAdvancedSp.value = true;
    }

    adoOrganization.value = data.adoOrganization || devopsDataStore.organization || '';
    adoProject.value = data.adoProject || devopsDataStore.project || '';
    hasAdoPat.value = !!data.hasAdoPat;
    clearAdoPat.value = false;
    adoPat.value = '';

    githubBaseUrl.value = data.githubBaseUrl || settingsStore.uiSettings.githubBaseUrl || 'https://github.com';
    githubOrganization.value = data.githubOrganization || settingsStore.uiSettings.githubOrg || '';
    hasGithubToken.value = !!data.hasGithubToken;
    clearGithubToken.value = false;
    githubToken.value = '';

    activeStorageProvider.value = data.activeStorageProvider || 'WindowsCredentialManager';
    storageProviders.value = data.storageProviders || [];
    isWindows.value = data.isWindows ?? true;

    cacheEnabled.value = data.cacheEnabled ?? true;
    cachePipelinesTtlMinutes.value = data.cachePipelinesTtlMinutes ?? 60;
    cachePipelineRunsTtlMinutes.value = data.cachePipelineRunsTtlMinutes ?? 30;
    cacheVaultKeysTtlMinutes.value = data.cacheVaultKeysTtlMinutes ?? 120;
    cacheVariableGroupsTtlMinutes.value = data.cacheVariableGroupsTtlMinutes ?? 60;
    azureAuthTimeoutSeconds.value = data.azureAuthTimeoutSeconds ?? 4;
    azureConnectionStore.setTimeoutSeconds(azureAuthTimeoutSeconds.value);
    fetchCacheStats();
  } catch (err: any) {
    console.warn('Could not load backend settings:', err);
  }
};

watch(() => props.show, (newVal) => {
  if (newVal) {
    if (props.initialTab) {
      activeTab.value = props.initialTab;
    }
    loadSettingsFromBackend();
    fetchCacheStats();
    saveSuccess.value = false;
    saveError.value = '';
    azureTestResult.value = null;
    adoTestResult.value = null;
    githubTestResult.value = null;
  }
});

watch(() => props.initialTab, (newTab) => {
  if (newTab) {
    activeTab.value = newTab;
    if (newTab === 'cache') {
      fetchCacheStats();
    }
  }
});

onMounted(() => {
  if (props.show) {
    loadSettingsFromBackend();
    fetchCacheStats();
  }
});

const handleSignIn = async () => {
  azureTestResult.value = null;
  const res = await authStore.loginInteractive();
  azureTestResult.value = {
    success: res.success,
    message: res.message
  };
};

const handleSignOut = async () => {
  azureTestResult.value = null;
  await authStore.logout();
  azureTestResult.value = {
    success: true,
    message: 'Signed out successfully.'
  };
};

const handleSave = async () => {
  isSaving.value = true;
  saveError.value = '';
  saveSuccess.value = false;

  try {
    const payload = {
      azureTenantId: azureTenantId.value.trim() || null,
      azureClientId: azureClientId.value.trim() || null,
      azureClientSecret: azureClientSecret.value.trim() ? azureClientSecret.value.trim() : null,
      clearAzureClientSecret: clearAzureClientSecret.value,
      adoOrganization: adoOrganization.value.trim(),
      adoProject: adoProject.value.trim(),
      adoPat: adoPat.value.trim() ? adoPat.value.trim() : null,
      clearAdoPat: clearAdoPat.value,
      githubBaseUrl: githubBaseUrl.value.trim() || 'https://github.com',
      githubOrganization: githubOrganization.value.trim(),
      githubToken: githubToken.value.trim() ? githubToken.value.trim() : null,
      clearGithubToken: clearGithubToken.value,
      storageProvider: activeStorageProvider.value,
      cacheEnabled: cacheEnabled.value,
      cachePipelinesTtlMinutes: Number(cachePipelinesTtlMinutes.value) || 60,
      cachePipelineRunsTtlMinutes: Number(cachePipelineRunsTtlMinutes.value) || 30,
      cacheVaultKeysTtlMinutes: Number(cacheVaultKeysTtlMinutes.value) || 120,
      cacheVariableGroupsTtlMinutes: Number(cacheVariableGroupsTtlMinutes.value) || 60,
      azureAuthTimeoutSeconds: Number(azureAuthTimeoutSeconds.value) || 4
    };

    const res = await fetch('/api/settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new Error(errData.error || `Server responded with ${res.status}`);
    }

    // Sync frontend stores
    devopsDataStore.organization = adoOrganization.value.trim();
    devopsDataStore.project = adoProject.value.trim();
    localStorage.setItem('adoOrg', adoOrganization.value.trim());
    localStorage.setItem('adoProject', adoProject.value.trim());

    settingsStore.uiSettings.githubBaseUrl = githubBaseUrl.value.trim();
    settingsStore.uiSettings.githubOrg = githubOrganization.value.trim();
    if (githubOrganization.value.trim()) {
      settingsStore.uiSettings.useGithubOrg = true;
    }
    settingsStore.saveUiSettings();

    // Re-check Azure Cloud connection with newly saved credentials
    await authStore.retryAuth();
    if (adoOrganization.value.trim()) {
      await authStore.checkAdoConnection(adoOrganization.value.trim(), adoProject.value.trim());
    }

    saveSuccess.value = true;
    await loadSettingsFromBackend();
    setTimeout(() => {
      saveSuccess.value = false;
    }, 3000);
  } catch (err: any) {
    saveError.value = err.message || 'Failed to save settings';
  } finally {
    isSaving.value = false;
  }
};

const testAzureConnection = async () => {
  isTestingAzure.value = true;
  azureTestResult.value = null;

  try {
    const res = await fetch('/api/settings/test-azure', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        tenantId: azureTenantId.value.trim() || null,
        clientId: azureClientId.value.trim() || null,
        clientSecret: azureClientSecret.value.trim() || null
      })
    });

    const data = await res.json();
    azureTestResult.value = {
      success: !!data.success,
      message: data.message || (data.success ? 'Connected successfully to Azure' : 'Azure connection failed')
    };
    if (data.success) {
      await authStore.retryAuth();
    }
  } catch (err: any) {
    azureTestResult.value = {
      success: false,
      message: err.message || 'Failed to test Azure connection'
    };
  } finally {
    isTestingAzure.value = false;
  }
};

const testAdoConnection = async () => {
  isTestingAdo.value = true;
  adoTestResult.value = null;

  try {
    const res = await fetch('/api/settings/test-ado', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        organization: adoOrganization.value.trim(),
        project: adoProject.value.trim(),
        pat: adoPat.value.trim() ? adoPat.value.trim() : null
      })
    });

    const data = await res.json();
    adoTestResult.value = {
      success: !!data.success,
      message: data.message || (data.success ? 'Connected successfully' : 'Connection failed'),
      authMethod: data.authMethod
    };
    if (data.success) {
      authStore.azureDevOps.isConnected = true;
      authStore.azureDevOps.authMethod = data.authMethod || 'AzureSSO';
      authStore.azureDevOps.connectionMessage = data.message;
    }
  } catch (err: any) {
    adoTestResult.value = {
      success: false,
      message: err.message || 'Failed to test ADO connection'
    };
  } finally {
    isTestingAdo.value = false;
  }
};

const testGithubConnection = async () => {
  isTestingGithub.value = true;
  githubTestResult.value = null;

  try {
    const res = await fetch('/api/settings/test-github', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        baseUrl: githubBaseUrl.value.trim() || 'https://github.com',
        organization: githubOrganization.value.trim() || null,
        token: githubToken.value.trim() ? githubToken.value.trim() : null
      })
    });

    const data = await res.json();
    githubTestResult.value = {
      success: !!data.success,
      message: data.message || (data.success ? 'Connected successfully' : 'Connection failed')
    };
  } catch (err: any) {
    githubTestResult.value = {
      success: false,
      message: err.message || 'Failed to test GitHub connection'
    };
  } finally {
    isTestingGithub.value = false;
  }
};

// Search Matching
const matchesSearch = (text: string) => {
  if (!searchQuery.value.trim()) return true;
  const q = searchQuery.value.toLowerCase();
  return text.toLowerCase().includes(q);
};

const showAzureCloudSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'azure-cloud';
  return matchesSearch('azure cloud key vault tenant client id secret service principal arm subscription sso login');
});

const showAdoSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'azure-devops';
  return matchesSearch('azure devops ado personal access token pat organization project build release pipeline sso');
});

const showGithubSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'github';
  return matchesSearch('github token pat organization base url enterprise repo code search');
});

const showStorageSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'security-storage';
  return matchesSearch('security storage windows credential manager dpapi aes-256-gcm docker linux encrypt plaintext secret');
});

const showCacheSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'cache';
  return matchesSearch('cache ttl encrypted memory disk expires pipelines runs key vault secrets variable groups purge wipe');
});

const showUiSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'ui-appearance';
  return matchesSearch('ui appearance demo mode blur limit layout grouping font');
});
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6 bg-slate-950/60 backdrop-blur-sm animate-fade-in" @click="emit('close')">
    <div 
      class="bg-white rounded-2xl shadow-2xl w-full max-w-5xl h-[88vh] max-h-[850px] flex flex-col overflow-hidden border border-slate-200"
      @click.stop
    >
      <!-- IDE Header -->
      <div class="px-5 py-3 border-b border-slate-200 bg-slate-50 flex items-center justify-between gap-4 shrink-0">
        <!-- Title & Breadcrumb -->
        <div class="flex items-center gap-3">
          <div class="p-1.5 bg-blue-100 text-blue-700 rounded-lg">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="font-bold text-slate-800 text-base">Settings</span>
              <kbd class="px-1.5 py-0.5 text-[10px] font-mono text-slate-500 bg-slate-200/80 rounded border border-slate-300">Ctrl+,</kbd>
            </div>
            <p class="text-xs text-slate-500">Configure connection methods, SSO credentials, and encrypted storage</p>
          </div>
        </div>

        <!-- Search Bar -->
        <div class="flex-1 max-w-md relative">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>
          <input 
            v-model="searchQuery" 
            type="text" 
            placeholder="Search settings (e.g. sso, pat, devops, github, storage)..." 
            class="w-full pl-9 pr-8 py-1.5 text-xs bg-white border border-slate-300 rounded-lg text-slate-800 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all shadow-inner"
          />
          <button 
            v-if="searchQuery" 
            @click="searchQuery = ''" 
            class="absolute inset-y-0 right-0 pr-2.5 flex items-center text-slate-400 hover:text-slate-600"
          >
            <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <!-- Close Button -->
        <button 
          @click="emit('close')" 
          class="text-slate-400 hover:text-slate-600 p-1.5 rounded-lg hover:bg-slate-200/70 transition-colors"
          title="Close Settings (Esc)"
        >
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- Main Body: Two-Column IDE Panel -->
      <div class="flex flex-1 overflow-hidden">
        <!-- Left Sidebar (Sections) -->
        <div class="w-64 border-r border-slate-200 bg-slate-50/70 p-3 space-y-1 shrink-0 flex flex-col justify-between">
          <nav class="space-y-1">
            <!-- Azure Cloud (Key Vaults & ARM) -->
            <button 
              @click="activeTab = 'azure-cloud'; searchQuery = ''"
              class="w-full flex items-center justify-between px-3 py-2 text-xs font-semibold rounded-lg transition-all"
              :class="activeTab === 'azure-cloud' && !searchQuery ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-200/60 hover:text-slate-900'"
            >
              <div class="flex items-center gap-2.5">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 15a4 4 0 004 4h9a5 5 0 10-.1-9.999 5.002 5.002 0 10-9.78 2.096A4.001 4.001 0 003 15z" />
                </svg>
                <span>Azure Cloud (Key Vaults)</span>
              </div>
              <span 
                v-if="authStore.isCloudConnected" 
                class="w-2 h-2 rounded-full bg-emerald-400" 
                title="Connected"
              ></span>
              <span 
                v-else-if="hasAzureClientSecret" 
                class="w-2 h-2 rounded-full bg-blue-400" 
                title="Service Principal configured"
              ></span>
            </button>

            <!-- Azure DevOps -->
            <button 
              @click="activeTab = 'azure-devops'; searchQuery = ''"
              class="w-full flex items-center justify-between px-3 py-2 text-xs font-semibold rounded-lg transition-all"
              :class="activeTab === 'azure-devops' && !searchQuery ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-200/60 hover:text-slate-900'"
            >
              <div class="flex items-center gap-2.5">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
                </svg>
                <span>Azure DevOps (ADO)</span>
              </div>
              <span 
                v-if="authStore.isAdoConnected" 
                class="w-2 h-2 rounded-full bg-emerald-400" 
                :title="authStore.adoAuthMethod === 'PAT' ? 'Connected via PAT' : 'Connected via Azure SSO'"
              ></span>
            </button>

            <!-- GitHub -->
            <button 
              @click="activeTab = 'github'; searchQuery = ''"
              class="w-full flex items-center justify-between px-3 py-2 text-xs font-semibold rounded-lg transition-all"
              :class="activeTab === 'github' && !searchQuery ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-200/60 hover:text-slate-900'"
            >
              <div class="flex items-center gap-2.5">
                <svg viewBox="0 0 16 16" class="h-4 w-4 fill-current">
                  <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"></path>
                </svg>
                <span>GitHub</span>
              </div>
              <span 
                v-if="hasGithubToken" 
                class="w-2 h-2 rounded-full bg-emerald-400" 
                title="Token Configured"
              ></span>
            </button>

            <!-- Security & Storage -->
            <button 
              @click="activeTab = 'security-storage'; searchQuery = ''"
              class="w-full flex items-center justify-between px-3 py-2 text-xs font-semibold rounded-lg transition-all"
              :class="activeTab === 'security-storage' && !searchQuery ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-200/60 hover:text-slate-900'"
            >
              <div class="flex items-center gap-2.5">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                </svg>
                <span>Security &amp; Storage</span>
              </div>
            </button>

            <!-- Encrypted Cache -->
            <button 
              @click="activeTab = 'cache'; searchQuery = ''; fetchCacheStats()"
              class="w-full flex items-center justify-between px-3 py-2 text-xs font-semibold rounded-lg transition-all"
              :class="activeTab === 'cache' && !searchQuery ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-200/60 hover:text-slate-900'"
            >
              <div class="flex items-center gap-2.5">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4m0 5c0 2.21-3.582 4-8 4s-8-1.79-8-4" />
                </svg>
                <span>Encrypted Cache</span>
              </div>
              <span 
                v-if="cacheStats?.isUnlocked" 
                class="w-2 h-2 rounded-full bg-emerald-400" 
                title="Session Unlocked & Active"
              ></span>
              <span 
                v-else-if="cacheStats?.isEnabled" 
                class="w-2 h-2 rounded-full bg-slate-300" 
                title="Encrypted & Sealed (Login Required)"
              ></span>
            </button>

            <!-- UI & Behavior -->
            <button 
              @click="activeTab = 'ui-appearance'; searchQuery = ''"
              class="w-full flex items-center justify-between px-3 py-2 text-xs font-semibold rounded-lg transition-all"
              :class="activeTab === 'ui-appearance' && !searchQuery ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-600 hover:bg-slate-200/60 hover:text-slate-900'"
            >
              <div class="flex items-center gap-2.5">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4" />
                </svg>
                <span>UI &amp; Display</span>
              </div>
            </button>
          </nav>

          <!-- Status Card in Left Nav -->
          <div class="p-2.5 bg-slate-200/50 rounded-xl border border-slate-200 text-[11px] text-slate-600 space-y-1.5">
            <div class="font-semibold text-slate-800 flex items-center gap-1.5">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              Encrypted At Rest
            </div>
            <p class="text-[10px] text-slate-500 leading-tight">
              {{ activeStorageProvider === 'WindowsCredentialManager' ? 'Windows Credential Manager (DPAPI)' : 'AES-256-GCM Encrypted File' }}
            </p>
          </div>
        </div>

        <!-- Right Content Area -->
        <div class="flex-1 overflow-y-auto p-6 space-y-8 bg-white">
          <!-- AZURE CLOUD (KEY VAULTS & ARM) SECTION -->
          <section v-if="showAzureCloudSection" class="space-y-5 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-blue-600"></span>
                Azure Cloud (Key Vaults &amp; ARM) Authentication
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">Manages identity and access to Azure Resource Manager and Key Vaults</p>
            </div>

            <!-- Active Connection Status Card -->
            <div 
              class="p-4 rounded-xl border transition-all"
              :class="authStore.isCloudConnected ? 'bg-emerald-50/70 border-emerald-200' : 'bg-slate-50 border-slate-200'"
            >
              <div class="flex items-start justify-between gap-4 flex-wrap">
                <div>
                  <div class="flex items-center gap-2">
                    <span 
                      class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold"
                      :class="authStore.isCloudConnected ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'"
                    >
                      <span class="w-2 h-2 rounded-full" :class="authStore.isCloudConnected ? 'bg-emerald-500' : 'bg-slate-400'"></span>
                      {{ authStore.isCloudConnected ? 'Connected to Azure Cloud' : 'Not Connected' }}
                    </span>
                    <span v-if="authStore.cloudAuthMethod" class="text-xs font-medium text-slate-500">
                      Method: <strong class="text-slate-800 font-semibold">{{ authStore.cloudAuthMethod === 'ServicePrincipal' ? 'Service Principal (App ID & Secret)' : 'Azure SSO Identity (Interactive)' }}</strong>
                    </span>
                  </div>

                  <!-- Details when connected -->
                  <div v-if="authStore.isCloudConnected && authStore.profile" class="mt-2.5 flex items-center gap-3">
                    <CurrentUserAvatar 
                      :profile="authStore.profile" 
                      :cloud-auth-method="authStore.cloudAuthMethod" 
                      size="lg" 
                    />
                    <div class="text-xs text-slate-700 space-y-0.5 min-w-0">
                      <div>User: <strong class="font-semibold text-slate-900">{{ authStore.profile.email }}</strong></div>
                      <div>Default Subscription: <span class="font-medium text-slate-600">{{ authStore.profile.subscriptionName }}</span></div>
                      <div>Available Subscriptions: <span class="font-medium text-slate-600">{{ authStore.subscriptions.length }}</span></div>
                    </div>
                  </div>

                  <!-- Guidance when disconnected -->
                  <div v-else class="mt-2 text-xs text-slate-600">
                    Sign in with your corporate account via browser. No Azure Administrator permissions are required.
                  </div>
                </div>

                <!-- Primary Action Buttons -->
                <div class="flex items-center gap-2">
                  <button 
                    v-if="!authStore.isCloudConnected"
                    type="button"
                    @click="handleSignIn"
                    :disabled="authStore.isCloudConnecting"
                    class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-bold shadow-sm transition-all flex items-center gap-2 disabled:opacity-50"
                  >
                    <svg v-if="authStore.isCloudConnecting" class="animate-spin h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <svg v-else class="h-4 w-4" viewBox="0 0 23 23">
                      <path fill="#f25022" d="M1 1h10v10H1z"/>
                      <path fill="#00a4ef" d="M1 12h10v10H1z"/>
                      <path fill="#7fba00" d="M12 1h10v10H12z"/>
                      <path fill="#ffb900" d="M12 12h10v10H12z"/>
                    </svg>
                    <span>{{ authStore.isCloudConnecting ? 'Opening Browser SSO...' : 'Sign in with Microsoft' }}</span>
                  </button>

                  <template v-else>
                    <button 
                      type="button"
                      @click="handleSignIn"
                      :disabled="authStore.isCloudConnecting"
                      class="px-3 py-1.5 border border-slate-300 hover:bg-slate-100 text-slate-700 rounded-lg text-xs font-semibold transition-colors"
                      title="Switch account"
                    >
                      Switch Account
                    </button>
                    <button 
                      type="button"
                      @click="handleSignOut"
                      class="px-3 py-1.5 border border-rose-200 text-rose-700 hover:bg-rose-50 rounded-lg text-xs font-semibold transition-colors"
                      title="Sign out from Azure"
                    >
                      Sign Out
                    </button>
                  </template>
                </div>
              </div>

              <!-- Test / Action result message -->
              <div v-if="azureTestResult" class="mt-3 pt-2 border-t border-slate-200/60 text-xs flex items-center gap-1.5 font-medium" :class="azureTestResult.success ? 'text-emerald-700' : 'text-rose-700'">
                <svg v-if="azureTestResult.success" class="w-4 h-4 text-emerald-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                <svg v-else class="w-4 h-4 text-rose-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
                <span>{{ azureTestResult.message }}</span>
              </div>
            </div>

            <!-- Fast Connection & Timeout Card -->
            <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between gap-4">
              <div class="space-y-0.5">
                <label class="text-xs font-bold text-slate-800 block">Fast Probe Timeout</label>
                <div class="text-[11px] text-slate-500">
                  Maximum time the startup state machine waits when checking Azure credentials before displaying the sign-in prompt (default: 4s).
                </div>
              </div>
              <div class="flex items-center gap-1.5 shrink-0">
                <input 
                  v-model.number="azureAuthTimeoutSeconds" 
                  type="number" 
                  min="1" 
                  max="30" 
                  class="w-20 text-xs bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 text-right font-semibold"
                />
                <span class="text-xs text-slate-500 font-medium">sec</span>
              </div>
            </div>

            <!-- Advanced / Service Principal Accordion -->
            <div class="border border-slate-200 rounded-xl overflow-hidden">
              <button 
                type="button" 
                @click="showAdvancedSp = !showAdvancedSp"
                class="w-full px-4 py-3 bg-slate-50 hover:bg-slate-100 flex items-center justify-between text-left transition-colors"
              >
                <div>
                  <div class="text-xs font-bold text-slate-800 flex items-center gap-2">
                    <span>Advanced: Automated Service Principal (Docker / CI / Headless)</span>
                    <span v-if="hasAzureClientSecret" class="px-2 py-0.5 rounded text-[10px] font-bold bg-blue-100 text-blue-800">Active</span>
                  </div>
                  <div class="text-[11px] text-slate-500 mt-0.5">
                    Only required for unattended runners or cross-tenant automation where browser login is unavailable
                  </div>
                </div>
                <svg 
                  class="w-4 h-4 text-slate-500 transition-transform" 
                  :class="showAdvancedSp ? 'rotate-180' : ''" 
                  fill="none" 
                  viewBox="0 0 24 24" 
                  stroke="currentColor"
                >
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              <div v-if="showAdvancedSp" class="p-4 space-y-4 bg-white border-t border-slate-200">
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="block text-xs font-semibold text-slate-700 mb-1">Tenant ID</label>
                    <input 
                      v-model="azureTenantId" 
                      type="text" 
                      placeholder="e.g. 00000000-0000-0000-0000-000000000000 or mytenant.onmicrosoft.com" 
                      class="w-full text-xs bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
                    />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-slate-700 mb-1">Application (Client) ID</label>
                    <input 
                      v-model="azureClientId" 
                      type="text" 
                      placeholder="e.g. 00000000-0000-0000-0000-000000000000" 
                      class="w-full text-xs bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
                    />
                  </div>
                </div>

                <!-- Client Secret Input -->
                <div class="space-y-2">
                  <div class="flex items-center justify-between">
                    <label class="block text-xs font-bold text-slate-800">Client Secret (Password)</label>
                    <div class="flex items-center gap-2">
                      <span 
                        v-if="hasAzureClientSecret && !clearAzureClientSecret" 
                        class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800"
                      >
                        Stored Encrypted
                      </span>
                    </div>
                  </div>

                  <div class="relative flex items-center">
                    <input 
                      v-model="azureClientSecret" 
                      :type="showAzureClientSecret ? 'text' : 'password'" 
                      :placeholder="hasAzureClientSecret && !clearAzureClientSecret ? '•••••••••••••••••••••••••••••••••••••••• (Leave blank to keep current)' : 'Paste your Client Secret here...'" 
                      class="w-full text-xs font-mono bg-white border border-slate-300 rounded-lg pl-3 pr-20 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
                    />
                    <div class="absolute right-2 flex items-center gap-1">
                      <button 
                        type="button" 
                        @click="showAzureClientSecret = !showAzureClientSecret" 
                        class="text-slate-400 hover:text-slate-600 p-1 text-[11px] font-medium"
                      >
                        {{ showAzureClientSecret ? 'Hide' : 'Show' }}
                      </button>
                      <button 
                        v-if="hasAzureClientSecret && !clearAzureClientSecret" 
                        type="button" 
                        @click="clearAzureClientSecret = true; azureClientSecret = ''" 
                        class="text-rose-600 hover:text-rose-800 p-1 text-[11px] font-semibold"
                      >
                        Remove
                      </button>
                      <button 
                        v-if="clearAzureClientSecret" 
                        type="button" 
                        @click="clearAzureClientSecret = false" 
                        class="text-slate-500 hover:text-slate-700 p-1 text-[11px] font-medium"
                      >
                        Undo
                      </button>
                    </div>
                  </div>
                  <div v-if="clearAzureClientSecret" class="text-[11px] text-rose-600 font-medium">
                    Client secret marked for deletion. Click Save to confirm.
                  </div>
                </div>

                <div class="flex items-center justify-between pt-1">
                  <button 
                    type="button"
                    @click="testAzureConnection" 
                    :disabled="isTestingAzure" 
                    class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-300 hover:border-slate-400 text-slate-700 bg-white hover:bg-slate-50 transition-colors flex items-center gap-2 shadow-sm disabled:opacity-50"
                  >
                    <svg v-if="isTestingAzure" class="animate-spin h-3.5 w-3.5 text-slate-600" fill="none" viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <span>{{ isTestingAzure ? 'Testing Service Principal...' : 'Test Service Principal' }}</span>
                  </button>
                </div>
              </div>
            </div>
          </section>

          <!-- AZURE DEVOPS (ADO) SECTION -->
          <section v-if="showAdoSection" class="space-y-5 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-indigo-600"></span>
                Azure DevOps (ADO) Configuration
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">Used specifically for ADO Pipelines, Variable Groups, and Release Matrices</p>
            </div>

            <!-- ADO Connection Status Box -->
            <div 
              class="p-4 rounded-xl border transition-all"
              :class="authStore.isAdoConnected ? 'bg-indigo-50/70 border-indigo-200' : 'bg-slate-50 border-slate-200'"
            >
              <div class="flex items-start justify-between gap-4 flex-wrap">
                <div>
                  <div class="flex items-center gap-2">
                    <span 
                      class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold"
                      :class="authStore.isAdoConnected ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'"
                    >
                      <span class="w-2 h-2 rounded-full" :class="authStore.isAdoConnected ? 'bg-emerald-500' : 'bg-slate-400'"></span>
                      {{ authStore.isAdoConnected ? 'Connected to Azure DevOps' : 'Not Connected' }}
                    </span>
                    <span class="text-xs font-medium text-slate-500">
                      Method: <strong class="text-slate-800 font-semibold">{{ authStore.adoAuthMethod === 'PAT' ? 'Personal Access Token (PAT Override)' : 'Azure SSO Identity (Recommended)' }}</strong>
                    </span>
                  </div>

                  <div v-if="authStore.azureDevOps.connectionMessage" class="mt-2 text-xs font-medium text-slate-700">
                    {{ authStore.azureDevOps.connectionMessage }}
                  </div>
                  <div v-else class="mt-2 text-xs text-slate-500">
                    ADO automatically uses your primary Azure SSO identity. Enter a PAT below only if your organization requires dedicated access.
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <button 
                    type="button"
                    @click="testAdoConnection" 
                    :disabled="isTestingAdo || !adoOrganization" 
                    class="px-3.5 py-1.5 text-xs font-semibold rounded-lg border border-slate-300 hover:border-slate-400 text-slate-700 bg-white hover:bg-slate-50 transition-colors flex items-center gap-2 shadow-sm disabled:opacity-50"
                  >
                    <svg v-if="isTestingAdo" class="animate-spin h-3.5 w-3.5 text-slate-600" fill="none" viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <span>{{ isTestingAdo ? 'Testing Connection...' : 'Test ADO Connection' }}</span>
                  </button>
                </div>
              </div>

              <!-- Test feedback -->
              <div v-if="adoTestResult" class="mt-3 pt-2 border-t border-slate-200/60 text-xs flex items-center gap-1.5 font-medium" :class="adoTestResult.success ? 'text-emerald-700' : 'text-rose-700'">
                <svg v-if="adoTestResult.success" class="w-4 h-4 text-emerald-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                <svg v-else class="w-4 h-4 text-rose-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
                <span>{{ adoTestResult.message }}</span>
              </div>
            </div>

            <!-- ADO Org & Project Inputs -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Default Organization</label>
                <input 
                  v-model="adoOrganization" 
                  type="text" 
                  placeholder="e.g. MyFintechBank" 
                  class="w-full text-xs bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
                />
                <span class="text-[10px] text-slate-400 mt-1 block">Your Azure DevOps organization name in dev.azure.com/{org}</span>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Default Project</label>
                <input 
                  v-model="adoProject" 
                  type="text" 
                  placeholder="e.g. PaymentServices" 
                  class="w-full text-xs bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
                />
                <span class="text-[10px] text-slate-400 mt-1 block">Default project containing pipelines and variable groups</span>
              </div>
            </div>

            <!-- ADO PAT Override Input -->
            <div class="p-4 bg-slate-50 rounded-xl border border-slate-200 space-y-3">
              <div class="flex items-center justify-between">
                <div>
                  <label class="block text-xs font-bold text-slate-800">Personal Access Token (PAT Override)</label>
                  <p class="text-[11px] text-slate-500">Optional: Leave blank to use Azure SSO identity automatically</p>
                </div>
                <div class="flex items-center gap-2">
                  <span 
                    v-if="hasAdoPat && !clearAdoPat" 
                    class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800"
                  >
                    PAT Stored Encrypted
                  </span>
                  <span 
                    v-else 
                    class="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-medium bg-slate-200 text-slate-600"
                  >
                    Using Azure SSO Default
                  </span>
                </div>
              </div>

              <div class="relative flex items-center">
                <input 
                  v-model="adoPat" 
                  :type="showAdoPat ? 'text' : 'password'" 
                  :placeholder="hasAdoPat && !clearAdoPat ? '•••••••••••••••••••••••••••••••••••••••• (Leave blank to keep current)' : 'Optional: Paste PAT here if Azure SSO lacks access...'" 
                  class="w-full text-xs font-mono bg-white border border-slate-300 rounded-lg pl-3 pr-20 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
                />
                <div class="absolute right-2 flex items-center gap-1">
                  <button 
                    type="button" 
                    @click="showAdoPat = !showAdoPat" 
                    class="text-slate-400 hover:text-slate-600 p-1 text-[11px] font-medium"
                  >
                    {{ showAdoPat ? 'Hide' : 'Show' }}
                  </button>
                  <button 
                    v-if="hasAdoPat && !clearAdoPat" 
                    type="button" 
                    @click="clearAdoPat = true; adoPat = ''" 
                    class="text-rose-600 hover:text-rose-800 p-1 text-[11px] font-semibold"
                  >
                    Remove
                  </button>
                  <button 
                    v-if="clearAdoPat" 
                    type="button" 
                    @click="clearAdoPat = false" 
                    class="text-slate-500 hover:text-slate-700 p-1 text-[11px] font-medium"
                  >
                    Undo
                  </button>
                </div>
              </div>
              <div v-if="clearAdoPat" class="text-[11px] text-rose-600 font-medium">
                PAT marked for deletion. Will revert to Azure SSO on save.
              </div>
            </div>
          </section>

          <!-- GITHUB SECTION -->
          <section v-if="showGithubSection" class="space-y-5 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-slate-900"></span>
                GitHub Configuration
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">Used for pipeline YAML exploration, Enterprise instances, and code search</p>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">GitHub Base URL</label>
                <input 
                  v-model="githubBaseUrl" 
                  type="text" 
                  placeholder="https://github.com" 
                  class="w-full text-xs bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
                />
                <span class="text-[10px] text-slate-400 mt-1 block">Default is https://github.com or your GitHub Enterprise Server URL</span>
              </div>
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Default Organization</label>
                <input 
                  v-model="githubOrganization" 
                  type="text" 
                  placeholder="e.g. microsoft or my-org" 
                  class="w-full text-xs bg-slate-50 border border-slate-300 rounded-lg px-3 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
                />
                <span class="text-[10px] text-slate-400 mt-1 block">Used to prefix repositories without explicit owners</span>
              </div>
            </div>

            <!-- GitHub Token Input -->
            <div class="p-4 bg-slate-50 rounded-xl border border-slate-200 space-y-3">
              <div class="flex items-center justify-between">
                <div>
                  <label class="block text-xs font-bold text-slate-800">GitHub Personal Access Token (PAT)</label>
                  <p class="text-[11px] text-slate-500">Fine-grained or classic token for private repositories &amp; increased API rate limits</p>
                </div>
                <div class="flex items-center gap-2">
                  <span 
                    v-if="hasGithubToken && !clearGithubToken" 
                    class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200"
                  >
                    <svg class="w-3 h-3 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                    </svg>
                    Stored Encrypted
                  </span>
                  <span 
                    v-else 
                    class="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-medium bg-slate-200 text-slate-600"
                  >
                    Not Configured
                  </span>
                </div>
              </div>

              <div class="relative flex items-center">
                <input 
                  v-model="githubToken" 
                  :type="showGithubToken ? 'text' : 'password'" 
                  :placeholder="hasGithubToken && !clearGithubToken ? '•••••••••••••••••••••••••••••••••••••••• (Leave blank to keep current)' : 'Paste your GitHub token here...'" 
                  class="w-full text-xs font-mono bg-white border border-slate-300 rounded-lg pl-3 pr-20 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
                />
                <div class="absolute right-2 flex items-center gap-1">
                  <button 
                    type="button" 
                    @click="showGithubToken = !showGithubToken" 
                    class="text-slate-400 hover:text-slate-600 p-1 text-[11px] font-medium"
                    :title="showGithubToken ? 'Hide Token' : 'Show Token'"
                  >
                    {{ showGithubToken ? 'Hide' : 'Show' }}
                  </button>
                  <button 
                    v-if="hasGithubToken && !clearGithubToken" 
                    type="button" 
                    @click="clearGithubToken = true; githubToken = ''" 
                    class="text-rose-600 hover:text-rose-800 p-1 text-[11px] font-semibold"
                    title="Remove stored token"
                  >
                    Remove
                  </button>
                  <button 
                    v-if="clearGithubToken" 
                    type="button" 
                    @click="clearGithubToken = false" 
                    class="text-slate-500 hover:text-slate-700 p-1 text-[11px] font-medium"
                  >
                    Undo
                  </button>
                </div>
              </div>

              <div v-if="clearGithubToken" class="text-[11px] text-rose-600 font-medium">
                GitHub token marked for deletion. Click Save to confirm.
              </div>

              <!-- Test GitHub Button -->
              <div class="flex items-center justify-between pt-1">
                <button 
                  type="button"
                  @click="testGithubConnection" 
                  :disabled="isTestingGithub" 
                  class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-300 hover:border-slate-400 text-slate-700 bg-white hover:bg-slate-50 transition-colors flex items-center gap-2 shadow-sm disabled:opacity-50"
                >
                  <svg v-if="isTestingGithub" class="animate-spin h-3.5 w-3.5 text-slate-600" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  <span>{{ isTestingGithub ? 'Testing GitHub...' : 'Test Connection' }}</span>
                </button>

                <div v-if="githubTestResult" class="text-xs flex items-center gap-1.5 font-medium" :class="githubTestResult.success ? 'text-emerald-700' : 'text-rose-700'">
                  <svg v-if="githubTestResult.success" class="w-4 h-4 text-emerald-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                  </svg>
                  <svg v-else class="w-4 h-4 text-rose-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                  <span>{{ githubTestResult.message }}</span>
                </div>
              </div>
            </div>
          </section>

          <!-- SECURITY & STORAGE SECTION -->
          <section v-if="showStorageSection" class="space-y-5 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-emerald-600"></span>
                Secret Storage Provider (Zero Plaintext)
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">Choose where and how encrypted secrets are stored across Windows 11 and Linux Docker</p>
            </div>

            <!-- Zero Plaintext Banner -->
            <div class="p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-xs text-emerald-950 flex items-start gap-3">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-emerald-600 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
              <div class="space-y-1">
                <div class="font-bold text-emerald-900">Guaranteed Encrypted Storage:</div>
                <p>Secrets (Azure Client Secret, Azure DevOps PAT, and GitHub token) are never written to disk in plain text. When switching providers, stored secrets are automatically migrated to the new store.</p>
              </div>
            </div>

            <!-- Provider Selection Cards -->
            <div class="space-y-3">
              <label class="block text-xs font-bold text-slate-800">Active Storage Provider</label>
              
              <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                <!-- Windows Credential Manager Card -->
                <div 
                  @click="isWindows && (activeStorageProvider = 'WindowsCredentialManager')"
                  class="p-4 rounded-xl border transition-all cursor-pointer relative"
                  :class="[
                    activeStorageProvider === 'WindowsCredentialManager' 
                      ? 'border-blue-600 bg-blue-50/50 shadow-sm ring-1 ring-blue-600' 
                      : 'border-slate-200 bg-white hover:border-slate-300',
                    !isWindows ? 'opacity-50 cursor-not-allowed' : ''
                  ]"
                >
                  <div class="flex items-start justify-between">
                    <div class="flex items-center gap-2">
                      <div class="w-4 h-4 rounded-full border flex items-center justify-center" :class="activeStorageProvider === 'WindowsCredentialManager' ? 'border-blue-600 bg-blue-600' : 'border-slate-300'">
                        <div v-if="activeStorageProvider === 'WindowsCredentialManager'" class="w-1.5 h-1.5 rounded-full bg-white"></div>
                      </div>
                      <span class="font-bold text-xs text-slate-900">Windows Credential Manager</span>
                    </div>
                    <span v-if="isWindows" class="text-[10px] font-bold px-2 py-0.5 rounded bg-blue-100 text-blue-700">Windows 11 Native</span>
                    <span v-else class="text-[10px] font-bold px-2 py-0.5 rounded bg-slate-200 text-slate-600">Unavailable in Linux/Docker</span>
                  </div>
                  <p class="text-[11px] text-slate-500 mt-2">
                    Stores tokens inside the Win32 Credential Manager (<code class="font-mono text-slate-700">Advapi32.dll</code>), encrypted with DPAPI under your Windows user account.
                  </p>
                </div>

                <!-- Encrypted File Store (AES-GCM) Card -->
                <div 
                  @click="activeStorageProvider = 'EncryptedFileStore'"
                  class="p-4 rounded-xl border transition-all cursor-pointer relative"
                  :class="activeStorageProvider === 'EncryptedFileStore' 
                    ? 'border-blue-600 bg-blue-50/50 shadow-sm ring-1 ring-blue-600' 
                    : 'border-slate-200 bg-white hover:border-slate-300'"
                >
                  <div class="flex items-start justify-between">
                    <div class="flex items-center gap-2">
                      <div class="w-4 h-4 rounded-full border flex items-center justify-center" :class="activeStorageProvider === 'EncryptedFileStore' ? 'border-blue-600 bg-blue-600' : 'border-slate-300'">
                        <div v-if="activeStorageProvider === 'EncryptedFileStore'" class="w-1.5 h-1.5 rounded-full bg-white"></div>
                      </div>
                      <span class="font-bold text-xs text-slate-900">Encrypted File (AES-256-GCM)</span>
                    </div>
                    <span class="text-[10px] font-bold px-2 py-0.5 rounded bg-emerald-100 text-emerald-700">Cross-Platform / Docker</span>
                  </div>
                  <p class="text-[11px] text-slate-500 mt-2">
                    Stores authenticated ciphertext in <code class="font-mono text-slate-700">secrets.enc</code>. Fully compatible with Linux Docker containers and Windows without plain text.
                  </p>
                </div>
              </div>
            </div>

            <!-- Docker Note -->
            <div class="p-3 bg-slate-50 rounded-xl border border-slate-200 text-xs text-slate-600 space-y-1">
              <span class="font-semibold text-slate-800">Docker Container Deployment:</span>
              <p class="text-[11px] text-slate-500">
                When running KeyVaultComparer in a Linux container, set <code class="font-mono bg-slate-200 px-1 py-0.5 rounded text-slate-800">KEYVAULTCOMPARER_MASTER_KEY</code> (or mount a Docker secret) to supply the master key, and mount a volume at <code class="font-mono bg-slate-200 px-1 py-0.5 rounded text-slate-800">/root/.keyvaultcomparer</code>. The store uses AES-256-GCM authenticated encryption.
              </p>
            </div>
          </section>

          <!-- ENCRYPTED CACHE SECTION (OPTION 1) -->
          <section v-if="showCacheSection" class="space-y-6 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-emerald-600"></span>
                Encrypted Cache &amp; Cold-Start Acceleration
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">
                AES-256-GCM local cache keyed to your Azure Entra identity. Instantly loads previous state across all tabs upon sign-in with zero redundant Azure API calls.
              </p>
            </div>

            <!-- Session Security Status Banner -->
            <div 
              class="p-4 rounded-xl border transition-all"
              :class="cacheStats?.isUnlocked ? 'bg-emerald-50/70 border-emerald-200' : 'bg-amber-50/70 border-amber-200'"
            >
              <div class="flex items-start justify-between gap-4 flex-wrap">
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span 
                      class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold"
                      :class="cacheStats?.isUnlocked ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'"
                    >
                      <span class="w-2 h-2 rounded-full" :class="cacheStats?.isUnlocked ? 'bg-emerald-500' : 'bg-amber-500'"></span>
                      {{ cacheStats?.isUnlocked ? 'Cache Unlocked (Active Azure Session)' : 'Cache Sealed (Login Required)' }}
                    </span>
                    <span class="text-xs font-medium text-slate-500">
                      Storage: <strong class="text-slate-800 font-semibold font-mono">cache.enc</strong>
                    </span>
                  </div>
                  <p class="text-xs text-slate-600 max-w-2xl">
                    <template v-if="cacheStats?.isUnlocked">
                      Decryption key is resident only in volatile RAM, derived via HKDF(SHA256) from your Azure Entra token + DPAPI machine entropy. Safe from offline extraction; completely cleared on logout or app shutdown.
                    </template>
                    <template v-else>
                      The cache file on disk is sealed with AES-256-GCM authenticated ciphertext. Keys are generated strictly in memory upon completing Microsoft SSO sign-in.
                    </template>
                  </p>
                </div>

                <div class="flex items-center gap-2">
                  <button 
                    type="button" 
                    @click="fetchCacheStats" 
                    :disabled="isLoadingCacheStats"
                    class="px-3 py-1.5 border border-slate-300 hover:bg-slate-100 text-slate-700 rounded-lg text-xs font-semibold transition-colors flex items-center gap-1.5 disabled:opacity-50"
                    title="Refresh cache metrics"
                  >
                    <svg class="h-3.5 w-3.5 text-slate-500" :class="{ 'animate-spin': isLoadingCacheStats }" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                    </svg>
                    <span>Refresh</span>
                  </button>
                </div>
              </div>
            </div>

            <!-- Metrics Overview Cards -->
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <!-- Total Items -->
              <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200">
                <div class="text-[11px] font-semibold text-slate-500 uppercase tracking-wide">Cached Entries</div>
                <div class="mt-1 text-xl font-bold text-slate-900 flex items-baseline gap-1.5">
                  <span>{{ cacheStats?.totalItems ?? 0 }}</span>
                  <span class="text-xs font-normal text-slate-500">items</span>
                </div>
              </div>

              <!-- Disk Usage -->
              <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200">
                <div class="text-[11px] font-semibold text-slate-500 uppercase tracking-wide">Encrypted Size</div>
                <div class="mt-1 text-xl font-bold text-slate-900 flex items-baseline gap-1.5">
                  <span>{{ formatBytes(cacheStats?.fileSizeBytes ?? 0) }}</span>
                </div>
              </div>

              <!-- Cache Switch Status -->
              <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200">
                <div class="text-[11px] font-semibold text-slate-500 uppercase tracking-wide">Cache Mode</div>
                <div class="mt-1 flex items-center gap-1.5">
                  <span class="w-2 h-2 rounded-full" :class="cacheEnabled ? 'bg-emerald-500' : 'bg-slate-400'"></span>
                  <span class="text-sm font-bold" :class="cacheEnabled ? 'text-emerald-700' : 'text-slate-500'">
                    {{ cacheEnabled ? 'Enabled' : 'Bypassed' }}
                  </span>
                </div>
              </div>

              <!-- Cryptographic Standard -->
              <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200">
                <div class="text-[11px] font-semibold text-slate-500 uppercase tracking-wide">Protection</div>
                <div class="mt-1 text-xs font-bold text-slate-800">AES-256-GCM</div>
                <div class="text-[10px] text-slate-500 font-mono">HKDF + DPAPI</div>
              </div>
            </div>

            <!-- Master Toggle -->
            <div class="flex items-center justify-between p-4 bg-slate-50 rounded-xl border border-slate-200">
              <div class="space-y-0.5">
                <div class="text-xs font-bold text-slate-800">Enable Local Encrypted Cache</div>
                <div class="text-[11px] text-slate-500">
                  When enabled, Key Vault names, subscriptions, ADO pipelines, matrix rows, and variable groups are served from disk immediately upon startup.
                </div>
              </div>
              <button 
                type="button"
                @click="cacheEnabled = !cacheEnabled"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none"
                :class="cacheEnabled ? 'bg-blue-600' : 'bg-slate-300'"
              >
                <span 
                  class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                  :class="cacheEnabled ? 'translate-x-6' : 'translate-x-1'"
                />
              </button>
            </div>

            <!-- Time-To-Live (TTL) Settings -->
            <div class="space-y-3">
              <div class="border-b border-slate-200 pb-1">
                <h5 class="text-xs font-bold text-slate-800 uppercase tracking-wider">Expiration &amp; Refresh Durations (TTL)</h5>
                <p class="text-[11px] text-slate-500 mt-0.5">
                  Set how many minutes cached items remain valid before being quietly re-fetched in the background.
                </p>
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
                <!-- Pipelines Catalog TTL -->
                <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between gap-4">
                  <div>
                    <label class="text-xs font-bold text-slate-800 block">Pipelines Catalog</label>
                    <span class="text-[11px] text-slate-500 block mt-0.5">ADO pipeline definitions &amp; IDs</span>
                  </div>
                  <div class="flex items-center gap-1.5 shrink-0">
                    <input 
                      v-model.number="cachePipelinesTtlMinutes" 
                      type="number" 
                      min="1" 
                      max="10080" 
                      class="w-20 text-xs bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 text-right font-semibold"
                    />
                    <span class="text-xs text-slate-500 font-medium">min</span>
                  </div>
                </div>

                <!-- Pipeline Runs & Matrix TTL -->
                <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between gap-4">
                  <div>
                    <label class="text-xs font-bold text-slate-800 block">Matrix Runs &amp; Rollback History</label>
                    <span class="text-[11px] text-slate-500 block mt-0.5">Cross-env deployment rows &amp; N-1 builds</span>
                  </div>
                  <div class="flex items-center gap-1.5 shrink-0">
                    <input 
                      v-model.number="cachePipelineRunsTtlMinutes" 
                      type="number" 
                      min="1" 
                      max="10080" 
                      class="w-20 text-xs bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 text-right font-semibold"
                    />
                    <span class="text-xs text-slate-500 font-medium">min</span>
                  </div>
                </div>

                <!-- Vault Keys TTL -->
                <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between gap-4">
                  <div>
                    <label class="text-xs font-bold text-slate-800 block">Vault Secret Names &amp; Keys</label>
                    <span class="text-[11px] text-slate-500 block mt-0.5">Discovered secret names per vault URI</span>
                  </div>
                  <div class="flex items-center gap-1.5 shrink-0">
                    <input 
                      v-model.number="cacheVaultKeysTtlMinutes" 
                      type="number" 
                      min="1" 
                      max="10080" 
                      class="w-20 text-xs bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 text-right font-semibold"
                    />
                    <span class="text-xs text-slate-500 font-medium">min</span>
                  </div>
                </div>

                <!-- Variable Groups TTL -->
                <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between gap-4">
                  <div>
                    <label class="text-xs font-bold text-slate-800 block">ADO Variable Groups</label>
                    <span class="text-[11px] text-slate-500 block mt-0.5">Library variable inventories &amp; key maps</span>
                  </div>
                  <div class="flex items-center gap-1.5 shrink-0">
                    <input 
                      v-model.number="cacheVariableGroupsTtlMinutes" 
                      type="number" 
                      min="1" 
                      max="10080" 
                      class="w-20 text-xs bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 text-right font-semibold"
                    />
                    <span class="text-xs text-slate-500 font-medium">min</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Categories Breakdown & Purge Controls -->
            <div class="space-y-3">
              <div class="flex items-center justify-between border-b border-slate-200 pb-1">
                <div>
                  <h5 class="text-xs font-bold text-slate-800 uppercase tracking-wider">Cached Categories &amp; Granular Purging</h5>
                  <p class="text-[11px] text-slate-500 mt-0.5">
                    Clear specific cached categories or wipe the entire encrypted storage file.
                  </p>
                </div>
                <button 
                  type="button" 
                  @click="handleClearCache()" 
                  :disabled="isClearingCache || (cacheStats?.totalItems ?? 0) === 0"
                  class="px-3 py-1.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 disabled:opacity-40"
                  title="Purge all items from local encrypted cache"
                >
                  <svg v-if="isClearingCache && clearingCategory === 'all'" class="animate-spin h-3.5 w-3.5 text-rose-600" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  <svg v-else class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                  <span>Clear Entire Cache</span>
                </button>
              </div>

              <!-- Action notification message -->
              <div 
                v-if="cacheActionMessage" 
                class="p-2.5 rounded-lg text-xs font-semibold flex items-center gap-2 animate-fade-in"
                :class="cacheActionMessage.type === 'success' ? 'bg-emerald-50 text-emerald-800 border border-emerald-200' : 'bg-rose-50 text-rose-800 border border-rose-200'"
              >
                <svg v-if="cacheActionMessage.type === 'success'" class="w-4 h-4 text-emerald-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                <svg v-else class="w-4 h-4 text-rose-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
                <span>{{ cacheActionMessage.text }}</span>
              </div>

              <!-- Categories List Grid -->
              <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2.5">
                <!-- Pipelines -->
                <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 flex items-center justify-between">
                  <div class="min-w-0 pr-2">
                    <div class="text-xs font-bold text-slate-800 truncate">Pipelines</div>
                    <div class="text-[10px] text-slate-500">
                      {{ cacheStats?.categories?.find(c => c.category === 'Pipelines')?.itemCount ?? 0 }} items cached
                    </div>
                  </div>
                  <button 
                    type="button"
                    @click="handleClearCache('Pipelines')"
                    :disabled="isClearingCache || !(cacheStats?.categories?.find(c => c.category === 'Pipelines')?.itemCount)"
                    class="px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 rounded transition-colors disabled:opacity-30 disabled:pointer-events-none"
                  >
                    Purge
                  </button>
                </div>

                <!-- PipelineRuns -->
                <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 flex items-center justify-between">
                  <div class="min-w-0 pr-2">
                    <div class="text-xs font-bold text-slate-800 truncate">Pipeline Runs &amp; Matrix</div>
                    <div class="text-[10px] text-slate-500">
                      {{ cacheStats?.categories?.find(c => c.category === 'PipelineRuns')?.itemCount ?? 0 }} items cached
                    </div>
                  </div>
                  <button 
                    type="button"
                    @click="handleClearCache('PipelineRuns')"
                    :disabled="isClearingCache || !(cacheStats?.categories?.find(c => c.category === 'PipelineRuns')?.itemCount)"
                    class="px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 rounded transition-colors disabled:opacity-30 disabled:pointer-events-none"
                  >
                    Purge
                  </button>
                </div>

                <!-- VaultKeys -->
                <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 flex items-center justify-between">
                  <div class="min-w-0 pr-2">
                    <div class="text-xs font-bold text-slate-800 truncate">Vault Keys &amp; Names</div>
                    <div class="text-[10px] text-slate-500">
                      {{ cacheStats?.categories?.find(c => c.category === 'VaultKeys')?.itemCount ?? 0 }} items cached
                    </div>
                  </div>
                  <button 
                    type="button"
                    @click="handleClearCache('VaultKeys')"
                    :disabled="isClearingCache || !(cacheStats?.categories?.find(c => c.category === 'VaultKeys')?.itemCount)"
                    class="px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 rounded transition-colors disabled:opacity-30 disabled:pointer-events-none"
                  >
                    Purge
                  </button>
                </div>

                <!-- VaultDiscovery -->
                <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 flex items-center justify-between">
                  <div class="min-w-0 pr-2">
                    <div class="text-xs font-bold text-slate-800 truncate">Vault Discovery</div>
                    <div class="text-[10px] text-slate-500">
                      {{ cacheStats?.categories?.find(c => c.category === 'VaultDiscovery')?.itemCount ?? 0 }} items cached
                    </div>
                  </div>
                  <button 
                    type="button"
                    @click="handleClearCache('VaultDiscovery')"
                    :disabled="isClearingCache || !(cacheStats?.categories?.find(c => c.category === 'VaultDiscovery')?.itemCount)"
                    class="px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 rounded transition-colors disabled:opacity-30 disabled:pointer-events-none"
                  >
                    Purge
                  </button>
                </div>

                <!-- Subscriptions -->
                <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 flex items-center justify-between">
                  <div class="min-w-0 pr-2">
                    <div class="text-xs font-bold text-slate-800 truncate">Subscriptions</div>
                    <div class="text-[10px] text-slate-500">
                      {{ cacheStats?.categories?.find(c => c.category === 'Subscriptions')?.itemCount ?? 0 }} items cached
                    </div>
                  </div>
                  <button 
                    type="button"
                    @click="handleClearCache('Subscriptions')"
                    :disabled="isClearingCache || !(cacheStats?.categories?.find(c => c.category === 'Subscriptions')?.itemCount)"
                    class="px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 rounded transition-colors disabled:opacity-30 disabled:pointer-events-none"
                  >
                    Purge
                  </button>
                </div>

                <!-- VariableGroups -->
                <div class="p-3 bg-slate-50 rounded-lg border border-slate-200 flex items-center justify-between">
                  <div class="min-w-0 pr-2">
                    <div class="text-xs font-bold text-slate-800 truncate">Variable Groups</div>
                    <div class="text-[10px] text-slate-500">
                      {{ cacheStats?.categories?.find(c => c.category === 'VariableGroups')?.itemCount ?? 0 }} items cached
                    </div>
                  </div>
                  <button 
                    type="button"
                    @click="handleClearCache('VariableGroups')"
                    :disabled="isClearingCache || !(cacheStats?.categories?.find(c => c.category === 'VariableGroups')?.itemCount)"
                    class="px-2 py-1 text-[11px] font-semibold text-slate-600 hover:text-rose-700 hover:bg-rose-50 border border-slate-200 hover:border-rose-200 rounded transition-colors disabled:opacity-30 disabled:pointer-events-none"
                  >
                    Purge
                  </button>
                </div>
              </div>
            </div>
          </section>

          <!-- UI & APPEARANCE SECTION -->
          <section v-if="showUiSection" class="space-y-5 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-purple-600"></span>
                UI &amp; Visual Preferences
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">Control demo blur modes, result limits, and table formatting</p>
            </div>

            <div class="space-y-4">
              <!-- Demo Mode Toggle -->
              <div class="flex items-center justify-between p-3.5 bg-slate-50 rounded-xl border border-slate-200">
                <div>
                  <div class="text-xs font-bold text-slate-800">Demo Privacy Mode</div>
                  <div class="text-[11px] text-slate-500">Automatically blurs secret names and values in all grids for screen sharing</div>
                </div>
                <button 
                  @click="settingsStore.uiSettings.demoMode = !settingsStore.uiSettings.demoMode"
                  class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none"
                  :class="settingsStore.uiSettings.demoMode ? 'bg-blue-600' : 'bg-slate-300'"
                >
                  <span 
                    class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                    :class="settingsStore.uiSettings.demoMode ? 'translate-x-6' : 'translate-x-1'"
                  />
                </button>
              </div>

              <!-- Result Limit -->
              <div class="p-3.5 bg-slate-50 rounded-xl border border-slate-200 flex items-center justify-between">
                <div>
                  <div class="text-xs font-bold text-slate-800">Result Limit</div>
                  <div class="text-[11px] text-slate-500">Maximum secret rows to display at one time</div>
                </div>
                <input 
                  v-model.number="settingsStore.uiSettings.resultLimit" 
                  type="number" 
                  min="10" 
                  max="1000" 
                  class="w-24 text-xs bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 text-right"
                />
              </div>

              <!-- Group DevOps Columns -->
              <div class="flex items-center justify-between p-3.5 bg-slate-50 rounded-xl border border-slate-200">
                <div>
                  <div class="text-xs font-bold text-slate-800">Group DevOps Columns</div>
                  <div class="text-[11px] text-slate-500">Group ADO library columns alongside Key Vault comparison columns</div>
                </div>
                <button 
                  @click="settingsStore.uiSettings.groupDevOpsColumns = !settingsStore.uiSettings.groupDevOpsColumns"
                  class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none"
                  :class="settingsStore.uiSettings.groupDevOpsColumns ? 'bg-blue-600' : 'bg-slate-300'"
                >
                  <span 
                    class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                    :class="settingsStore.uiSettings.groupDevOpsColumns ? 'translate-x-6' : 'translate-x-1'"
                  />
                </button>
              </div>
            </div>
          </section>
        </div>
      </div>

      <!-- Footer Bar -->
      <div class="px-6 py-3.5 bg-slate-50 border-t border-slate-200 flex items-center justify-between shrink-0">
        <!-- Status Notification -->
        <div>
          <div v-if="saveSuccess" class="text-xs font-bold text-emerald-700 flex items-center gap-1.5 animate-fade-in">
            <svg class="w-4 h-4 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
            </svg>
            Settings and encrypted secrets saved successfully.
          </div>
          <div v-else-if="saveError" class="text-xs font-bold text-rose-700 flex items-center gap-1.5 animate-fade-in">
            <svg class="w-4 h-4 text-rose-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
            {{ saveError }}
          </div>
          <div v-else class="text-[11px] text-slate-400">
            Press <kbd class="px-1 py-0.5 text-[10px] font-mono bg-slate-200 rounded">Esc</kbd> to dismiss without saving
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center gap-3">
          <button 
            type="button" 
            @click="emit('close')" 
            class="px-4 py-1.5 text-xs font-semibold text-slate-600 hover:text-slate-800 hover:bg-slate-200/60 rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button 
            type="button" 
            @click="handleSave" 
            :disabled="isSaving" 
            class="px-5 py-1.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-lg shadow-sm transition-all flex items-center gap-2 disabled:opacity-50"
          >
            <svg v-if="isSaving" class="animate-spin h-3.5 w-3.5 text-white" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            <span>{{ isSaving ? 'Saving...' : 'Save Settings' }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
