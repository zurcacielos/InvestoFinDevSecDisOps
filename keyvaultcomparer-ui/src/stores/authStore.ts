import { defineStore } from 'pinia';

export interface UserProfile {
  email: string;
  subscriptionName: string;
  initials: string;
}

export interface AzureSubscription {
  id: string;
  name: string;
}

export interface AzureCloudState {
  isConnected: boolean;
  isConnecting: boolean;
  hasAttempted: boolean;
  isDismissed: boolean;
  error: string | null;
  profile: UserProfile | null;
  subscriptions: AzureSubscription[];
}

export interface AzureDevOpsState {
  isConnected: boolean | null;
  isConnecting: boolean;
  hasPat: boolean;
  error: string | null;
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    azureCloud: {
      isConnected: false,
      isConnecting: false,
      hasAttempted: false,
      isDismissed: false,
      error: null as string | null,
      profile: null as UserProfile | null,
      subscriptions: [] as AzureSubscription[],
    } as AzureCloudState,
    azureDevOps: {
      isConnected: null as boolean | null,
      isConnecting: false,
      hasPat: false,
      error: null as string | null,
    } as AzureDevOpsState,
    globalError: null as string | null,
    connectionPromise: null as Promise<boolean> | null,
  }),

  getters: {
    // Azure Cloud Getters
    isCloudConnected: (state): boolean => state.azureCloud.isConnected,
    isCloudConnecting: (state): boolean => state.azureCloud.isConnecting,
    showCloudAuthBanner: (state): boolean => 
      !state.azureCloud.isConnected && state.azureCloud.hasAttempted && !state.azureCloud.isDismissed,
    profile: (state): UserProfile | null => state.azureCloud.profile,
    subscriptions: (state): AzureSubscription[] => state.azureCloud.subscriptions,

    // Backward-compatibility Getters
    isConnected: (state): boolean => state.azureCloud.isConnected,
    isConnecting: (state): boolean => state.azureCloud.isConnecting,
    showAuthError: (state): boolean => 
      !state.azureCloud.isConnected && state.azureCloud.hasAttempted && !state.azureCloud.isDismissed,

    // Azure DevOps Getters
    isAdoConnected: (state): boolean => !!state.azureDevOps.isConnected,
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
      this.azureCloud.isDismissed = !val;
    },

    dismissCloudBanner() {
      this.azureCloud.isDismissed = true;
    },

    setGlobalError(val: string | null) {
      this.globalError = val;
    },

    handleAzureUnauthorized() {
      this.azureCloud.isConnected = false;
      this.azureCloud.hasAttempted = true;
      this.azureCloud.isDismissed = false;
    },

    setAzureCloudConnected(val: boolean) {
      this.azureCloud.isConnected = val;
      if (val) {
        this.azureCloud.error = null;
        this.azureCloud.isDismissed = false;
      }
    },

    async fetchProfile() {
      try {
        const response = await fetch('/api/profile');
        if (response.ok) {
          const data = await response.json();
          this.setProfile(data);
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

    async connectToAzure(): Promise<boolean> {
      if (this.azureCloud.isConnected) return true;
      if (this.connectionPromise) return this.connectionPromise;

      this.azureCloud.isConnecting = true;

      const promise = (async () => {
        try {
          const [profileRes, subsRes] = await Promise.all([
            fetch('/api/profile'),
            fetch('/api/subscriptions')
          ]);

          if (profileRes.ok && subsRes.ok) {
            const profile = await profileRes.json();
            const subs = await subsRes.json();
            this.setProfile(profile);
            this.setSubscriptions(subs);
            this.azureCloud.isConnected = !!(profile && profile.email && profile.email !== 'Unknown User');
            this.azureCloud.hasAttempted = true;
            this.azureCloud.error = null;
            return this.azureCloud.isConnected;
          } else {
            this.azureCloud.profile = null;
            this.azureCloud.subscriptions = [];
            this.azureCloud.isConnected = false;
            this.azureCloud.hasAttempted = true;
            this.azureCloud.error = 'Azure authentication required. Please run az login or configure Azure credentials in Settings.';
            return false;
          }
        } catch (e: any) {
          this.azureCloud.profile = null;
          this.azureCloud.subscriptions = [];
          this.azureCloud.isConnected = false;
          this.azureCloud.hasAttempted = true;
          this.azureCloud.error = e?.message || 'Failed to connect to Azure Cloud';
          return false;
        } finally {
          this.azureCloud.isConnecting = false;
          this.connectionPromise = null;
        }
      })();

      this.connectionPromise = promise;
      return promise;
    },

    async retryAuth() {
      this.azureCloud.isDismissed = false;
      this.azureCloud.isConnected = false;
      this.connectionPromise = null;
      return this.connectToAzure();
    },

    disconnectFromAzure() {
      this.azureCloud.isConnected = false;
      this.azureCloud.profile = null;
      this.azureCloud.subscriptions = [];
      this.azureCloud.hasAttempted = false;
      this.azureCloud.isDismissed = false;
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
