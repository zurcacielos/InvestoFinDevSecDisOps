import { apiFetch } from '../services/apiClient';

export const openUrlInNewTab = (url: string, existingWindow?: Window | null): Window | null => {
  if (!url && !existingWindow) return null;

  if (url) {
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
  }

  if (existingWindow) {
    if (url) existingWindow.location.href = url;
    return existingWindow;
  }

  return window.open(url || 'about:blank', '_blank');
};
