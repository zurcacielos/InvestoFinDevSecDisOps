import { apiFetch } from '../services/apiClient';

export const openUrlInNewTab = async (url: string): Promise<void> => {
  if (!url) return;

  // Fire and forget logging
  apiFetch('/api/logs/url', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ url }),
  }).catch(err => {
    console.error('Failed to log url:', err);
  });

  // Open the url
  window.open(url, '_blank');
};
