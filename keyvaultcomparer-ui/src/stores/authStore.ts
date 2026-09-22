import { defineStore } from 'pinia'
import { apiFetch } from '../services/apiClient'

export interface UserProfile {
  email: string;
  subscriptionName: string;
  initials: string;
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    profile: null as UserProfile | null,
    subscriptions: [] as any[],
    showAuthError: false,
    globalError: null as string | null,
    isConnecting: false,
    connectionPromise: null as Promise<boolean> | null,
  }),
  getters: {
    isConnected: (state) => !!(state.profile && state.profile.email !== 'Unknown User'),
  },
  actions: {
    setProfile(profile: UserProfile | null) {
      this.profile = profile;
    },
    setSubscriptions(subs: any[]) {
      this.subscriptions = subs;
    },
    setShowAuthError(val: boolean) {
      this.showAuthError = val;
    },
    setGlobalError(val: string | null) {
      this.globalError = val;
    },
    setIsConnecting(val: boolean) {
      this.isConnecting = val;
    },
    setConnectionPromise(promise: Promise<boolean> | null) {
      this.connectionPromise = promise;
    },
    async fetchProfile() {
      try {
        const response = await apiFetch('/api/profile');
        if (response.ok) {
          this.setProfile(await response.json());
        }
      } catch (error) {
        console.error('Failed to fetch profile', error);
      }
    },
    async fetchSubscriptions() {
      try {
        const response = await apiFetch('/api/subscriptions');
        if (response.ok) {
          this.setSubscriptions(await response.json());
        }
      } catch (error) {
        console.error('Failed to fetch subscriptions', error);
      }
    },
    connectToAzure(): Promise<boolean> {
      if (this.isConnected) return Promise.resolve(true);
      if (this.connectionPromise) return this.connectionPromise;

      this.setIsConnecting(true);
      this.setGlobalError(null);

      const promise = (async () => {
        await Promise.all([this.fetchProfile(), this.fetchSubscriptions()]);
        if (!this.isConnected && !this.showAuthError) {
          this.setGlobalError("Failed to connect to Azure. Please verify your authentication via az login.");
        }
        this.setIsConnecting(false);
        this.setConnectionPromise(null);
        return this.isConnected;
      })();

      this.setConnectionPromise(promise);
      return promise;
    },
    disconnectFromAzure() {
      this.setProfile(null);
      this.setSubscriptions([]);
    },
    async retryAuth() {
      this.setShowAuthError(false);
      await this.connectToAzure();
    }
  }
})
