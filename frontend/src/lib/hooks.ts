import { useEffect, useState } from 'react';
import { APP_NAME } from './brand';

export function useDebounced<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = window.setTimeout(() => setDebounced(value), delay);
    return () => window.clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

export function useDocumentTitle(title: string) {
  useEffect(() => {
    const prev = document.title;
    document.title = `${title} · ${APP_NAME}`;
    return () => {
      document.title = prev;
    };
  }, [title]);
}
