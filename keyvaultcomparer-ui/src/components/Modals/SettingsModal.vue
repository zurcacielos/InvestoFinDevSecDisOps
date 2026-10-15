<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useSettingsStore } from '../../stores/settingsStore';
import { useDevopsDataStore } from '../../stores/devopsDataStore';

const props = defineProps<{
  show: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const settingsStore = useSettingsStore();
const devopsDataStore = useDevopsDataStore();

// Navigation Tabs
type SectionTab = 'azure-devops' | 'github' | 'security-storage' | 'ui-appearance';
const activeTab = ref<SectionTab>('azure-devops');
const searchQuery = ref('');

// Form State
const adoOrganization = ref('');
const adoProject = ref('');
const adoPat = ref('');
const showAdoPat = ref(false);
const hasAdoPat = ref(false);
const clearAdoPat = ref(false);

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

// Action States
const isSaving = ref(false);
const saveSuccess = ref(false);
const saveError = ref('');

// Test Connection States
const isTestingAdo = ref(false);
const adoTestResult = ref<{ success: boolean; message: string } | null>(null);

const isTestingGithub = ref(false);
const githubTestResult = ref<{ success: boolean; message: string } | null>(null);

const loadSettingsFromBackend = async () => {
  try {
    const res = await fetch('/api/settings');
    if (!res.ok) return;
    const data = await res.json();

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
  } catch (err: any) {
    console.warn('Could not load backend settings:', err);
  }
};

watch(() => props.show, (newVal) => {
  if (newVal) {
    loadSettingsFromBackend();
    saveSuccess.value = false;
    saveError.value = '';
    adoTestResult.value = null;
    githubTestResult.value = null;
  }
});

onMounted(() => {
  if (props.show) {
    loadSettingsFromBackend();
  }
});

const handleSave = async () => {
  isSaving.value = true;
  saveError.value = '';
  saveSuccess.value = false;

  try {
    const payload = {
      adoOrganization: adoOrganization.value.trim(),
      adoProject: adoProject.value.trim(),
      adoPat: adoPat.value.trim() ? adoPat.value.trim() : null,
      clearAdoPat: clearAdoPat.value,
      githubBaseUrl: githubBaseUrl.value.trim() || 'https://github.com',
      githubOrganization: githubOrganization.value.trim(),
      githubToken: githubToken.value.trim() ? githubToken.value.trim() : null,
      clearGithubToken: clearGithubToken.value,
      storageProvider: activeStorageProvider.value
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
      message: data.message || (data.success ? 'Connected successfully' : 'Connection failed')
    };
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

const showAdoSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'azure-devops';
  return matchesSearch('azure devops ado personal access token pat organization project build release pipeline');
});

const showGithubSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'github';
  return matchesSearch('github token pat organization base url enterprise repo code search');
});

const showStorageSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'security-storage';
  return matchesSearch('security storage windows credential manager dpapi aes-256-gcm docker linux encrypt plaintext secret');
});

const showUiSection = computed(() => {
  if (!searchQuery.value.trim()) return activeTab.value === 'ui-appearance';
  return matchesSearch('ui appearance demo mode blur limit layout grouping font');
});
</script>

<template>
  <div v-if="show" class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6 bg-slate-950/60 backdrop-blur-sm animate-fade-in" @click="emit('close')">
    <div 
      class="bg-white rounded-2xl shadow-2xl w-full max-w-5xl h-[86vh] max-h-[820px] flex flex-col overflow-hidden border border-slate-200"
      @click.stop
    >
      <!-- VS Code / Antigravity IDE Header -->
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
            <p class="text-xs text-slate-500">Configure credentials, tokens, endpoints and encrypted storage</p>
          </div>
        </div>

        <!-- Search Bar (IDE Style) -->
        <div class="flex-1 max-w-md relative">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>
          <input 
            v-model="searchQuery" 
            type="text" 
            placeholder="Search settings (e.g. pat, devops, github, storage)..." 
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
        <div class="w-60 border-r border-slate-200 bg-slate-50/70 p-3 space-y-1 shrink-0 flex flex-col justify-between">
          <nav class="space-y-1">
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
                <span>Azure DevOps</span>
              </div>
              <span 
                v-if="hasAdoPat" 
                class="w-2 h-2 rounded-full bg-emerald-400" 
                title="PAT Configured"
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
          <!-- AZURE DEVOPS SECTION -->
          <section v-if="showAdoSection" class="space-y-5 animate-fade-in">
            <div class="border-b border-slate-200 pb-2">
              <h4 class="text-sm font-bold text-slate-900 uppercase tracking-wider flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-blue-600"></span>
                Azure DevOps (ADO) Configuration
              </h4>
              <p class="text-xs text-slate-500 mt-0.5">Configure organization, project, and encrypted Personal Access Token (PAT)</p>
            </div>

            <!-- ADO PAT Notice -->
            <div class="p-3 bg-blue-50 border border-blue-200 rounded-xl text-xs text-blue-900 flex items-start gap-2.5">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-blue-600 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <div class="space-y-1">
                <span class="font-semibold">Authentication Flow:</span> If an Azure PAT is provided, it is securely stored in Windows Credential Manager and used via HTTP Basic Auth. If empty, the backend falls back to your local Azure CLI (<code class="bg-blue-100 px-1 py-0.5 rounded text-blue-800 font-mono">az login</code>) session.
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

            <!-- ADO PAT Input -->
            <div class="p-4 bg-slate-50 rounded-xl border border-slate-200 space-y-3">
              <div class="flex items-center justify-between">
                <div>
                  <label class="block text-xs font-bold text-slate-800">Azure DevOps Personal Access Token (PAT)</label>
                  <p class="text-[11px] text-slate-500">Scopes needed: Build (Read), Release (Read), Variable Groups (Read/Write)</p>
                </div>
                <div class="flex items-center gap-2">
                  <span 
                    v-if="hasAdoPat && !clearAdoPat" 
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
                  v-model="adoPat" 
                  :type="showAdoPat ? 'text' : 'password'" 
                  :placeholder="hasAdoPat && !clearAdoPat ? '•••••••••••••••••••••••••••••••••••••••• (Leave blank to keep current)' : 'Paste your ADO PAT here...'" 
                  class="w-full text-xs font-mono bg-white border border-slate-300 rounded-lg pl-3 pr-20 py-2 text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-500 transition-colors"
                />
                <div class="absolute right-2 flex items-center gap-1">
                  <button 
                    type="button" 
                    @click="showAdoPat = !showAdoPat" 
                    class="text-slate-400 hover:text-slate-600 p-1 text-[11px] font-medium"
                    :title="showAdoPat ? 'Hide PAT' : 'Show PAT'"
                  >
                    {{ showAdoPat ? 'Hide' : 'Show' }}
                  </button>
                  <button 
                    v-if="hasAdoPat && !clearAdoPat" 
                    type="button" 
                    @click="clearAdoPat = true; adoPat = ''" 
                    class="text-rose-600 hover:text-rose-800 p-1 text-[11px] font-semibold"
                    title="Remove stored PAT"
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
                PAT marked for deletion. Click Save to confirm.
              </div>

              <!-- Test ADO Button & Feedback -->
              <div class="flex items-center justify-between pt-1">
                <button 
                  type="button"
                  @click="testAdoConnection" 
                  :disabled="isTestingAdo || !adoOrganization" 
                  class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-300 hover:border-slate-400 text-slate-700 bg-white hover:bg-slate-50 transition-colors flex items-center gap-2 shadow-sm disabled:opacity-50"
                >
                  <svg v-if="isTestingAdo" class="animate-spin h-3.5 w-3.5 text-slate-600" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  <span>{{ isTestingAdo ? 'Testing Connection...' : 'Test Connection' }}</span>
                </button>

                <div v-if="adoTestResult" class="text-xs flex items-center gap-1.5 font-medium" :class="adoTestResult.success ? 'text-emerald-700' : 'text-rose-700'">
                  <svg v-if="adoTestResult.success" class="w-4 h-4 text-emerald-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                  </svg>
                  <svg v-else class="w-4 h-4 text-rose-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                  <span>{{ adoTestResult.message }}</span>
                </div>
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
                <p>Secrets (Azure PAT and GitHub token) are never written to disk in plain text. When switching providers, stored secrets are automatically migrated to the new store.</p>
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
                When running KeyVaultComparer in a minimal Alpine or Debian Linux container, set <code class="font-mono bg-slate-200 px-1 py-0.5 rounded text-slate-800">KEYVAULTCOMPARER_MASTER_KEY</code> (or mount a Docker secret) to supply the master key, and mount a volume at <code class="font-mono bg-slate-200 px-1 py-0.5 rounded text-slate-800">/root/.keyvaultcomparer</code>. The store uses AES-256-GCM authenticated encryption.
              </p>
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
