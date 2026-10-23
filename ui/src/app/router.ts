import { useSyncExternalStore } from 'react';
function subscribe(callback: () => void) {
  window.addEventListener('hashchange', callback);
  return () => window.removeEventListener('hashchange', callback);
}
function getPath() {
  return window.location.hash.slice(1) || '/';
}
export function useRoute() {
  return useSyncExternalStore(subscribe, getPath);
}
export function navigate(path: string) {
  window.location.hash = path;
}
