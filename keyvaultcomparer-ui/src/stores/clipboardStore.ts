import { defineStore } from 'pinia';

interface CopiedCell {
  uri: string;
  secretName: string;
}

export const useClipboardStore = defineStore('clipboard', {
  state: () => ({
    internalClipboard: null as string | null,
    copiedCell: null as CopiedCell | null
  }),
  actions: {
    copy(uri: string, secretName: string, value: string) {
      this.internalClipboard = value;
      this.copiedCell = { uri, secretName };
    },
    clear() {
      this.internalClipboard = null;
      this.copiedCell = null;
    }
  }
});
