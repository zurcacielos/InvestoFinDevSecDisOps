import { defineStore } from 'pinia';
import { useDataStore } from './dataStore';
import { useFilterStore } from './filterStore';

export interface SecuritySettings {
  minLength: number;
  ignoreValues: string[];
  includeKeyKeywords: string[];
}

export const defaultSecuritySettings: SecuritySettings = {
  minLength: 15,
  ignoreValues: ['true', 'false', '0', '1', 'null', 'undefined', ''],
  includeKeyKeywords: ['salt', 'key', 'token', 'password', 'secret', 'pwd']
};

export interface UiSettings {
  resultLimit: number;
  identiconsByRow: boolean;
  identiconsByCol: boolean;
  colorMatchByRow: boolean;
  statusFilter: 'Any' | '=' | '≠' | 'Missing';
  showReusedValues: boolean;
  showStagedOnly: boolean;
  securityByRow: boolean;
  securityByCol: boolean;
}

export const defaultUiSettings: UiSettings = {
  resultLimit: 50,
  identiconsByRow: true,
  identiconsByCol: true,
  colorMatchByRow: true,
  statusFilter: 'Any',
  showReusedValues: false,
  showStagedOnly: false,
  securityByRow: false,
  securityByCol: false
};

export const identiconEmojis = [
  // Original 72 from the first list
  '🍎', '💎', '🐸', '🌻', '🔮', '🥥', '⚽', '🌈', '🚗', '🐬', 
  '🥑', '🔥', '🍆', '🐵', '🎲', '🦄', '🎈', '🦋', '🌵', '🐤', 
  '👾', '🍔', '🦓', '🎨', '🍅', '🧬', '🥝', '🦁', '🪐', '🦇', 
  '🦜', '🐞', '🧩', '🌞', '🐪', '🐧', '🐔', '🦀', '🔋', '🐝', 
  '🦉', '🐺', '🐦', '🦩', '🦖', '🐯', '🐌', '🐘', '🍄', '🐛', 
  '🦐', '🍁', '🦢', '🦑', '🦚', '🎃', '🥁', '☃️', '🎭', '🐢', 
  '🎥', '🩰', '📞', '🎸', '📺', '🎬', '🎤', '🎧', '🎼', '🎹', 
  '🚀', '🍕',
  // 78 additional highly distinguishable icons
  '⚓', '🔔', '🔑', '💣', '🧲', '🏆', '👑', '🎩', '🎒', '👠', 
  '☂️', '💡', '💰', '🛒', '✂️', '🗑️', '🔒', '🏷️', '🎁', '🧸', 
  '🪀', '🪄', '🩺', '💊', '🩸', '🧪', '🔭', '🧯', '🍩', '🥐', 
  '🍒', '🌰', '🍯', '🧊', '🍷', '🍺', '🍼', '🧁', '🍭', '🍉', 
  '🍓', '🌶️', '🌽', '🥕', '🥨', '🥚', '🥞', '🍖', '🌭', '🌮', 
  '🥗', '🍿', '🚁', '🚜', '⛵', '🚲', '🛸', '🚂', '✈️', '🛵', 
  '🐾', '🐲', '🐄', '🐐', '🌲', '🌴', '🍀', '🌹', '🌍', '🌙', 
  '⚡', '🛑', '⛔', '❌', '⚠️', '♻️', '♠️', '♥️'
];

export const hashString = (str: string) => {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = (hash << 5) - hash + str.charCodeAt(i);
    hash |= 0;
  }
  return Math.abs(hash);
};

export const loadSharableConfig = () => {
  try {
    const params = new URLSearchParams(window.location.search);
    const s = params.get('s');
    if (s) {
      return JSON.parse(decodeURIComponent(atob(s)));
    }
  } catch (e) {
    console.warn('Failed to parse URL config', e);
  }
  return null;
};

export const useSettingsStore = defineStore('settings', {
  state: () => {
    const urlConfig = loadSharableConfig();
    
    // Load Security Settings
    let securitySettings = { ...defaultSecuritySettings };
    try {
      const stored = localStorage.getItem('securitySettings');
      if (stored) {
        securitySettings = { ...defaultSecuritySettings, ...JSON.parse(stored) };
      }
    } catch (e) { }

    // Load UI Settings
    let uiSettings = { ...defaultUiSettings };
    if (urlConfig && urlConfig.u) {
      uiSettings = { ...defaultUiSettings, ...urlConfig.u };
    } else {
      try {
        const stored = localStorage.getItem('uiSettings');
        if (stored) {
          uiSettings = { ...defaultUiSettings, ...JSON.parse(stored) };
        }
      } catch (e) { }
    }

    return {
      securitySettings,
      uiSettings,
      urlConfig,
    };
  },
  actions: {
    saveSecuritySettings() {
      localStorage.setItem('securitySettings', JSON.stringify(this.securitySettings));
    },
    saveUiSettings() {
      localStorage.setItem('uiSettings', JSON.stringify(this.uiSettings));
      const dataStore = useDataStore();
      const filterStore = useFilterStore();
      this.syncUrl(dataStore.vaultUris, filterStore.nameFilter);
    },
    syncUrl(vaultUris: string[], nameFilter: string) {
      try {
        const payload = { u: this.uiSettings, v: vaultUris, f: nameFilter };
        const encoded = btoa(encodeURIComponent(JSON.stringify(payload)));
        const newUrl = new URL(window.location.href);
        newUrl.searchParams.set('s', encoded);
        window.history.replaceState({}, '', newUrl);
      } catch (e) { console.warn('Failed to sync URL', e); }
    }
  }
});
