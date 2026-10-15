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
    cloudAuthMethod: (state): 'InteractiveSSO' | 'ServicePrincipal' | null => state.azureCloud.authMethod,

    // Backward-compatibility Getters
    isConnected: (state): boolean => state.azureCloud.isConnected,
    isConnecting: (state): boolean => state.azureCloud.isConnecting,
    showAuthError: (state): boolean => 
      !state.azureCloud.isConnected && state.azureCloud.hasAttempted && !state.azureCloud.isDismissed,

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
          const fetchWithTimeout = async (url: string, timeoutMs = 15000) => {
            const controller = new AbortController();
            const timer = setTimeout(() => controller.abort(), timeoutMs);
            try {
              return await fetch(url, { signal: controller.signal });
            } finally {
              clearTimeout(timer);
            }
          };

          const [profileRes, subsRes, settingsRes] = await Promise.all([
            fetchWithTimeout('/api/profile').catch(() => null),
            fetchWithTimeout('/api/subscriptions').catch(() => null),
            fetchWithTimeout('/api/settings').catch(() => null)
          ]);

          if (settingsRes && settingsRes.ok) {
            const settingsData = await settingsRes.json();
            this.azureCloud.authMethod = settingsData.azureAuthMode || 'InteractiveSSO';
            this.azureDevOps.hasPat = !!settingsData.hasAdoPat;
          }

          let profile = null;
          let subs: any[] = [];

          if (profileRes && profileRes.ok) {
            try {
              profile = await profileRes.json();
              this.setProfile(profile);
            } catch {}
          }

          if (subsRes && subsRes.ok) {
            try {
              subs = await subsRes.json();
              this.setSubscriptions(subs);
            } catch {}
          }

          if (profile && profile.email && profile.email !== 'Unknown User') {
            this.azureCloud.isConnected = true;
            this.azureCloud.hasAttempted = true;
            this.azureCloud.error = null;

            // Probe ADO automatically with the connected Azure identity
            this.checkAutoAdo();

            return true;
          } else {
            this.azureCloud.profile = null;
            this.azureCloud.subscriptions = [];
            this.azureCloud.isConnected = false;
            this.azureCloud.hasAttempted = true;
            this.azureCloud.error = 'Azure authentication required. Please sign in with Microsoft or configure credentials.';
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

    async loginInteractive(): Promise<{ success: boolean; message: string }> {
      this.azureCloud.isConnecting = true;
      this.azureCloud.error = null;
      try {
        const res = await fetch('/api/auth/login-interactive', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: '{}'
        });
        const data = await res.json();
        if (data.success) {
          await this.retryAuth();
          return { success: true, message: data.message || 'Authenticated successfully' };
        } else {
          this.azureCloud.error = data.message || 'Authentication failed';
          return { success: false, message: this.azureCloud.error || 'Authentication failed' };
        }
      } catch (err: any) {
        this.azureCloud.error = err?.message || 'Login request failed';
        return { success: false, message: this.azureCloud.error || 'Login request failed' };
      } finally {
        this.azureCloud.isConnecting = false;
      }
    },

    async logout(): Promise<void> {
      try {
        await fetch('/api/auth/logout', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: '{}'
        });
      } catch {}
      this.disconnectFromAzure();
      this.azureDevOps.isConnected = null;
      this.azureDevOps.authMethod = null;
      this.azureDevOps.connectionMessage = null;
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
