const TOKEN_KEY = 'hrms.token';
/** The owner's platform token, kept aside while they are inside a company. */
const PLATFORM_TOKEN_KEY = 'hrms.platformToken';

let token: string | null = readStored(TOKEN_KEY);
let platformToken: string | null = readStored(PLATFORM_TOKEN_KEY);

function readStored(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string | null) {
  try {
    if (value) localStorage.setItem(key, value);
    else localStorage.removeItem(key);
  } catch {
    // Storage may be unavailable (private mode); the in-memory token still works.
  }
}

export function getToken(): string | null {
  return token;
}

export function setToken(value: string | null): void {
  token = value;
  writeStored(TOKEN_KEY, value);
}

export function getPlatformToken(): string | null {
  return platformToken;
}

export function setPlatformToken(value: string | null): void {
  platformToken = value;
  writeStored(PLATFORM_TOKEN_KEY, value);
}

/** RFC 7807 ProblemDetails, as returned by the backend for every handled error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  /** Machine-readable reason, e.g. CompanyInactive, SubscriptionExpired, UserInactive. */
  code?: string;
}

/** 403 codes meaning "this company/user may not use the system right now". */
export const ACCESS_BLOCK_CODES = ['CompanyInactive', 'SubscriptionExpired', 'UserInactive'];

export function isAccessBlocked(err: unknown): err is ApiError {
  return err instanceof ApiError && err.status === 403 && ACCESS_BLOCK_CODES.includes(err.problem.code ?? '');
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;
  /** True when a 401/403 was already surfaced by the global handler (so callers need not toast it again). */
  handledGlobally = false;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title || problem.detail || `Request failed (${status})`);
    this.status = status;
    this.problem = problem;
  }

  /** Flat list of validation messages, if any. */
  get validationMessages(): string[] {
    return Object.values(this.problem.errors ?? {}).flat();
  }

  /** Validation messages keyed by camelCase field name. */
  fieldErrors(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const [key, messages] of Object.entries(this.problem.errors ?? {})) {
      const field = key.replace(/^\$\./, '');
      const name = field.charAt(0).toLowerCase() + field.slice(1);
      if (messages.length) out[name] = messages[0];
    }
    return out;
  }
}

/** Human-readable message for any thrown value. Validation errors include their messages. */
export function errorMessage(err: unknown): string {
  if (err instanceof ApiError) {
    const v = err.validationMessages;
    if (v.length) return `${err.message} ${v.join(' ')}`;
    return err.message;
  }
  if (err instanceof Error) return err.message;
  return 'Something went wrong.';
}

type Listener = (status: 401 | 403, problem: ProblemDetails) => void;
const listeners = new Set<Listener>();

/** Subscribe to auth failures (AuthContext logs out on 401 and toasts on 403). */
export function onAuthFailure(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export type Query = Record<string, string | number | boolean | null | undefined>;

export function buildQuery(query?: Query): string {
  if (!query) return '';
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(query)) {
    if (v === undefined || v === null || v === '') continue;
    params.set(k, String(v));
  }
  const s = params.toString();
  return s ? `?${s}` : '';
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  query?: Query;
  /** Do not broadcast 401/403 (login, me: the caller handles them). */
  silentAuth?: boolean;
  /** Use this bearer token instead of the active one. */
  token?: string;
}

function defaultTitle(status: number): string {
  switch (status) {
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 403:
      return "You don't have permission to do that.";
    case 404:
      return 'Not found.';
    case 429:
      return 'Too many attempts. Please wait a minute and try again.';
    default:
      return `Request failed (${status}).`;
  }
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  const bearer = options.token ?? token;
  if (bearer) headers.Authorization = `Bearer ${bearer}`;

  let res: Response;
  try {
    res = await fetch(`/api${path}${buildQuery(options.query)}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(0, { title: 'Cannot reach the server. Check your connection and try again.' });
  }

  if (!res.ok) {
    let problem: ProblemDetails = { status: res.status };
    try {
      const text = await res.text();
      if (text) problem = { ...problem, ...(JSON.parse(text) as ProblemDetails) };
    } catch {
      // Non-JSON error body.
    }
    if (!problem.title) problem.title = defaultTitle(res.status);
    const error = new ApiError(res.status, problem);
    if (!options.silentAuth && (res.status === 401 || res.status === 403)) {
      const status = res.status;
      error.handledGlobally = true;
      listeners.forEach((l) => l(status, problem));
    }
    throw error;
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const http = {
  get: <T>(path: string, query?: Query) => request<T>(path, { query }),
  post: <T = void>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body: body ?? {} }),
  put: <T = void>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body: body ?? {} }),
  del: <T = void>(path: string) => request<T>(path, { method: 'DELETE' }),
};
