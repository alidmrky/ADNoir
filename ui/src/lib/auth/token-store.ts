let accessToken: string | undefined;
const refreshKey = 'adnoir.refresh-token';
export const tokenStore = {
  access: () => accessToken,
  refresh: () => localStorage.getItem(refreshKey) || undefined,
  write(access: string, refresh: string) {
    localStorage.setItem(refreshKey, refresh);
    accessToken = access;
  },
  clear() {
    accessToken = undefined;
    localStorage.removeItem(refreshKey);
  },
  onExternalChange(callback: () => void) {
    const listener = (event: StorageEvent) => {
      if (event.key === refreshKey || event.key === null) {
        accessToken = undefined;
        callback();
      }
    };
    window.addEventListener('storage', listener);
    return () => window.removeEventListener('storage', listener);
  },
};
