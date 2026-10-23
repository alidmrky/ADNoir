import { useSyncExternalStore } from 'react';
import tr from './tr.json';
import en from './en.json';
export type Language = 'tr' | 'en';
let language: Language = 'tr';
const listeners = new Set<() => void>();
export const getLanguage = () => language;
export function setLanguage(value: Language) {
  language = value;
  document.documentElement.lang = value;
  listeners.forEach((listener) => listener());
}
export function subscribeLanguage(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
export function t(key: string, args: Record<string, string | number> = {}): string {
  const catalog: Record<string, string> = language === 'tr' ? tr : en;
  return (catalog[key] ?? key).replace(/\{(\w+)\}/g, (_, name: string) =>
    String(args[name] ?? `{${name}}`),
  );
}
export function useTranslation() {
  useSyncExternalStore(subscribeLanguage, getLanguage);
  return { t, language, setLanguage };
}
