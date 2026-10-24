/** Lakh and crore grouping, as amounts are read in Pakistan: Rs 12,50,000. */
const lakh = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 });
const num = new Intl.NumberFormat('en-PK', { maximumFractionDigits: 2 });
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const MONTHS_LONG = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

export function formatCurrency(value: number | null | undefined): string {
  if (value == null) return '—';
  return `${value < 0 ? '-' : ''}Rs ${lakh.format(Math.abs(value))}`;
}

export function formatNumber(value: number | null | undefined): string {
  return value == null ? '—' : num.format(value);
}

/** Parses "YYYY-MM-DD" (or the date part of an ISO string) without timezone shifts. */
function dateParts(value: string): [number, number, number] | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : null;
}

/** "20 Oct 2026" from a DateOnly or a company-local date-time. */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const p = dateParts(value);
  if (!p) return value;
  return `${p[2]} ${MONTHS[p[1] - 1]} ${p[0]}`;
}

/** "20 Oct 2026, 09:14" for UTC timestamps, shown in the browser's local time. */
export function formatDateTimeUtc(value: string | null | undefined): string {
  if (!value) return '—';
  const iso = /[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return value;
  return `${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}, ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** "09:14" from a company-local date-time ("2026-10-20T09:14:33") or a TimeOnly ("09:14:00"). */
export function formatTime(value: string | null | undefined): string {
  if (!value) return '—';
  const m = /(\d{2}):(\d{2})(?::\d{2})?(?:\.\d+)?$/.exec(value.replace(/[zZ]$/, ''));
  if (!m) return value;
  const h = Number(m[1]);
  const suffix = h >= 12 ? 'PM' : 'AM';
  const h12 = h % 12 === 0 ? 12 : h % 12;
  return `${h12}:${m[2]} ${suffix}`;
}

export function formatMinutes(minutes: number | null | undefined): string {
  if (minutes == null || minutes <= 0) return '—';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h ? `${h}h ${m.toString().padStart(2, '0')}m` : `${m}m`;
}

export function monthName(month: number, long = true): string {
  return (long ? MONTHS_LONG : MONTHS)[month - 1] ?? String(month);
}

export function monthLabel(year: number, month: number): string {
  return `${monthName(month)} ${year}`;
}

export function pad(n: number): string {
  return n.toString().padStart(2, '0');
}

/** Today's date as "YYYY-MM-DD" in the browser's timezone. */
export function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** "09:00:00" -> "09:00" for <input type="time">. */
export function timeForInput(value: string | null | undefined): string {
  if (!value) return '';
  return value.slice(0, 5);
}

/** "09:00" -> "09:00:00" for TimeOnly. */
export function timeFromInput(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

export function initials(name: string | null | undefined): string {
  if (!name) return '?';
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase() || '?';
}

const ROLE_LABELS: Record<string, string> = {
  SuperAdmin: 'Owner',
  CompanyAdmin: 'Company Super Admin',
  HrManager: 'HR manager',
  Manager: 'Manager',
  Employee: 'Employee',
};

export function roleLabel(role: string | null | undefined): string {
  return role ? ROLE_LABELS[role] ?? role : '—';
}

/** "HalfDay" -> "Half day", "OnLeave" -> "On leave". */
export function humanize(value: string | null | undefined): string {
  if (!value) return '—';
  const s = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return s.charAt(0).toUpperCase() + s.slice(1);
}
