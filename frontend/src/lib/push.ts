import { useCallback, useEffect, useState } from 'react';
import { notificationsApi } from '../api/notifications';

/** unsupported: browser can't; blocked: the user said no in the browser; off/on: our switch. */
export type PushState = 'unsupported' | 'blocked' | 'off' | 'on' | 'loading';

const supported = () =>
  typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;

/** The VAPID key arrives base64url; PushManager wants raw bytes. */
function keyBytes(base64url: string): Uint8Array {
  const pad = '='.repeat((4 - (base64url.length % 4)) % 4);
  const raw = atob((base64url + pad).replace(/-/g, '+').replace(/_/g, '/'));
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
}

function encode(buf: ArrayBuffer | null): string {
  if (!buf) return '';
  return btoa(String.fromCharCode(...new Uint8Array(buf))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Phone/browser notifications for this device: whether they're on, and turning them on or off. */
export function usePush() {
  const [state, setState] = useState<PushState>(() => (supported() ? 'loading' : 'unsupported'));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!supported()) return;
    if (Notification.permission === 'denied') {
      setState('blocked');
      return;
    }
    navigator.serviceWorker.getRegistration().then(async (reg) => {
      const sub = await reg?.pushManager.getSubscription();
      setState(sub ? 'on' : 'off');
    });
  }, []);

  const enable = useCallback(async () => {
    setError(null);
    setState('loading');
    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        setState(permission === 'denied' ? 'blocked' : 'off');
        return;
      }
      const reg = (await navigator.serviceWorker.getRegistration()) ?? (await navigator.serviceWorker.register('/sw.js'));
      await navigator.serviceWorker.ready;
      const { publicKey } = await notificationsApi.pushKey();
      const sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(publicKey) as BufferSource });
      await notificationsApi.subscribePush({ endpoint: sub.endpoint, p256dh: encode(sub.getKey('p256dh')), auth: encode(sub.getKey('auth')) });
      setState('on');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setState('off');
    }
  }, []);

  const disable = useCallback(async () => {
    setError(null);
    setState('loading');
    try {
      const reg = await navigator.serviceWorker.getRegistration();
      const sub = await reg?.pushManager.getSubscription();
      if (sub) {
        await notificationsApi.unsubscribePush(sub.endpoint).catch(() => undefined);
        await sub.unsubscribe();
      }
      setState('off');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setState('on');
    }
  }, []);

  return { state, error, enable, disable };
}
