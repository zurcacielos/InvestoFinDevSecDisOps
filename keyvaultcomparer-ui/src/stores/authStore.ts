import { defineStore } from 'pinia';
import { useAzureConnectionStore, type UserProfile } from './azureConnectionStore';

export type { UserProfile } from './azureConnectionStore';

export interface AzureSubscription {
  id: string;
  name: string;
}

export interface AzureCloudState {
  isConnected: boolean;
  isConnecting: boolean;
  hasAttempted: boolean;
  isDismissed: boolean;
  authMethod: 'InteractiveSSO' | 'ServicePrincipal' | null;
  error: string | null;
  profile: UserProfile | null;
  subscriptions: AzureSubscription[];
}

export interface AzureDevOpsState {
  isConnected: boolean | null;
  isConnecting: boolean;
  hasPat: boolean;
  authMethod: 'AzureSSO' | 'PAT' | null;
  connectionMessage: string | null;
  error: string | null;
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    azureCloud: {
      isConnected: false,
      isConnecting: false,
      hasAttempted: false,
      isDismissed: false,
      authMethod: null as 'InteractiveSSO' | 'ServicePrincipal' | null,
      error: null as string | null,
      profile: null as UserProfile | null,
      subscriptions: [] as AzureSubscription[],
    } as AzureCloudState,
    azureDevOps: {
      isConnected: null as boolean | null,
      isConnecting: false,
      hasPat: false,
      authMethod: null as 'AzureSSO' | 'PAT' | null,
      connectionMessage: null as string | null,
      error: null as string | null,
    } as AzureDevOpsState,
    globalError: null as string | null,
  }),

  getters: {
    // Azure Cloud Getters driven by state machine
    isCloudConnected(): boolean {
      const conn = useAzureConnectionStore();
      return conn.isConnected || this.azureCloud.isConnected;
    },
    isCloudConnecting(): boolean {
      const conn = useAzureConnectionStore();
      return conn.isChecking || conn.isLoggingIn || this.azureCloud.isConnecting;
    },
    showCloudAuthBanner(): boolean {
      return useAzureConnectionStore().showLoginPrompt;
    },
    profile(state): UserProfile | null {
      const conn = useAzureConnectionStore();
      return conn.profile || state.azureCloud.profile;
    },
    subscriptions: (state): AzureSubscription[] => state.azureCloud.subscriptions,
    cloudAuthMethod(state): 'InteractiveSSO' | 'ServicePrincipal' | null {
      const conn = useAzureConnectionStore();
      return conn.authMethod || state.azureCloud.authMethod;
    },

    // Backward-compatibility Getters
    isConnected(): boolean {
      const conn = useAzureConnectionStore();
      return conn.isConnected || this.azureCloud.isConnected;
    },
    isConnecting(): boolean {
      const conn = useAzureConnectionStore();
      return conn.isChecking || conn.isLoggingIn || this.azureCloud.isConnecting;
    },
    showAuthError(): boolean {
      return useAzureConnectionStore().showLoginPrompt;
    },

    // Azure DevOps Getters
    isAdoConnected: (state): boolean => !!state.azureDevOps.isConnected,
    adoAuthMethod: (state): 'AzureSSO' | 'PAT' | null => state.azureDevOps.authMethod,
  },

  actions: {
    setProfile(profile: UserProfile | null) {
      this.azureCloud.profile = profile;
      if (profile && profile.email && profile.email !== 'Unknown User') {
        this.azureCloud.isConnected = true;
      }
    },

    setSubscriptions(subs: AzureSubscription[]) {
      this.azureCloud.subscriptions = subs;
    },

    setShowAuthError(val: boolean) {
      const conn = useAzureConnectionStore();
      if (!val) conn.dismissBanner();
      else conn.showBanner();
      this.azureCloud.isDismissed = !val;
    },

    dismissCloudBanner() {
      useAzureConnectionStore().dismissBanner();
      this.azureCloud.isDismissed = true;
    },

    setGlobalError(val: string | null) {
      this.globalError = val;
    },

    handleAzureUnauthorized() {
      useAzureConnectionStore().status = 'login_prompt';
      this.azureCloud.isConnected = false;
      this.azureCloud.hasAttempted = true;
      this.azureCloud.isDismissed = false;
    },

    setAzureCloudConnected(val: boolean) {
      this.azureCloud.isConnected = val;
      if (val) {
        useAzureConnectionStore().status = 'connected';
        this.azureCloud.error = null;
        this.azureCloud.isDismissed = false;
      } else {
        useAzureConnectionStore().status = 'login_prompt';
      }
    },

    async fetchProfile() {
      const conn = useAzureConnectionStore();
      if (!conn.canMakeAzureRequests) {
        return null;
      }
      try {
        const response = await fetch('/api/profile');
        if (response.ok) {
          const data = await response.json();
          this.setProfile(data);
          conn.profile = data;
          return data;
        } else if (response.status === 401) {
          this.handleAzureUnauthorized();
          this.azureCloud.profile = null;
        }
      } catch (error) {
        console.error('Failed to fetch profile', error);
      }
      return null;
    },

    async fetchSubscriptions() {
      const conn = useAzureConnectionStore();
      if (!conn.canMakeAzureRequests) {
        this.azureCloud.subscriptions = [];
        return [];
      }
      try {
        const response = await fetch('/api/subscriptions');
        if (response.ok) {
          const data = await response.json();
          this.setSubscriptions(data);
          return data;
        } else if (response.status === 401) {
          this.handleAzureUnauthorized();
          this.azureCloud.subscriptions = [];
        }
      } catch (error) {
        console.error('Failed to fetch subscriptions', error);
      }
      return [];
    },

    /**
     * Connect to Azure using the single-probe state machine.
     * Guaranteed fast response; no heavy requests if not authenticated.
     */
    async connectToAzure(): Promise<boolean> {
      const conn = useAzureConnectionStore();
      const isConnected = await conn.checkConnection();

      this.azureCloud.isConnected = isConnected;
      this.azureCloud.hasAttempted = true;
      this.azureCloud.authMethod = conn.authMethod;

      if (isConnected) {
        this.azureCloud.profile = conn.profile;
        this.azureCloud.error = null;

        // Fetch subscriptions and probe ADO only when connected
        this.fetchSubscriptions();
        this.checkAutoAdo();
        return true;
      } else {
        this.azureCloud.profile = null;
        this.azureCloud.subscriptions = [];
        this.azureCloud.error = conn.error || 'Azure authentication required.';
        return false;
      }
    },

    async loginInteractive(): Promise<{ success: boolean; message: string }> {
      const conn = useAzureConnectionStore();
      const res = await conn.loginInteractive();

      if (res.success) {
        this.azureCloud.isConnected = true;
        this.azureCloud.profile = conn.profile;
        this.azureCloud.authMethod = conn.authMethod;
        this.fetchSubscriptions();
        this.checkAutoAdo();
      } else {
        this.azureCloud.isConnected = false;
        this.azureCloud.error = res.message;
      }

      return res;
    },

    async logout(): Promise<void> {
      const conn = useAzureConnectionStore();
      await conn.logout();
      this.azureCloud.isConnected = false;
      this.azureCloud.profile = null;
      this.azureCloud.subscriptions = [];
    },

    async retryAuth(): Promise<boolean> {
      const conn = useAzureConnectionStore();
      return conn.checkConnection(true);
    },

    disconnectFromAzure() {
      const conn = useAzureConnectionStore();
      conn.status = 'login_prompt';
      this.azureCloud.isConnected = false;
      this.azureCloud.profile = null;
      this.azureCloud.subscriptions = [];
      this.azureCloud.hasAttempted = false;
      this.azureCloud.isDismissed = false;
    },

    async checkAutoAdo() {
      const org = localStorage.getItem('adoOrg') || '';
      const project = localStorage.getItem('adoProject') || '';
      if (org) {
        await this.checkAdoConnection(org, project);
      }
    },

    async checkAdoConnection(organization: string, project: string, pat?: string) {
      if (!organization) return false;
      this.azureDevOps.isConnecting = true;
      this.azureDevOps.error = null;

      try {
        const res = await fetch('/api/settings/test-ado', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ organization, project, pat: pat || null })
        });
        const data = await res.json();
        this.azureDevOps.isConnected = !!data.success;
        this.azureDevOps.authMethod = data.authMethod || (pat ? 'PAT' : 'AzureSSO');
        this.azureDevOps.connectionMessage = data.message || (data.success ? 'Connected' : 'Connection failed');
        if (!data.success) {
          this.azureDevOps.error = data.message || 'Failed to connect to Azure DevOps';
        }
        return this.azureDevOps.isConnected;
      } catch (err: any) {
        this.azureDevOps.isConnected = false;
        this.azureDevOps.error = err.message || 'Error checking ADO connection';
        return false;
      } finally {
        this.azureDevOps.isConnecting = false;
      }
    }
  }
});
