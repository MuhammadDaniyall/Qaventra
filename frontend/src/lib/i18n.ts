import { useCallback, useSyncExternalStore, type ReactNode } from 'react';
import { UR } from './i18n-ur';
import { AR } from './i18n-ar';

/**
 * English, Urdu and Arabic. English text is the key: `t('Save')` gives the Urdu when Urdu is on and a translation
 * exists, otherwise the English, so untranslated screens keep working. Shared components translate their own
 * string props (titles, labels, buttons), which is how most of the app is covered without touching each page.
 */
export type Lang = 'en' | 'ur' | 'ar';

/** The language picker's options, each written in its own language so people can find it. */
export const LANGS: { value: Lang; label: string }[] = [
  { value: 'en', label: 'English' },
  { value: 'ur', label: 'اردو' },
  { value: 'ar', label: 'العربية' },
];

const DICTS: Record<Lang, Record<string, string> | null> = { en: null, ur: UR, ar: AR };
const FONTS: Partial<Record<Lang, string>> = {
  ur: 'https://fonts.googleapis.com/css2?family=Noto+Sans+Arabic:wght@400;500;600;700&display=swap',
  ar: 'https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Arabic:wght@400;500;600;700&display=swap',
};

export const isRtl = (lang: Lang) => lang === 'ur' || lang === 'ar';

const KEY = 'hrms.lang';
const listeners = new Set<() => void>();

function read(): Lang {
  try {
    const v = localStorage.getItem(KEY);
    return v === 'ur' || v === 'ar' ? v : 'en';
  } catch {
    return 'en';
  }
}

let current: Lang = read();

/** Sets <html lang dir> and loads the language's font the first time it is needed. */
export function applyLang(lang: Lang = current) {
  const html = document.documentElement;
  html.lang = lang;
  html.dir = isRtl(lang) ? 'rtl' : 'ltr';
  const font = FONTS[lang];
  if (font && !document.getElementById(`font-${lang}`)) {
    const link = document.createElement('link');
    link.id = `font-${lang}`;
    link.rel = 'stylesheet';
    link.href = font;
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
  const dict = DICTS[lang];
  if (!dict) return text;
  return dict[text] ?? dict[text.trim()] ?? text;
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
