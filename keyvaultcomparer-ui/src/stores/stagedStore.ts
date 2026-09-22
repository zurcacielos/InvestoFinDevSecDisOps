import { defineStore } from 'pinia';
import { apiFetch } from '../services/apiClient';

export interface StagedChange {
  vaultUri: string;
  secretName: string;
  originalValue: string | null;
  newValue: string;
  type: 'CREATE' | 'UPDATE' | 'DELETE';
}

export const useStagedStore = defineStore('staged', {
  state: () => ({
    stagedChanges: [] as StagedChange[],
    isApplying: false
  }),
  actions: {
    clearAll() {
      this.stagedChanges.splice(0, this.stagedChanges.length);
    },
    addChange(uri: string, secretName: string, originalValue: string | null, newValue: string, currentStatus: string) {
      const type = (originalValue === null || currentStatus === 'Missing' || currentStatus === 'Not Retrieved') ? 'CREATE' : 'UPDATE';
      const existingIndex = this.stagedChanges.findIndex(s => s.vaultUri === uri && s.secretName === secretName);
      
      if (newValue === originalValue) {
        if (existingIndex >= 0) this.stagedChanges.splice(existingIndex, 1);
        return;
      }

      if (existingIndex >= 0) {
        this.stagedChanges[existingIndex].newValue = newValue;
      } else {
        this.stagedChanges.push({ vaultUri: uri, secretName, originalValue, newValue, type });
      }
    },
    revertChange(uri: string, secretName: string) {
      const index = this.stagedChanges.findIndex(s => s.vaultUri === uri && s.secretName === secretName);
      if (index >= 0) this.stagedChanges.splice(index, 1);
    },
    async applyStagedChanges(fetchComparison: () => Promise<void>) {
      if (this.stagedChanges.length === 0) return;
      const toApply = this.stagedChanges.slice(0, 5);
      
      if (!confirm(`Are you sure you want to apply ${toApply.length} change(s) directly to Azure Key Vault? This action cannot be easily undone.`)) {
        return;
      }

      this.isApplying = true;
      try {
        const payload = toApply.map(c => ({
          vaultUri: c.vaultUri,
          secretName: c.secretName,
          newValue: c.newValue
        }));
        const response = await apiFetch('/api/vault/apply', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload)
        });
        
        if (response.ok) {
          alert('Changes applied successfully! Refreshing dashboard...');
          this.stagedChanges.splice(0, toApply.length);
          await fetchComparison();
        } else {
          const err = await response.json();
          alert('Some errors occurred while applying changes:\n' + (err.errors ? err.errors.join('\n') : JSON.stringify(err)));
        }
      } catch (error) {
        console.error('Failed to apply changes', error);
        alert('A network error occurred while applying changes.');
      } finally {
        this.isApplying = false;
      }
    }
  }
});
