import { defineStore } from 'pinia';
import { useEncryptedCacheStore } from './encryptedCacheStore';

export type AzureConnectionStatus = 
  | 'disconnected' 
  | 'checking' 
  | 'login_prompt' 
  | 'logging_in' 
  | 'connected';

export interface UserProfile {
  email: string;
  subscriptionName: string;
  initials: string;
  photoUrl?: string | null;
  tenantId?: string;
}

export const useAzureConnectionStore = defineStore('azureConnection', {
  state: () => ({
    status: 'checking' as AzureConnectionStatus,
    authMethod: null as 'InteractiveSSO' | 'ServicePrincipal' | null,
    profile: null as UserProfile | null,
    error: null as string | null,
    isDismissed: false,
    timeoutSeconds: 4,
    probePromise: null as Promise<boolean> | null,
  }),

  getters: {
    isConnected: (state): boolean => state.status === 'connected',
    isChecking: (state): boolean => state.status === 'checking',
    isLoggingIn: (state): boolean => state.status === 'logging_in',
    showLoginPrompt: (state): boolean => 
      (state.status === 'login_prompt' || state.status === 'disconnected') && !state.isDismissed,
    canMakeAzureRequests: (state): boolean => state.status === 'connected',
    canAccessEncryptedCache: (state): boolean => state.status === 'connected',
  },

  actions: {
    dismissBanner() {
      this.isDismissed = true;
    },

    showBanner() {
      this.isDismissed = false;
    },

    setTimeoutSeconds(sec: number) {
      if (sec >= 1 && sec <= 60) {
        this.timeoutSeconds = sec;
      }
    },

    /**
     * Executes exactly ONE fast probe request to check if Azure is authenticated.
     * Guaranteed fast response; never makes redundant Azure or cache calls if unauthenticated.
     */
    async checkConnection(force = false): Promise<boolean> {
      if (!force && this.status === 'connected' && this.profile) {
        return true;
      }

      if (this.probePromise) {
        return this.probePromise;
      }

      this.status = 'checking';
      this.error = null;

      const promise = (async () => {
        const controller = new AbortController();
        const timeoutMs = (this.timeoutSeconds || 4) * 1000;
        const timer = setTimeout(() => controller.abort(), timeoutMs);

        try {
          const res = await fetch('/api/auth/status', { signal: controller.signal });
          if (res.ok) {
            const data = await res.json();
            if (data.isConnected) {
              this.status = 'connected';
              this.authMethod = data.authMethod || 'InteractiveSSO';
              this.profile = data.profile || null;
              this.error = null;
              this.isDismissed = false;

              // Signal cache store to load stats safely
              try {
                const cacheStore = useEncryptedCacheStore();
                cacheStore.onAzureConnected();
              } catch {}

              return true;
            }
          }

          // Unauthenticated: transition cleanly to login_prompt state
          this.status = 'login_prompt';
          this.profile = null;
          return false;
        } catch (err: any) {
          this.status = 'login_prompt';
          this.profile = null;
          this.error = err.name === 'AbortError' 
            ? `Azure auth check timed out after ${this.timeoutSeconds}s`
            : (err.message || 'Failed to probe Azure authentication');
          return false;
        } finally {
          clearTimeout(timer);
          this.probePromise = null;
        }
      })();

      this.probePromise = promise;
      return promise;
    },

    /**
     * Interactive SSO login triggered by user.
     */
    async loginInteractive(): Promise<{ success: boolean; message: string }> {
      this.status = 'logging_in';
      this.error = null;

      try {
        const res = await fetch('/api/auth/login-interactive', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: '{}'
        });

        const data = await res.json().catch(() => ({}));
        if (data.success) {
          // Probe connection immediately to set connected state and load profile
          await this.checkConnection(true);
          return { success: true, message: data.message || 'Authenticated successfully.' };
        } else {
          this.status = 'login_prompt';
          this.error = data.message || 'Authentication failed.';
          return { success: false, message: this.error || 'Authentication failed.' };
        }
      } catch (err: any) {
        this.status = 'login_prompt';
        this.error = err.message || 'Login request error';
        return { success: false, message: this.error || 'Login request error' };
      }
    },

    /**
     * Sign out and seal encrypted local cache.
     */
    async logout(): Promise<void> {
      try {
        await fetch('/api/auth/logout', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: '{}'
        });
      } catch {}

      this.status = 'login_prompt';
      this.profile = null;
      this.isDismissed = false;

      // Lock and clear cache store
      try {
        const cacheStore = useEncryptedCacheStore();
        cacheStore.onAzureDisconnected();
      } catch {}
    }
  }
});
