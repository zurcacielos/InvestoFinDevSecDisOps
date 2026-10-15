import { defineStore } from 'pinia';
import { useAzureConnectionStore } from './azureConnectionStore';
import { useDevopsDataStore } from './devopsDataStore';

export interface CacheCategoryStat {
  category: string;
  itemCount: number;
}

export interface CacheStatsResponse {
  enabled: boolean;
  isSessionUnlocked: boolean;
  itemCount: number;
  fileSizeBytes: number;
  filePath?: string;
  categories: CacheCategoryStat[];
}

export const useEncryptedCacheStore = defineStore('encryptedCache', {
  state: () => ({
    isEnabled: true,
    isUnlocked: false,
    totalItems: 0,
    fileSizeBytes: 0,
    filePath: '',
    categories: [] as CacheCategoryStat[],
    isLoading: false,
    isClearing: false,
    clearingCategory: null as string | null,
    pipelinesTtlMinutes: 60,
    pipelineRunsTtlMinutes: 30,
    vaultKeysTtlMinutes: 120,
    variableGroupsTtlMinutes: 60,
    lastActionMessage: null as { type: 'success' | 'error'; text: string } | null,
  }),

  actions: {
    formatBytes(bytes: number): string {
      if (!bytes || bytes <= 0) return '0 B';
      const k = 1024;
      const sizes = ['B', 'KB', 'MB', 'GB'];
      const i = Math.floor(Math.log(bytes) / Math.log(k));
      return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    },

    /**
     * Called when Azure connection is confirmed. Safely loads encrypted cache metrics.
     */
    async onAzureConnected() {
      this.isUnlocked = true;
      await this.fetchStats();
    },

    /**
     * Called when Azure is logged out or disconnected. Locks the store and zeroes counters.
     */
    onAzureDisconnected() {
      this.isUnlocked = false;
      this.totalItems = 0;
      this.categories = [];
    },

    /**
     * Fetches current cache metrics from API.
     * Guarded: will not query backend if Azure is not connected.
     */
    async fetchStats(): Promise<void> {
      const connStore = useAzureConnectionStore();
      if (!connStore.canAccessEncryptedCache) {
        this.isUnlocked = false;
        return;
      }

      this.isLoading = true;
      try {
        const res = await fetch('/api/cache/stats');
        if (res.ok) {
          const data: CacheStatsResponse = await res.json();
          this.isEnabled = data.enabled ?? true;
          this.isUnlocked = data.isSessionUnlocked ?? false;
          this.totalItems = data.itemCount ?? 0;
          this.fileSizeBytes = data.fileSizeBytes ?? 0;
          this.filePath = data.filePath || '';
          this.categories = data.categories || [];
        }
      } catch (err: any) {
        console.warn('[encryptedCacheStore] Could not load cache stats:', err);
      } finally {
        this.isLoading = false;
      }
    },

    /**
     * Clears all cache or a specific category.
     */
    async clearCache(category?: string): Promise<{ success: boolean; message: string }> {
      this.isClearing = true;
      this.clearingCategory = category || 'all';
      this.lastActionMessage = null;

      try {
        const url = category 
          ? `/api/cache/clear?category=${encodeURIComponent(category)}` 
          : '/api/cache/clear';
        const res = await fetch(url, { method: 'POST' });
        if (!res.ok) throw new Error('Failed to clear cache');
        const data = await res.json().catch(() => ({}));

        const message = data.message || (category 
          ? `Cache category "${category}" cleared.` 
          : 'All cached data cleared.');

        if (!category || category === 'Pipelines') {
          try {
            useDevopsDataStore().clearLocalPipelinesCache();
          } catch {}
        }

        this.lastActionMessage = { type: 'success', text: message };
        await this.fetchStats();

        setTimeout(() => {
          if (this.lastActionMessage?.type === 'success') {
            this.lastActionMessage = null;
          }
        }, 4000);

        return { success: true, message };
      } catch (err: any) {
        const message = err.message || 'Error clearing cache';
        this.lastActionMessage = { type: 'error', text: message };
        return { success: false, message };
      } finally {
        this.isClearing = false;
        this.clearingCategory = null;
      }
    }
  }
});
