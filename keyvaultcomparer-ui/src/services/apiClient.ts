import { useAuthStore } from '../stores/authStore';

export const apiFetch = async (input: RequestInfo | URL, init?: RequestInit) => {
  const authStore = useAuthStore();
  try {
    const response = await fetch(input, init);
    if (response.status === 401) {
      authStore.setShowAuthError(true);
      throw new Error('Azure Login Required');
    }
    if (!response.ok) {
      const cloned = response.clone();
      const errorText = await cloned.text().catch(() => '');
      authStore.setGlobalError(`Backend Error: ${response.status} ${response.statusText} - ${errorText.substring(0, 100)}`);
      
      // Auto-recovery for profile and subscriptions if a data call succeeds but auth state is broken
      if (!authStore.profile || authStore.profile.email === 'Unknown User') {
        if (input !== '/api/profile' && input !== '/api/subscriptions') {
          setTimeout(() => {
            authStore.connectToAzure();
          }, 100);
        }
      }
    }
    return response;
  } catch (e: any) {
    if (e.message !== 'Azure Login Required') {
      authStore.setGlobalError(`Network Error: ${e.message || 'Failed to connect to backend'}`);
    }
    throw e;
  }
};
