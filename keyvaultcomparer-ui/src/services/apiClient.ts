import { useAuthStore } from '../stores/authStore';

export const apiFetch = async (input: RequestInfo | URL, init?: RequestInit) => {
  const authStore = useAuthStore();
  try {
    const response = await fetch(input, init);
    if (response.status === 401) {
      authStore.setAzureCloudConnected(false);
      throw new Error('Azure Authentication Required');
    }
    if (!response.ok) {
      const cloned = response.clone();
      const errorText = await cloned.text().catch(() => '');
      const inputStr = typeof input === 'string' ? input : '';
      if (!inputStr.includes('/api/profile') && !inputStr.includes('/api/subscriptions') && !inputStr.includes('/api/settings')) {
        authStore.setGlobalError(`Backend Error: ${response.status} ${response.statusText} - ${errorText.substring(0, 100)}`);
      }
    }
    return response;
  } catch (e: any) {
    const inputStr = typeof input === 'string' ? input : '';
    if (e.message !== 'Azure Authentication Required' && !inputStr.includes('/api/profile') && !inputStr.includes('/api/subscriptions') && !inputStr.includes('/api/settings')) {
      authStore.setGlobalError(`Network Error: ${e.message || 'Failed to connect to backend'}`);
    }
    throw e;
  }
};
