import { useCallback, useSyncExternalStore, type ReactNode } from 'react';
import { UR } from './i18n-ur';

/**
 * English and Urdu. English text is the key: `t('Save')` gives the Urdu when Urdu is on and a translation
 * exists, otherwise the English, so untranslated screens keep working. Shared components translate their own
 * string props (titles, labels, buttons), which is how most of the app is covered without touching each page.
 */
export type Lang = 'en' | 'ur';

const KEY = 'hrms.lang';
const listeners = new Set<() => void>();

function read(): Lang {
  try {
    return localStorage.getItem(KEY) === 'ur' ? 'ur' : 'en';
  } catch {
    return 'en';
  }
}

let current: Lang = read();

/** Sets <html lang dir> and loads the Urdu font the first time it is needed. */
export function applyLang(lang: Lang = current) {
  const html = document.documentElement;
  html.lang = lang;
  html.dir = lang === 'ur' ? 'rtl' : 'ltr';
  if (lang === 'ur' && !document.getElementById('font-ur')) {
    const link = document.createElement('link');
    link.id = 'font-ur';
    link.rel = 'stylesheet';
    link.href = 'https://fonts.googleapis.com/css2?family=Noto+Sans+Arabic:wght@400;500;600;700&display=swap';
    document.head.appendChild(link);
  }
}

export function setLang(lang: Lang) {
  current = lang;
  try {
    localStorage.setItem(KEY, lang);
  } catch {
    // ignore
  }
  applyLang(lang);
  listeners.forEach((l) => l());
}

function subscribe(fn: () => void) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export function translate(text: string, lang: Lang = current): string {
  if (lang !== 'ur') return text;
  return UR[text] ?? UR[text.trim()] ?? text;
}

/** The current language and a translator that re-renders when the language changes. */
export function useT() {
  const lang = useSyncExternalStore(subscribe, () => current);
  const t = useCallback((text: string) => translate(text, lang), [lang]);
  return { lang, t, setLang };
}

/**
 * Translates a node when it is plain text. Safe to call outside hooks: the app re-mounts under a new key when
 * the language changes (see main.tsx), so every render already sees the current language.
 */
export function tx<T extends ReactNode>(node: T): T | string {
  return typeof node === 'string' ? translate(node) : node;
}

/** Translates a sentence with {placeholders}, then fills them: txf('{done} of {total} done', { done: 3, total: 8 }). */
export function txf(template: string, vars: Record<string, string | number>): string {
  return translate(template).replace(/\{(\w+)\}/g, (m, k: string) => (k in vars ? String(vars[k]) : m));
}
