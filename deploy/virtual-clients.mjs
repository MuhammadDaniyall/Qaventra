#!/usr/bin/env node
// Virtual clients: the people of companies that seed-demo.mjs filled use the product through the public API the way they
// would during a working day, and every call the app gets wrong is reported.
//
//   node deploy/virtual-clients.mjs --logins deploy/secrets/logins-a.txt,deploy/secrets/logins-b.txt      (a run: what is due now)
//   node deploy/virtual-clients.mjs --base http://localhost:5080 --logins a.txt --days 3                 (backfill past days)
//
// Working hours mode (the default, meant to be started every 30 minutes through the day)
//   Each person gets a plan for the day, worked out again identically on every run (seeded by company + person + date):
//   arrival (shift start with jitter, now and then late), breaks (lunch/Zuhr, Asr prayer, tea), departure (shift end with
//   jitter, some overtime, a few leave early or forget to check out), a few absences and sick days, and daytime work
//   spread over the hours in the office (leave, expenses, helpdesk, letters, timesheets, training, suggestions, manager
//   approvals, HR work: joiners, attendance fixes, announcements, recruitment, payroll near the month end). Shifts, off
//   days and holidays come from the roster (/attendance/roster), as on the app's Roster screen.
//   A run does every action whose time has come and that is not done yet, then waits for the actions due in the next
//   --lookahead minutes so punches land on their planned minute. What has been done is kept per day in the state file,
//   so nothing runs twice. Punches are live (the server stamps them with its own clock), so a punch more than --late-ok
//   minutes overdue (the Mac slept) is not made late; the person asks for a missed-punch fix on their next working day,
//   as a real person would.
//
// Flags
//   --base <url>       API server (default https://qaventra.runasp.net)
//   --logins <file>    a demo-logins.txt written by seed-demo.mjs; repeat the flag (or comma-separate) for several companies
//   --now <time>       pretend the current time is this (ISO; without a zone it is Pakistan time), for testing. The server
//                      stamps punches with its own clock, so when --now is more than 5 minutes from the real time punches
//                      are only logged ("dry"), never sent; all other actions are sent as usual.
//   --lookahead <min>  also do what falls due in the next <min> minutes, waiting for each (default 25; 0 with --now)
//   --late-ok <min>    a punch up to this many minutes overdue is still made now (default 40)
//   --plan             print today's plan for everyone and stop
//   --days <n> / --date <y-m-d>   backfill: play whole past days ending on --date (default today) the old way, through
//                      missed-punch and work-from-home requests; today, if in the range, is played as a normal run.
//   --force            backfill days that were already played
//   --seed <text>      changes the random choices (default none: the same day always plays out the same way)
//   --state-dir <dir>  where the state file and reports go (default deploy/secrets)
//
// Who acts: the HR manager, the line manager and the employees from the logins file. On the first run HR also gives
// logins (same shared password) to a few more of the manager's team and to an IT helpdesk agent, and every new joiner
// HR adds gets one too, so the cast grows over time. Logins and their tokens are kept in the state file
// (virtual-clients-state.json, git-ignored) so sign-in, which is rate limited, is rarely needed.
//
// Outcome of every call: ok; "refused" when the app turned the request down for a business reason a real user can run
// into (no leave balance, overlapping dates, ...); "failed" for 5xx, network errors and any other 4xx. Failures are
// what to look at: each is an app bug or something this script got wrong. Each run appends a section to that day's
// report, virtual-clients-report-<date>.md, and prints one line per company.

import { appendFileSync, existsSync, mkdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hostname } from 'node:os';

const here = dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const arg = (name, fallback) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 ? argv[i + 1] : fallback;
};
const args = (name) => argv.flatMap((a, i) => (a === `--${name}` && argv[i + 1] ? argv[i + 1].split(',') : []));
const flag = (name) => argv.includes(`--${name}`);

const BASE = arg('base', 'https://qaventra.runasp.net').replace(/\/$/, '');
const LOGIN_FILES = args('logins');
const BACKFILL = argv.includes('--days') || argv.includes('--date');
const DAYS = Math.max(1, Number(arg('days', '1')) || 1);
const FORCE = flag('force');
const SEED = arg('seed', '');
const PLAN_ONLY = flag('plan');
const SECRETS = resolve(arg('state-dir', join(here, 'secrets')));
const STATE_FILE = join(SECRETS, 'virtual-clients-state.json');
// Several computers may run this (a Mac and a Windows PC). Each company's progress is shared on the server, and a lease
// lets one computer work at a time. --defer-to-others <min> makes this computer stand aside while another one has run
// within that many minutes (the backup computer uses it, so the main one keeps the work while it is awake).
const HOLDER = arg('holder', hostname()).slice(0, 100);
const DEFER = Math.max(0, Number(arg('defer-to-others', '0')) || 0);
const LEASE_MINUTES = Math.max(10, Number(arg('lease-minutes', '45')) || 45);
const SHARED = !flag('no-share');

if (!LOGIN_FILES.length) {
  const fallback = join(here, 'secrets', 'demo-logins.txt');
  if (existsSync(fallback)) LOGIN_FILES.push(fallback);
  else {
    console.error('Give at least one --logins file (the demo-logins.txt that seed-demo.mjs writes).');
    process.exit(1);
  }
}

// ---------- Dates and the clock (company-local, Pakistan UTC+5) ----------

const pad = (n) => String(n).padStart(2, '0');
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (d, n) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() + n));
const parseDate = (s) => new Date(`${s}T00:00:00Z`);
const dayOf = (ms) => {
  const n = new Date(ms + 5 * 3600_000);
  return new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth(), n.getUTCDate()));
};
const minuteOf = (ms) => {
  const n = new Date(ms + 5 * 3600_000);
  return n.getUTCHours() * 60 + n.getUTCMinutes();
};

// The script's clock: the real time, or --now moving on at the real pace.
const NOW_ARG = arg('now');
const START_REAL = Date.now();
const START = NOW_ARG ? Date.parse(/[zZ]|[+-]\d\d:?\d\d$/.test(NOW_ARG) ? NOW_ARG : `${NOW_ARG}+05:00`) : START_REAL;
if (Number.isNaN(START)) {
  console.error(`--now ${NOW_ARG} is not a date and time.`);
  process.exit(1);
}
const clock = () => Date.now() - START_REAL + START;
/** Punches are stamped by the server's clock: only send them when ours agrees with it. */
const DRY_PUNCHES = Math.abs(START - START_REAL) > 5 * 60_000;
const LOOKAHEAD = Math.max(0, Number(arg('lookahead', NOW_ARG ? '0' : '25')) || 0);
const LATE_OK = Math.max(0, Number(arg('late-ok', '40')) || 0);
const TODAY = dayOf(START); // the simulated today
const REAL_TODAY = dayOf(START_REAL); // the server's today: requests cannot be dated after it
const LAST_DAY = arg('date') ? parseDate(arg('date')) : TODAY;
if (LAST_DAY > TODAY) {
  console.error('--date cannot be in the future.');
  process.exit(1);
}
const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const minutesOf = (t) => (t ? Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5)) : null);
const hhmm = (m) => `${pad(Math.floor(m / 60))}:${pad(m % 60)}:00`;
const hm = (m) => hhmm(m).slice(0, 5);

// ---------- Randomness (same day, same company → same choices) ----------

function hash(s) {
  let h = 2166136261;
  for (const c of s) h = Math.imul(h ^ c.charCodeAt(0), 16777619);
  return h >>> 0;
}
function rng(seedText) {
  let a = hash(seedText);
  const next = () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const r = {
    next,
    chance: (p) => next() < p,
    int: (min, max) => min + Math.floor(next() * (max - min + 1)),
    pick: (xs) => xs[Math.floor(next() * xs.length)],
    shuffle: (xs) => {
      const a2 = [...xs];
      for (let i = a2.length - 1; i > 0; i--) {
        const j = Math.floor(next() * (i + 1));
        [a2[i], a2[j]] = [a2[j], a2[i]];
      }
      return a2;
    },
  };
  return r;
}

// ---------- Event log ----------

const events = []; // { company, date, persona, kind: 'did'|'refused'|'failed', text, intent, method, path, status, body }
const ctx = { company: '', date: '' };
const record = (e) => events.push({ company: ctx.company, date: ctx.date, at: hm(minuteOf(clock())), ...e });

// ---------- HTTP ----------

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function http(method, path, body, token, headers = {}) {
  const isForm = typeof FormData !== 'undefined' && body instanceof FormData;
  let res;
  try {
    res = await fetch(`${BASE}/api${path}`, {
      method,
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(body != null && !isForm ? { 'Content-Type': 'application/json' } : {}),
        ...headers,
      },
      body: body == null ? undefined : isForm ? body : JSON.stringify(body),
    });
  } catch (e) {
    return { ok: false, status: 0, text: String(e.cause?.message ?? e.message), data: null, title: 'Network error' };
  }
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }
  const problem = data && typeof data === 'object' && !Array.isArray(data) ? data : {};
  return {
    ok: res.ok, status: res.status, text, data, title: problem.title ?? '', validation: !!problem.errors,
    retryAfter: Number(res.headers.get('retry-after')) || 0,
  };
}

/** Phrases the app uses when it turns down a request that a real user can legitimately make. */
const REFUSALS = {
  leave: [/Not enough .* (balance|earned yet)/, /already have a leave request covering/, /weekly off days? or (a )?holiday/, /cannot span two calendar years/,
    /must be requested at least/, /available after probation/, /is only for (male|female)/, /can be at most .* day/, /is closed\. Ask HR to reopen/],
  leaveDecide: [/needs a supporting document/, /This request is already/],
  attendanceRequest: [/already have a pending request for this day/, /on approved leave that day/, /Requests can go back at most/, /is closed\. Ask HR to reopen/,
    /before your joining date/],
  punch: [/You are .* m from .*; you must be within/, /away from your approved location/, /cannot mark attendance from the (phone|web)/,
    /only be marked from the office network/, /Check-in opens at/, /Location is required/, /already checked (in|out) today/,
    /already on a break/, /not on a break/],
  expense: [/is limited to Rs/, /needs a receipt/, /is not open to your grade/],
  letter: [/already asked for this letter/],
  training: [/already asked for this course/, /Only \d+ seats are left/, /not open for nominations/, /already started/],
  decide: [/This request is already/, /is not waiting for approval/, /needs a supporting document/],
  manual: [/is closed\. Ask HR to reopen/, /after the employee left/],
  timesheet: [/is closed\. Ask HR to reopen/, /already (submitted|approved)/i],
  helpdesk: [/the employee closes the ticket after you resolve it/],
  payroll: [/already (finalized|approved|submitted)/i],
  pay: [/is already claimed/, /already claimed as comp-off/, /Advances can total at most/, /An advance can be at most/, /is finalized\. Choose the next month/,
    /Overtime counts from/, /No salary is set/],
  loan: [/already a loan request waiting/, /is up to /, /is repaid in at most/, /is available after \d+ months of service/],
  compOff: [/already claimed comp-off/, /already claimed as pay/, /is closed\. Ask HR to reopen/, /normal working day/, /extra hours earn/],
  exemption: [/You have already asked for this day/, /exemption\(s\) a month; this month's are used/, /Ask within \d+ days/, /is closed\. Ask HR to reopen/,
    /does not take exemption requests/],
  offDay: [/already have a pending request for those dates/, /already your days off/, /is closed\. Ask HR to reopen/, /You have no shift/],
  shift: [/already have a shift request waiting/, /You are already on .* on the working days/, /already has a shift request on some/, /have the same schedule on those days/,
    /is closed\. Ask HR to reopen/, /no longer works here/, /no longer waiting for your answer/],
  advance: [/An advance is already waiting/],
  typedExpense: [/is limited to Rs/, /needs a receipt/, /is not open to your grade/, /is only for (women|men)/, /That advance is not open/, /There is no distance from/],
  travel: [/already have a trip on these dates/, /days' notice/, /The settlement is waiting for HR/, /This trip is settled/, /The trip is still waiting for approval/,
    /The trip is being settled/, /No settlement is waiting/],
  asset: [/already have a .* request open/, /already asked to return this/, /already returned this/, /Take that back first/, /is not available/, /already closed/],
  desk: [/already have a desk that day/, /is already booked that day/, /You have a permanent desk/, /You can book up to/, /Check in on the day/, /no longer active/,
    /released because nobody checked in/, /That day has passed/],
  fund: [/withdrawal from this fund is already waiting/, /Only .* is available/],
  profile: [/already have a change request waiting/, /Nothing has changed/],
  document: [/switched off attachments/, /At most \d+ files/, /must be smaller than/],
  ai: [/not switched on/, /used this month's AI questions/, /busy right now/, /key on this site is not valid/, /could not be (made|read)/],
  performance: [/Rate every item before sending/, /must add up to 100/, /This review cycle is closed/, /not waiting for calibration/, /already been launched/,
    /Nobody is in scope/],
  separation: [/already has an open separation/, /has already left/, /Only a requested resignation/, /exit interview opens once/],
  onboarding: [/This onboarding was cancelled/, /already introduced yourself/],
};

class Persona {
  constructor(co, { label, role, email, password }) {
    Object.assign(this, { co, label, role, email, password });
    this.token = null;
    this.me = null;
  }
  get employeeId() {
    return this.me?.employeeId;
  }

  /** Company and simulated day for this persona's events (companies run side by side). */
  where() {
    return { company: this.co.name, date: this.co.day ?? ctx.date };
  }

  did(text) {
    record({ ...this.where(), persona: this.label, kind: 'did', text });
  }

  /** The call worked but what came back is wrong: recorded as a failure (status shows what the server answered). */
  check(intent, ok, method, path, detail) {
    if (ok) return true;
    record({ ...this.where(), persona: this.label, kind: 'failed', intent, method, path, query: '', status: 'wrong data', title: detail, body: detail });
    return false;
  }

  /** One API call. Returns { ok, status, data, title }; failures and refusals are recorded, never thrown. */
  async req(intent, method, path, body, { refuse = [], headers, expect404 = false } = {}) {
    if (!this.token) await this.signIn();
    if (!this.token) return { ok: false, status: 0, data: null, title: 'not signed in' };
    let r = await http(method, path, body, this.token, headers);
    if (r.status === 401) {
      // Token revoked (password or role changed elsewhere): sign in once more and retry.
      this.token = null;
      await this.signIn(true);
      if (this.token) r = await http(method, path, body, this.token, headers);
    }
    if (r.ok) return r;
    if (expect404 && r.status === 404) return r;
    const refused = r.status === 400 && !r.validation && refuse.some((x) => x.test(r.title));
    record({
      ...this.where(), persona: this.label, kind: refused ? 'refused' : 'failed', intent, method, path: path.split('?')[0], query: path.includes('?') ? path.slice(path.indexOf('?')) : '',
      status: r.status, title: r.title, body: (r.text ?? '').replace(/\s+/g, ' ').slice(0, 400),
      text: refused ? `${intent}: refused (${r.title})` : undefined,
    });
    return r;
  }
  get = (intent, path, opts) => this.req(intent, 'GET', path, undefined, opts);
  post = (intent, path, body, opts) => this.req(intent, 'POST', path, body ?? {}, opts);
  put = (intent, path, body, opts) => this.req(intent, 'PUT', path, body, opts);

  async signIn(forceNew = false) {
    const saved = this.co.state.tokens[this.email];
    if (!forceNew && saved && saved.exp * 1000 - Date.now() > 30 * 60_000) {
      const me = await http('GET', '/auth/me', undefined, saved.token);
      if (me.ok) {
        this.token = saved.token;
        this.me = me.data;
        if (this.label.includes('@') && this.me?.fullName) this.label = this.me.fullName;
        return;
      }
    }
    for (let attempt = 0; attempt < 4; attempt++) {
      const r = await http('POST', '/auth/login', { email: this.email, password: this.password });
      if (r.status === 429) {
        const wait = Math.max(r.retryAfter, 61) * 1000;
        console.log(`    (sign-in rate limit reached; waiting ${Math.round(wait / 1000)} s)`);
        await sleep(wait);
        continue;
      }
      if (!r.ok) {
        record({ ...this.where(), persona: this.label, kind: 'failed', intent: 'Sign in', method: 'POST', path: '/auth/login', query: '', status: r.status, title: r.title,
          body: (r.text ?? '').slice(0, 400) });
        return;
      }
      this.token = r.data.token;
      this.me = r.data.user;
      if (this.label.includes('@') && this.me?.fullName) this.label = this.me.fullName;
      const exp = JSON.parse(Buffer.from(this.token.split('.')[1], 'base64url').toString()).exp;
      this.co.state.tokens[this.email] = { token: this.token, exp };
      saveState();
      return;
    }
    record({ ...this.where(), persona: this.label, kind: 'failed', intent: 'Sign in', method: 'POST', path: '/auth/login', query: '', status: 429, title: 'Rate limited after retries', body: '' });
  }
}

// ---------- State ----------

const state = existsSync(STATE_FILE) ? JSON.parse(readFileSync(STATE_FILE, 'utf8')) : { companies: {} };
function saveState() {
  mkdirSync(SECRETS, { recursive: true });
  writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
  // Copy the progress to the server soon after it changes, so another computer never repeats it.
  if (leased.size) {
    clearTimeout(pushTimer);
    pushTimer = setTimeout(() => void pushShared(), 15000);
  }
}

// ---------- Shared progress and the lease (see HOLDER) ----------

/** Companies whose lease this run holds. */
const leased = new Set();
let pushTimer = null;
/** What is shared between computers: never the sign-in tokens. */
const shareable = (s) => ({ people: s.people ?? [], days: s.days ?? [], castGrown: !!s.castGrown, progress: s.progress ?? {}, firstDay: s.firstDay ?? null });

/** Adds the server's progress to ours: everything either computer did counts as done. */
function mergeShared(local, shared) {
  if (!shared) return;
  local.castGrown ||= !!shared.castGrown;
  if (shared.firstDay && (!local.firstDay || shared.firstDay < local.firstDay)) local.firstDay = shared.firstDay;
  local.days = [...new Set([...(local.days ?? []), ...(shared.days ?? [])])].sort();
  local.people ??= [];
  const emails = new Set(local.people.map((p) => p.email));
  for (const p of shared.people ?? []) if (!emails.has(p.email)) local.people.push(p);
  local.progress ??= {};
  for (const [day, items] of Object.entries(shared.progress ?? {})) {
    const mine = (local.progress[day] ??= {});
    for (const [k, v] of Object.entries(items ?? {})) if (!(k in mine) || (v?.s === 'done' && mine[k]?.s !== 'done')) mine[k] = v;
  }
}

/** Takes the company's lease and merges the shared progress; false when another computer has the turn. */
async function takeTurn(co) {
  if (!SHARED) return true;
  const lease = await http('POST', '/setup/automation/lease', { holder: HOLDER, minutes: LEASE_MINUTES, deferToOthersMinutes: DEFER }, co.hr.token);
  if (!lease.ok) {
    // An older server without the lease: carry on alone, as before.
    if (lease.status === 404) return true;
    throw new Error(`Could not take the turn (${lease.status} ${lease.title})`);
  }
  if (!lease.data.granted) {
    co.skipReason = lease.data.reason ?? `${lease.data.holder ?? lease.data.lastHolder} has the turn`;
    return false;
  }
  leased.add(co);
  const shared = await http('GET', '/setup/automation', null, co.hr.token);
  if (shared.ok && shared.data?.state) {
    try {
      mergeShared(co.state, JSON.parse(shared.data.state));
    } catch {
      // unreadable shared state: ours stands and replaces it
    }
  }
  return true;
}

async function pushShared() {
  clearTimeout(pushTimer);
  for (const co of leased)
    await http('PUT', '/setup/automation/state', { holder: HOLDER, state: JSON.stringify(shareable(co.state)) }, co.hr.token);
}

async function endTurns() {
  await pushShared();
  for (const co of leased) await http('POST', '/setup/automation/release', { holder: HOLDER }, co.hr.token);
  leased.clear();
}

// ---------- Logins file ----------

function readLogins(file) {
  const text = readFileSync(resolve(file), 'utf8');
  const lines = text.split(/\r?\n/);
  const header = lines[0] ?? '';
  const company = header.replace(/\s+on\s+https?:\/\/\S+\s*$/, '').trim();
  const password = text.match(/Password for the logins below[^:]*:\s*(\S+)/)?.[1];
  const people = [];
  for (const l of lines) {
    const m = l.match(/^(Company admin|HR manager|Manager|Employee)\s+(\S+@\S+)/);
    if (m) people.push({ kind: m[1], email: m[2] });
  }
  if (!password || !people.length) throw new Error(`${file} does not look like a demo-logins.txt file.`);
  return { company, password, people, server: header.match(/on\s+(https?:\/\/\S+)/)?.[1] };
}

// ---------- Realistic content ----------

const MALE = ['Ahmed', 'Usama', 'Talha', 'Haris', 'Saad', 'Fahad', 'Arslan', 'Waleed', 'Shahzaib', 'Zeeshan', 'Faizan', 'Muneeb', 'Abdullah', 'Rizwan', 'Hamid', 'Junaid', 'Sohail', 'Kashif', 'Naveed', 'Adnan'];
const FEMALE = ['Ayesha', 'Mahnoor', 'Iqra', 'Hafsa', 'Anum', 'Kiran', 'Laiba', 'Areeba', 'Mehak', 'Amna', 'Rimsha', 'Sidra', 'Javeria', 'Maham', 'Saima', 'Bushra', 'Zoya', 'Fiza'];
const LAST = ['Khan', 'Malik', 'Chaudhry', 'Qureshi', 'Siddiqui', 'Butt', 'Sheikh', 'Mirza', 'Abbasi', 'Awan', 'Gill', 'Bhatti', 'Rana', 'Hashmi', 'Niazi', 'Jatoi', 'Baloch', 'Shah', 'Zaidi', 'Rizvi', 'Cheema', 'Warraich', 'Dar', 'Lodhi'];
const AREAS = ['Wapda Town, Lahore', 'Johar Town, Lahore', 'Model Town, Lahore', 'DHA Phase 6, Lahore', 'Bahria Town, Lahore', 'Gulberg III, Lahore', 'Township, Lahore',
  'Valencia Town, Lahore', 'Allama Iqbal Town, Lahore', 'Cantt, Lahore', 'Faisal Town, Lahore', 'Garden Town, Lahore'];
const BANKS = ['Meezan Bank', 'HBL', 'Bank Alfalah', 'MCB', 'UBL', 'Allied Bank', 'Faysal Bank'];

const LEAVE_REASONS = {
  casual: ['CNIC renewal at the NADRA office', "Parent-teacher meeting at my son's school", 'Family function in Gujranwala', 'Car registration work at the Excise office',
    'Shifting to a new house', "Sister's mehndi", 'Gas bill correction at the SNGPL office', 'Passport renewal appointment'],
  annual: ['Family trip to Naran and Kaghan', "Brother's wedding in Multan", 'Umrah with my parents', 'Visiting grandparents in Sialkot', 'Trip to Skardu with family',
    "Cousin's wedding in Karachi", 'Family holiday in Murree'],
  sick: ['High fever and flu', 'Dengue test, doctor advised rest', 'Food poisoning', 'Severe migraine', 'Wisdom tooth extraction', 'Viral infection, doctor advised rest',
    'Throat infection because of the smog'],
};

const EXPENSES = [
  { title: 'Client visit, Sundar Industrial Estate', items: [['Fuel', 'Fuel for the client visit to Sundar Industrial Estate', 3500, 8000], ['Meals', 'Lunch on the way back', 1200, 2500]] },
  { title: 'Islamabad trip for client UAT', items: [['Travel', 'Daewoo Express ticket Lahore to Islamabad and back', 6000, 8500], ['Accommodation', 'Hotel one night, Blue Area', 14000, 24000],
    ['Meals', 'Dinner with the client team', 5000, 12000]] },
  { title: 'Careem rides for client meetings', items: [['Travel', 'Careem to the client office in Gulberg and back', 1200, 3500]] },
  { title: 'Mobile bill', items: [['Mobile', 'Jazz postpaid bill, official calls', 2000, 4500]] },
  { title: 'Release night dinner', items: [['Meals', 'Team dinner during the production release', 6000, 15000]] },
  { title: 'Office supplies', items: [['OfficeSupplies', 'Printer toner and two reams of A4 paper', 4500, 9000]] },
  { title: 'Doctor visit', items: [['Medical', 'OPD consultation and medicines', 2500, 7000]] },
  { title: 'Online course', items: [['Training', 'Udemy course: Docker and Kubernetes', 2500, 6000]] },
  { title: 'Karachi visit to the port office', items: [['Travel', 'PIA return ticket LHE-KHI', 28000, 48000], ['Accommodation', 'Hotel two nights, Shahrah-e-Faisal', 28000, 44000],
    ['Travel', 'Airport transfers', 2500, 5000]] },
];

const TICKETS = {
  it: [
    ['Laptop very slow since the Windows update', 'My laptop takes 10 minutes to start and Teams keeps freezing since yesterday’s update.', 'High'],
    ['Outlook not syncing on my phone', 'Email stopped syncing on my Android phone. Password was changed last week.', 'Medium'],
    ['VPN not connecting from home', 'FortiClient says "credential or SSLVPN configuration is wrong" when I try from home.', 'High'],
    ['Access to the Finance shared drive', 'Please give me read access to the Finance shared drive for the month-end reports.', 'Low'],
    ['Printer on the 2nd floor is jammed', 'The HP printer near the pantry shows a paper jam and nobody can print.', 'Medium'],
    ['Second monitor flickering', 'The Dell monitor on my desk flickers every few seconds. I think the cable is loose.', 'Low'],
    ['Need Postman and Docker installed', 'Please install Postman and Docker Desktop; I do not have admin rights.', 'Medium'],
    ['WiFi drops in meeting room 3', 'The WiFi keeps dropping during calls in meeting room 3. Happened in two client calls today.', 'High'],
  ],
  hr: [
    ['Leave balance looks wrong', 'My casual leave balance shows 2 days less than I expected. Can you please check?', 'Medium'],
    ['EOBI number missing on payslip', 'My EOBI registration number is not printed on my payslip.', 'Low'],
    ['Tax deduction higher this month', 'Income tax on my last payslip is higher than usual. Please explain the calculation.', 'Medium'],
    ['Salary certificate needed urgently', 'I need a salary certificate for a car loan application by Thursday.', 'High'],
    ['Medical reimbursement status', 'I submitted my medical bills two weeks ago. When will they be reimbursed?', 'Medium'],
    ['Change salary account to Meezan Bank', 'Please change my salary account to my new Meezan Bank account from next month.', 'Medium'],
    ['Provident fund statement', 'Can I get my provident fund statement for the last financial year?', 'Low'],
  ],
};

const LETTER_REASONS = ['Required by Meezan Bank for car financing', 'For a UK visit visa application', 'For the rent agreement of an apartment in DHA',
  'For Umrah visa processing', 'Required for a credit card application at HBL', 'For a Schengen visa application'];

const SUGGESTIONS = [
  ['Shuttle service from Raiwind Road', 'Many of us commute from Raiwind Road and Bahria Town. A shared shuttle would save time and fuel.'],
  ['Better ventilation in the prayer area', 'The prayer area gets very stuffy after Zuhr. An exhaust fan would help.'],
  ['Monthly knowledge sharing sessions', 'A one-hour session every month where a team shares what they built would help everyone learn.'],
  ['Flexible hours during Ramzan', 'Office hours of 9 to 3 during Ramzan, as many companies do, would help people fasting.'],
  ['Solar panels for the office', 'Solar panels with batteries would keep us working during load-shedding and cut the electricity bill.'],
  ['N95 masks during smog season', 'Please keep N95 masks at reception during the smog months; the AQI in Lahore is often above 300.'],
  ['Water dispenser filters', 'The water dispenser filters on the 2nd floor seem overdue for a change.'],
  ['Standing desks pilot', 'A few standing desks that anyone can book would help people with back pain.'],
];

const COURSES = [['AWS Certified Solutions Architect – Associate', 'Udemy', 12000], ['PMP exam preparation', 'PMI Lahore Chapter', 85000],
  ['Advanced Excel for analytics', 'LUMS Executive Education', 35000], ['Professional Scrum Master I', 'Scrum.org', 60000],
  ['Business English communication', 'British Council Pakistan', 40000], ['Data analysis with Power BI', 'Coursera', 15000]];

const ANNOUNCEMENTS = [
  ['Smog advisory', 'News', 'Because of the high AQI in Lahore, please wear a mask while commuting. Anyone with asthma may work from home on bad days after telling their manager.'],
  ['Blood donation camp', 'Event', 'Sundas Foundation will hold a blood donation camp in the cafeteria on Thursday from 11 am to 3 pm. Every donation helps a thalassemia patient.'],
  ['Winter office timings', 'Policy', 'From 15 November office timings will be 9:30 am to 5:30 pm until the end of February.'],
  ['Inter-department cricket tournament', 'Event', 'Tape-ball cricket tournament on Saturday at the Model Town ground. Register your team with HR by Wednesday.'],
  ['Payroll cut-off', 'News', 'Please submit expense claims and attendance corrections by the 25th to be included in this month’s salary.'],
  ['Fire drill on Friday', 'Event', 'There will be a fire drill on Friday at 11 am. Please follow the floor wardens to the assembly point in the parking area.'],
  ['Dengue awareness', 'News', 'Dengue cases are rising. Use repellent, and report any standing water in the office to admin.'],
  ['Iqbal Day holiday', 'Policy', 'The office will remain closed on 9 November for Iqbal Day.'],
  ['Quarterly town hall', 'Event', 'The quarterly town hall will be held in the main hall on the last Thursday of the month at 4 pm. Bring your questions for management.'],
  ['New medical insurance panel', 'Policy', 'Our medical insurance now covers Shaukat Khanum, Doctors Hospital and Hameed Latif Hospital. Cards will be handed out next week.'],
];

const NOTES = {
  approve: {
    Leave: ['Approved. Please hand over your tasks before you go.', 'Enjoy your time off.', 'Approved. Make sure the sprint demo is covered.', 'OK, approved.'],
    LeaveSick: ['Approved, get well soon.', 'Take rest, approved.'],
    Expense: ['Verified against the trip plan.', 'OK, receipts checked.', 'Approved.'],
    Attendance: ['Confirmed, you were in the office.', 'Approved. Saw you online on Teams that day.', 'OK, the machine missed your punch.'],
    Timesheet: ['Looks right.', 'Approved.', 'Hours match the sprint board.'],
    Training: ['Good choice, approved.', 'Approved. Share what you learn with the team afterwards.'],
    Letter: ['Approved.', 'OK.'],
    default: ['Approved.', 'OK.'],
  },
  reject: {
    Leave: ['Sorry, the client release falls on these dates. Please move it by a week.', 'Two people from the team are already off on these dates; can you move it by a week?'],
    Expense: ['Please attach the receipts and resubmit.', 'Meals above Rs 5,000 need prior approval; please split it per person.'],
    Attendance: ['The machine shows no entry and I did not see you that day. Please talk to me.', 'Please apply for leave for that day instead.'],
    Timesheet: ['Hours on Wednesday look high; please check and resubmit.', 'Please split the hours by task.'],
    Training: ['Let us plan this for next quarter; this quarter’s budget is used up.'],
    default: ['Please discuss this with me first.'],
  },
};

const tinyReceipt = {
  fileName: 'receipt.png', contentType: 'image/png',
  base64: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
};

// ---------- One company ----------

async function setupCompany(file) {
  const logins = readLogins(file);
  if (logins.server && logins.server.replace(/\/$/, '') !== BASE)
    console.log(`  Note: ${file} was written for ${logins.server}, running against ${BASE}.`);
  const key = `${BASE}|${logins.company}`;
  state.companies[key] ??= { tokens: {}, people: [], days: [] };
  const co = { name: logins.company, key, password: logins.password, state: state.companies[key] };
  co.domain = logins.people.find((p) => p.kind === 'HR manager')?.email.split('@')[1];
  const make = (label, role, email) => new Persona(co, { label, role, email, password: logins.password });

  const hrLogin = logins.people.find((p) => p.kind === 'HR manager');
  const mgrLogin = logins.people.find((p) => p.kind === 'Manager');
  if (!hrLogin) throw new Error(`${file} has no HR manager login.`);
  co.hr = make('HR', 'hr', hrLogin.email);
  await co.hr.signIn();
  if (!co.hr.token) throw new Error(`Could not sign in as ${hrLogin.email}.`);
  co.hr.label = `${co.hr.me.fullName} (HR manager)`;
  if (!PLAN_ONLY && !(await takeTurn(co))) {
    const e = new Error(`not this computer's turn: ${co.skipReason}`);
    e.skip = true;
    throw e;
  }
  if (mgrLogin) {
    co.mgr = make('Manager', 'manager', mgrLogin.email);
    await co.mgr.signIn();
    if (co.mgr.me) co.mgr.label = `${co.mgr.me.fullName} (line manager)`;
  }
  co.employees = [];
  for (const p of logins.people.filter((x) => x.kind === 'Employee')) co.employees.push(make(p.email, 'employee', p.email));
  co.agents = [];
  for (const p of co.state.people) {
    const persona = make(p.name, p.role, p.email);
    persona.joinedOn = p.joinedOn;
    (p.role === 'agent' ? co.agents : co.employees).push(persona);
  }
  return co;
}

/** Company lookups everyone uses, read once per run as HR. */
async function loadReference(co) {
  const hr = co.hr;
  const get = async (path) => (await hr.get(`Load ${path}`, path)).data ?? [];
  co.ref = {
    shifts: await get('/setup/shifts'),
    offices: await get('/setup/office-locations'),
    leaveTypes: (await get('/setup/leave-types')).filter((t) => t.isActive),
    helpdesk: await get('/helpdesk/categories'),
    departments: await get('/departments'),
    designations: await get('/designations'),
    letters: await get('/letters/self-service'),
    programs: await get('/training/programs'),
    suggestionCats: await get('/suggestions/categories'),
    leaveReasons: (await get('/leave-rules/reasons')).filter((x) => x.isActive),
    holidays: new Set(),
  };
  for (const y of new Set([addDays(LAST_DAY, -DAYS).getUTCFullYear(), LAST_DAY.getUTCFullYear(), LAST_DAY.getUTCFullYear() + 1]))
    for (const h of await get(`/setup/holidays?year=${y}`)) co.ref.holidays.add(h.date);
  co.ref.defaultShift = co.ref.shifts.find((s) => s.isDefault) ?? co.ref.shifts[0];
  const all = [];
  for (let page = 1; page < 20; page++) {
    const r = (await hr.get('List employees', `/employees?page=${page}&pageSize=200`)).data;
    if (!r) break;
    all.push(...r.items);
    if (all.length >= r.total || !r.items.length) break;
  }
  co.ref.staff = all;
  // Lookups of the other modules; a company without them (or an older server) just skips those actions.
  const soft = async (path) => {
    const res = await http('GET', path, undefined, hr.token);
    return res.ok && Array.isArray(res.data) ? res.data : [];
  };
  Object.assign(co.ref, {
    loanTypes: (await soft('/pay/loan-types')).filter((t) => t.isActive),
    travelReasons: (await soft('/travel/reasons')).filter((t) => t.isActive),
    floors: await soft('/desks/floors'),
    assetTypes: (await soft('/assets/types')).filter((t) => t.isActive),
    reports: await soft('/reports'),
  });
}

const isOff = (co, d) => co.ref.holidays.has(iso(d)) || (co.ref.defaultShift?.weeklyOffDays ?? ['Saturday', 'Sunday']).includes(WEEKDAYS[d.getUTCDay()]);
function workdaysFrom(co, from, n) {
  const out = [];
  for (let d = from, guard = 0; out.length < n && guard < 60; d = addDays(d, 1), guard++) if (!isOff(co, d)) out.push(d);
  return out;
}
function previousWorkday(co, d) {
  let x = addDays(d, -1);
  for (let i = 0; i < 14 && isOff(co, x); i++) x = addDays(x, -1);
  return x;
}

/** First run: HR gives self-service logins to a few more of the manager's team and to an IT helpdesk agent. */
async function growCast(co, r) {
  if (co.state.castGrown) return;
  const hr = co.hr;
  const have = new Set([...co.employees, ...co.agents].map((p) => p.email.toLowerCase()));
  const team = co.mgr?.token ? ((await co.mgr.get('Team list', '/employees?pageSize=200')).data?.items ?? []) : [];
  const grant = async (emp, role) => {
    const email = (emp.email ?? `${emp.fullName.replace(/\s+/g, '.')}@${co.domain}`).toLowerCase();
    if (have.has(email)) return;
    const res = await hr.post(`Give ${emp.fullName} a self-service login`, `/employees/${emp.id}/login`, { email, password: co.password, role: 'Employee' });
    if (!res.ok) return;
    hr.did(`Gave ${emp.fullName} a self-service login`);
    co.state.people.push({ name: emp.fullName, email, role, employeeId: emp.id });
    const persona = new Persona(co, { label: emp.fullName, role, email, password: co.password });
    (role === 'agent' ? co.agents : co.employees).push(persona);
    have.add(email);
  };
  for (const e of r.shuffle(team.filter((e) => !e.hasLogin)).slice(0, 3)) await grant(e, 'employee');
  const it = co.ref.helpdesk.find((c) => /\bIT\b/i.test(c.name));
  const agentId = it?.agentIds?.[0];
  const agent = co.ref.staff.find((e) => e.id === agentId);
  if (agent && !agent.hasLogin) await grant(agent, 'agent');
  co.state.castGrown = true;
  saveState();
}

// ---------- Employee behaviour ----------

/** Backfill of a past day: anyone the machine missed asks for it to be fixed. */
async function attendance(p, co, d, r, { workday }) {
  if (!workday) return;
  // A past day: anyone the machine missed asks for it to be fixed.
  const month = `${d.getUTCFullYear()}-${d.getUTCMonth() + 1}`;
  p.cache ??= {};
  p.cache[month] ??= (await p.get('Open my attendance', `/attendance/my?year=${d.getUTCFullYear()}&month=${d.getUTCMonth() + 1}`)).data ?? [];
  const rec = p.cache[month].find((a) => a.date === iso(d));
  if (rec?.checkOut) return;
  p.cache.leave ??= (await p.get('Open my leave', '/leave/my')).data ?? [];
  if (p.cache.leave.some((l) => l.status === 'Approved' && l.fromDate <= iso(d) && l.toDate >= iso(d))) return;
  const pending = (p.cache.requests ??= (await p.get('Open my attendance requests', '/attendance/requests/my')).data ?? []);
  if (pending.some((x) => x.date === iso(d) && x.status === 'Pending')) return;
  const inAt = rec ? rec.checkIn.slice(11, 19) : hhmm(r.int(530, 560));
  const outAt = hhmm(r.int(1075, 1130));
  let kind, reason;
  if (rec) [kind, reason] = ['MissedPunch', r.pick(['Forgot to check out, left at 6 pm after the team meeting.', 'The machine did not read my thumb on the way out.', 'Left in a hurry for a family emergency and forgot to punch out.'])];
  else if (r.chance(0.35)) [kind, reason] = ['WorkFromHome', r.pick(['Worked from home, the car was at the mechanic. Was on Teams all day.', 'Worked from home because of the smog; my asthma was bad.', 'Waited at home for the internet technician; worked online.'])];
  else if (r.chance(0.7)) [kind, reason] = ['MissedPunch', r.pick(['The attendance machine was not reading thumbs in the morning.', 'Forgot my card and the guard let me in; no punch recorded.', 'Phone battery died so I could not check in from the app.'])];
  else return; // simply absent; HR will notice
  const res = await p.post(`Ask to fix attendance for ${iso(d)} (${kind})`, '/attendance/requests', { date: iso(d), kind, checkIn: inAt, checkOut: outAt, reason },
    { refuse: REFUSALS.attendanceRequest });
  if (res.ok) {
    p.did(`Asked to fix attendance for ${iso(d)}: ${kind === 'WorkFromHome' ? 'worked from home' : 'missed punch'}`);
    pending.push({ date: iso(d), status: 'Pending' });
  }
}

function leaveType(co, re) {
  return co.ref.leaveTypes.find((t) => re.test(t.name));
}

async function applyLeave(p, co, d, r) {
  const roll = r.next();
  let type, from, days, reason, extra = {};
  if (roll < 0.2) {
    type = leaveType(co, /sick/i);
    from = isOff(co, d) ? workdaysFrom(co, d, 1)[0] : d;
    days = r.chance(0.7) ? 1 : 2;
    reason = r.pick(LEAVE_REASONS.sick);
  } else if (roll < 0.65) {
    type = leaveType(co, /casual/i);
    from = workdaysFrom(co, addDays(d, r.int(1, 7)), 1)[0];
    days = 1;
    reason = r.pick(LEAVE_REASONS.casual);
    if (r.chance(0.25) && type?.allowHalfDay) extra = { duration: 'HalfDay', halfDayPart: r.chance(0.5) ? 'FirstHalf' : 'SecondHalf' };
  } else {
    type = leaveType(co, /annual/i);
    from = workdaysFrom(co, addDays(d, r.int(5, 25)), 1)[0];
    days = r.int(2, 5);
    reason = r.pick(LEAVE_REASONS.annual);
  }
  if (!type || !from) return;
  // Leave types with a reasons list need one picked from it, as on the leave form; mostly one that needs no document.
  const reasons = co.ref.leaveReasons.filter((x) => x.leaveTypeId === type.id);
  if (reasons.length) {
    const choice = r.chance(0.85) ? (reasons.find((x) => !x.requiresDocument) ?? reasons[0]) : r.pick(reasons);
    extra.reasonId = choice.id;
    if (choice.maxDays) days = Math.min(days, Math.max(1, Math.floor(choice.maxDays)));
  }
  const span = workdaysFrom(co, from, days);
  const to = span.at(-1);
  if (to.getUTCFullYear() !== from.getUTCFullYear()) return;
  const res = await p.post(`Apply for ${type.name} ${iso(from)}${days > 1 ? ` to ${iso(to)}` : ''}`, '/leave',
    { leaveTypeId: type.id, fromDate: iso(from), toDate: iso(to), reason, ...extra }, { refuse: REFUSALS.leave });
  if (res.ok) {
    const expected = extra.duration === 'HalfDay' ? 0.5 : span.length;
    p.check(`Apply for ${type.name}`, Number(res.data.days) === expected && res.data.status === 'Pending', 'POST', '/leave',
      `Leave ${iso(from)}–${iso(to)} came back as ${res.data.days} day(s), ${res.data.status}; expected ${expected} working day(s), Pending.`);
  }
  if (res.ok) p.did(`Applied for ${extra.duration === 'HalfDay' ? 'half a day of ' : ''}${type.name} ${iso(from)}${days > 1 ? ` to ${iso(to)}` : ''}: “${reason}”`);
}

async function cancelLeave(p, d) {
  const mine = (await p.get('Open my leave', '/leave/my')).data ?? [];
  const pending = mine.find((l) => l.status === 'Pending' && l.fromDate > iso(d));
  if (!pending) return;
  const res = await p.post('Cancel a pending leave request', `/leave/${pending.id}/cancel`, {});
  if (res.ok) p.did(`Cancelled pending ${pending.leaveType} for ${pending.fromDate} (plans changed)`);
}

async function claimExpense(p, d, r) {
  const t = r.pick(EXPENSES);
  const latest = d > REAL_TODAY ? REAL_TODAY : d;
  const day = addDays(latest, -r.int(0, 6));
  const items = t.items.map(([category, description, min, max]) => ({
    date: iso(day), category, description, amount: Math.round(r.int(min, max) / 50) * 50, ...(r.chance(0.5) ? { receipt: tinyReceipt } : {}),
  }));
  const total = items.reduce((s, i) => s + i.amount, 0);
  const res = await p.post(`Claim expenses: ${t.title}`, '/expenses', { title: t.title, items }, { refuse: REFUSALS.expense });
  if (res.ok) {
    const receipts = items.filter((i) => i.receipt).length;
    p.check('Claim expenses', Number(res.data.total) === total && res.data.itemCount === items.length && res.data.receiptCount === receipts, 'POST', '/expenses',
      `Claim came back with total ${res.data.total}, ${res.data.itemCount} item(s), ${res.data.receiptCount} receipt(s); sent ${total}, ${items.length}, ${receipts}.`);
  }
  if (res.ok) p.did(`Filed an expense claim “${t.title}” for Rs ${total.toLocaleString('en-PK')}`);
}

async function raiseTicket(p, co, r) {
  const hrCat = co.ref.helpdesk.find((c) => /HR|payroll/i.test(c.name) && c.isActive);
  const itCat = co.ref.helpdesk.find((c) => /\bIT\b/i.test(c.name) && c.isActive);
  const useIt = itCat && (!hrCat || r.chance(0.6));
  const cat = useIt ? itCat : hrCat;
  if (!cat) return;
  const [subject, description, priority] = r.pick(useIt ? TICKETS.it : TICKETS.hr);
  const typeId = cat.types?.find((t) => t.isActive)?.id ?? null;
  const res = await p.post(`Raise a helpdesk ticket: ${subject}`, '/helpdesk/tickets', { categoryId: cat.id, subject, description, priority, typeId });
  if (res.ok) p.did(`Raised helpdesk ticket “${subject}” (${cat.name}, ${priority})`);
}

async function followTickets(p, r) {
  const mine = (await p.get('Open my tickets', '/helpdesk/tickets/my')).data ?? [];
  for (const t of mine.filter((t) => t.employeeId === p.employeeId)) {
    if (t.status === 'Resolved' && r.chance(0.7)) {
      const rating = r.pick([5, 5, 4, 4, 3]);
      const res = await p.post(`Close resolved ticket ${t.code}`, `/helpdesk/tickets/${t.id}/close`,
        { rating, feedback: rating >= 4 ? 'Sorted quickly, thank you.' : 'Took a while but it works now.' });
      if (res.ok) p.did(`Closed ticket ${t.code} and rated it ${rating}/5`);
    } else if (t.status === 'WaitingOnEmployee') {
      const res = await p.post(`Reply on ticket ${t.code}`, `/helpdesk/tickets/${t.id}/comments`, { body: 'Done, I have tried that. Please check now.' });
      if (res.ok) p.did(`Replied on ticket ${t.code} that was waiting for them`);
    }
  }
}

async function requestLetter(p, co, r) {
  if (!co.ref.letters.length) return;
  const t = r.pick(co.ref.letters);
  const reason = r.pick(LETTER_REASONS);
  const res = await p.post(`Request a ${t.name}`, '/letters/request', { templateId: t.id, reason }, { refuse: REFUSALS.letter });
  if (res.ok) p.did(`Requested the “${t.name}” letter (“${reason}”)`);
}

async function timesheet(p, co, d, r, { submitDay }) {
  p.cache ??= {};
  p.cache.projects ??= (await p.get('Open my projects', '/projects')).data ?? [];
  const projects = p.cache.projects.filter((x) => x.status === 'Active' || !x.status);
  if (!projects.length || isOff(co, d)) return;
  const monday = addDays(d, -((d.getUTCDay() + 6) % 7));
  const week = (await p.get('Open my timesheet', `/timesheets/week?start=${iso(monday)}`)).data;
  if (!week || week.status === 'Submitted' || week.status === 'Approved') return;
  const project = r.pick(projects);
  const task = project.tasks?.filter((t) => !t.isDone && (!t.assigneeEmployeeId || t.assigneeEmployeeId === p.employeeId))?.[0];
  const minutes = r.int(6, 9) * 60;
  const split = projects.length > 1 && r.chance(0.4);
  const other = split ? projects.find((x) => x.id !== project.id) : null;
  const lines = week.lines.filter((l) => l.date !== iso(d)).map(({ date, projectId, taskId, minutes: m, note }) => ({ date, projectId, taskId, minutes: m, note }));
  lines.push({ date: iso(d), projectId: project.id, taskId: task?.id ?? null, minutes: split ? minutes - 120 : minutes, note: r.pick(['Development', 'Bug fixing', 'Code review and fixes', 'Client call and follow-ups', 'Testing']) });
  if (other) lines.push({ date: iso(d), projectId: other.id, taskId: null, minutes: 120, note: 'Support' });
  const saved = await p.put(`Log ${Math.round(minutes / 60)} h on the timesheet for ${iso(d)}`, '/timesheets/week', { weekStart: iso(monday), lines }, { refuse: REFUSALS.timesheet });
  if (!saved.ok) return;
  const sent = lines.reduce((t, l) => t + l.minutes, 0);
  p.check('Save timesheet', saved.data?.totalMinutes === sent, 'PUT', '/timesheets/week', `Week total came back as ${saved.data?.totalMinutes} min; the lines sent add up to ${sent} min.`);
  p.did(`Logged ${minutes / 60} h on ${project.name}${other ? ` and ${other.name}` : ''} for ${iso(d)}`);
  if (submitDay && r.chance(0.85)) {
    const sub = await p.post(`Submit the timesheet for the week of ${iso(monday)}`, `/timesheets/week/submit?start=${iso(monday)}`, {}, { refuse: REFUSALS.timesheet });
    if (sub.ok) p.did(`Submitted the timesheet for the week of ${iso(monday)} (${Math.round((sub.data?.totalMinutes ?? 0) / 60)} h)`);
  }
}

async function requestTraining(p, co, r) {
  const useProgram = co.ref.programs.length && r.chance(0.4);
  const [course, provider, cost] = r.pick(COURSES);
  const body = useProgram
    ? { programId: r.pick(co.ref.programs).id, reason: 'This will help me in the upcoming cloud migration work.' }
    : { courseName: course, provider, cost, preferredDate: iso(addDays(REAL_TODAY, r.int(14, 60))), reason: 'This will help me take on more responsibility in my current role.' };
  const res = await p.post(`Ask for training: ${useProgram ? 'a company programme' : course}`, '/training/requests', body, { refuse: REFUSALS.training });
  if (res.ok) p.did(`Asked to attend ${useProgram ? co.ref.programs.find((x) => x.id === body.programId)?.title : `${course} (${provider})`}`);
}

async function suggest(p, co, r) {
  const [title, details] = r.pick(SUGGESTIONS);
  const cat = co.ref.suggestionCats.find((c) => c.isActive !== false) ?? null;
  const anonymous = r.chance(0.3);
  const res = await p.post(`Submit a suggestion: ${title}`, '/suggestions', { title, details, categoryId: cat?.id ?? null, isAnonymous: anonymous });
  if (res.ok) p.did(`Submitted a${anonymous ? 'n anonymous' : ''} suggestion: “${title}”`);
}

async function social(p, r) {
  const list = (await p.get('Read announcements', '/announcements')).data ?? [];
  const unread = list.filter((a) => !a.isRead);
  for (const a of unread.slice(0, 3)) if (r.chance(0.6)) {
    const res = await p.post(`Mark announcement “${a.title}” read`, `/announcements/${a.id}/read`, {});
    if (res.ok) p.did(`Read the announcement “${a.title}”`);
  }
  const polls = (await p.get('Open polls', '/announcements/polls')).data ?? [];
  for (const poll of polls.filter((x) => x.isOpen && !x.myVote && !x.myOptionId && !x.hasVoted)) if (r.chance(0.5)) {
    const option = r.pick(poll.options);
    const res = await p.post(`Vote in the poll “${poll.question}”`, `/announcements/polls/${poll.id}/vote`, { optionId: option.id }, { refuse: [/already voted/i] });
    if (res.ok) p.did(`Voted “${option.text}” in “${poll.question}”`);
  }
  const celebrations = (await p.get('Open celebrations', '/dashboard/celebrations')).data ?? [];
  for (const c of celebrations.filter((c) => c.isToday && !c.isMe && !c.wishedByMe)) if (r.chance(0.5)) {
    const res = await p.post(`Wish ${c.name ?? 'a colleague'}`, `/dashboard/celebrations/${c.employeeId}/wish`, { kind: c.kind });
    if (res.ok) p.did(`Wished ${c.name ?? c.fullName ?? 'a colleague'} a happy ${String(c.kind).toLowerCase()}`);
  }
  await p.get('Open the dashboard', '/dashboard');
  const feed = (await p.get('Open notifications', '/notifications')).data;
  if (feed?.unread && r.chance(0.4)) {
    const res = await p.post('Mark all notifications read', '/notifications/read-all', {});
    if (res.ok) p.did(`Cleared ${feed.unread} notifications`);
  }
  if (r.chance(0.2)) await p.get('Check leave balance', '/leave/balances');
  if (r.chance(0.15)) {
    const slips = (await p.get('Open my payslips', '/payroll/my-payslips')).data ?? [];
    if (slips[0]) {
      const s = await p.get('Open the latest payslip', `/payroll/payslips/${slips[0].id}`);
      if (s.ok) p.did('Looked at the latest payslip');
      if (r.chance(0.3)) {
        const x = await p.post('Ask AI to explain my payslip', `/payroll/payslips/${slips[0].id}/explain?lang=${r.chance(0.3) ? 'ur' : 'en'}`, {});
        if (x.ok) p.did(`Payslip explained: ${x.data.summary.slice(0, 60)}`);
      }
    }
  }
}

async function employeeDay(p, co, d, r, opts) {
  if (!p.token) await p.signIn();
  if (!p.token || !p.employeeId) return;
  if (p.joinedOn && p.joinedOn > iso(d)) return;
  await attendance(p, co, d, r, opts);
  if (opts.weekendWorker || opts.workday) {
    await social(p, r);
    if (r.chance(0.14)) await applyLeave(p, co, d, r);
    if (r.chance(0.04)) await cancelLeave(p, d);
    if (r.chance(0.12)) await claimExpense(p, d, r);
    if (r.chance(0.14)) await raiseTicket(p, co, r);
    await followTickets(p, r);
    if (r.chance(0.07)) await requestLetter(p, co, r);
    if (opts.workday && r.chance(0.7)) await timesheet(p, co, d, r, opts);
    if (r.chance(0.05)) await requestTraining(p, co, r);
    if (r.chance(0.05)) await suggest(p, co, r);
    await moreModules(p, co, d, r, EMPLOYEE_MODULES);
  } else if (r.chance(0.3)) await social(p, r);
}

// ---------- Approvers ----------

function note(kind, type, r, sick = false) {
  const pool = NOTES[kind];
  return r.pick((sick && kind === 'approve' && pool.LeaveSick) || pool[type] || pool.default);
}

/** Works through the approvals inbox. HR also approves over the chain for requests stuck with someone who never signs in. */
async function workInbox(p, r, { hr = false } = {}) {
  const inbox = (await p.get('Open the approvals inbox', '/approvals/inbox')).data ?? [];
  let done = 0;
  const decided = [];
  for (const item of inbox) {
    const age = (Date.now() - Date.parse(`${item.createdAtUtc}Z`)) / 86_400_000;
    let force = false;
    if (!item.assignedToMe) {
      if (!hr || age < 3 || !r.chance(0.5)) continue;
      force = true;
    } else if (!r.chance(0.85)) continue; // some wait for tomorrow
    const sick = /sick/i.test(item.summary);
    const rejectRate = { Leave: 0.15, Expense: 0.12, Attendance: 0.1, Timesheet: 0.1, Training: 0.25 }[item.requestType] ?? 0.05;
    const approve = force || sick || !r.chance(rejectRate);
    const body = force
      ? { approve: true, force: true, note: 'Approved by HR on behalf of the line manager, who has not been able to sign in this week.' }
      : { approve, note: note(approve ? 'approve' : 'reject', item.requestType, r, sick) };
    const res = await p.post(`${approve ? 'Approve' : 'Reject'} ${item.requestType} from ${item.employeeName} (${item.summary})`,
      `/approvals/${item.requestType}/${item.subjectId}/decide`, body, { refuse: REFUSALS.decide });
    if (res.ok) {
      done++;
      decided.push({ ...item, forced: force });
      p.did(`${force ? 'Approved over the chain' : approve ? 'Approved' : 'Rejected'} ${item.employeeName}'s ${item.requestType.toLowerCase()} request (${item.summary})${body.note ? `: “${body.note}”` : ''}`);
    } else if (res.status === 400 && /supporting document/.test(res.title)) {
      const rej = await p.post(`Send back ${item.employeeName}'s leave for a document`, `/approvals/${item.requestType}/${item.subjectId}/decide`,
        { approve: false, note: 'Please attach the medical certificate and apply again.' }, { refuse: REFUSALS.decide });
      if (rej.ok) p.did(`Sent back ${item.employeeName}'s leave asking for a medical certificate`);
    }
  }
  if (!inbox.length) p.did('Approvals inbox was empty');
  if (decided.length) {
    // Whatever was just decided must leave this person's to-do list (multi-step requests move on to someone else).
    const again = (await p.get('Reopen the approvals inbox', '/approvals/inbox')).data ?? [];
    for (const x of decided) {
      const still = again.find((y) => y.subjectId === x.subjectId && y.requestType === x.requestType);
      p.check(`Decide ${x.requestType} from ${x.employeeName}`, !still || (!x.forced && still.step > x.step),
        'GET', '/approvals/inbox', `${x.requestType} “${x.summary}” was decided (step ${x.step}/${x.totalSteps}${x.forced ? ', forced' : ''}) but is still in the inbox at step ${still?.step}, assignedToMe=${still?.assignedToMe}.`);
    }
  }
  return done;
}

/** Nominates one or two of the team for an upcoming training session. */
async function nominate(m, r) {
  const sessions = ((await m.get('Open training sessions', '/training/sessions')).data ?? [])
    .filter((s) => s.status === 'Scheduled' && Date.parse(`${s.startsAtUtc}Z`) > Date.now() && (!s.capacity || s.enrolled < s.capacity));
  const s = sessions[0];
  const team = (await m.get('Team list', '/employees?pageSize=200')).data?.items ?? [];
  if (s && team.length) {
    const detail = (await m.get('Open a training session', `/training/sessions/${s.id}`)).data;
    const enrolled = new Set((detail?.enrollments ?? []).map((e) => e.employeeId));
    const pickIds = r.shuffle(team.filter((e) => !enrolled.has(e.id))).slice(0, r.int(1, 2));
    if (pickIds.length && detail?.canNominate !== false) {
      const res = await m.post(`Nominate ${pickIds.map((e) => e.fullName).join(', ')} for ${s.program}`, `/training/sessions/${s.id}/enroll`,
        { employeeIds: pickIds.map((e) => e.id) }, { refuse: REFUSALS.training });
      if (res.ok) m.did(`Nominated ${pickIds.map((e) => e.fullName).join(' and ')} for ${s.program}`);
    }
  }
}

async function managerDay(m, co, d, r, opts) {
  if (!m?.token) return;
  await attendance(m, co, d, r, opts);
  if (!opts.workday && !opts.weekendWorker) return;
  await workInbox(m, r);
  await m.get('Look at the team’s attendance', `/attendance/daily?date=${iso(d)}`);
  await social(m, r);
  if (r.chance(0.2)) await nominate(m, r);
  if (r.chance(0.08)) await applyLeave(m, co, d, r);
  if (r.chance(0.08)) await claimExpense(m, d, r);
  await moreModules(m, co, d, r, MANAGER_MODULES);
}

// ---------- HR ----------

async function addJoiner(hr, co, d, r) {
  const female = r.chance(0.4);
  const first = r.pick(female ? FEMALE : MALE);
  const last = r.pick(LAST);
  const n = r.int(10, 99);
  const email = `${first}.${last}${n}@${co.domain}`.toLowerCase();
  const dept = r.pick(co.ref.departments.filter((x) => !/management/i.test(x.name))) ?? co.ref.departments[0];
  const desig = r.pick(co.ref.designations.filter((x) => !/chief|head|manager|lead/i.test(x.title))) ?? co.ref.designations[0];
  const managerId = r.chance(0.6) && co.mgr?.employeeId ? co.mgr.employeeId : dept?.headEmployeeId ?? co.mgr?.employeeId ?? null;
  // Joiners are added ahead of their first day, as HR does once the offer is signed.
  const joining = workdaysFrom(co, addDays(d, r.int(1, 10)), 1)[0];
  const dob = `${r.int(1994, 2002)}-${pad(r.int(1, 12))}-${pad(r.int(1, 28))}`;
  const emp = await hr.post(`Add new joiner ${first} ${last}`, '/employees', {
    firstName: first, lastName: last, joiningDate: iso(joining), status: 'Probation', email,
    phone: `+92 3${r.int(0, 4)}${r.int(0, 9)} ${r.int(1000000, 9999999)}`, cnic: `35202-${r.int(1000000, 9999999)}-${r.int(1, 9)}`,
    dateOfBirth: dob, gender: female ? 'Female' : 'Male', address: `House ${r.int(1, 400)}, Street ${r.int(1, 30)}, ${r.pick(AREAS)}`,
    bankName: r.pick(BANKS), bankAccount: `PK${r.int(10, 99)}${r.pick(['MEZN', 'HABB', 'ALFH', 'MUCB'])}000${r.int(1000000000, 9999999999)}`,
    departmentId: dept?.id ?? null, designationId: desig?.id ?? null, managerId, shiftId: co.ref.defaultShift?.id ?? null,
    maritalStatus: r.chance(0.5) ? 'Single' : 'Married', bloodGroup: r.pick(['A+', 'B+', 'O+', 'AB+', 'B-', 'O-']),
  });
  if (!emp.ok) return;
  const id = emp.data.id;
  hr.did(`Added new joiner ${first} ${last} (${desig?.title ?? 'staff'}, ${dept?.name ?? 'no department'}), joining ${iso(joining)}`);
  const gross = r.int(9, 30) * 10000;
  const sal = await hr.put(`Set ${first} ${last}'s salary`, `/employees/${id}/salary`, {
    effectiveFrom: iso(joining), basicSalary: Math.round(gross * 0.6), eobiApplicable: true,
    components: [{ name: 'House rent', kind: 'Earning', amount: Math.round(gross * 0.3), isTaxable: true }, { name: 'Utilities', kind: 'Earning', amount: Math.round(gross * 0.1), isTaxable: true }],
  });
  if (sal.ok) hr.did(`Set ${first}'s gross salary to Rs ${gross.toLocaleString('en-PK')}`);
  const login = await hr.post(`Give ${first} ${last} a login`, `/employees/${id}/login`, { email, password: co.password, role: 'Employee' });
  if (login.ok) {
    hr.did(`Gave ${first} ${last} a login (${email})`);
    co.state.people.push({ name: `${first} ${last}`, email, role: 'employee', employeeId: id, joinedOn: iso(joining) });
    saveState();
    // From their joining day the new joiner acts like everyone else.
    const persona = new Persona(co, { label: `${first} ${last}`, role: 'employee', email, password: co.password });
    persona.joinedOn = iso(joining);
    co.employees.push(persona);
  }
  if (r.chance(0.7)) {
    const templates = (await hr.get('Open onboarding templates', '/onboarding/templates')).data ?? [];
    const t = templates.find((x) => x.isDefault) ?? templates[0];
    if (t) {
      const ob = await hr.post(`Start onboarding for ${first} ${last}`, '/onboarding', { employeeId: id, templateId: t.id, reportingTime: '09:00:00', reportingPlace: 'Reception, Head Office' });
      if (ob.ok) hr.did(`Started the “${t.name}” onboarding checklist for ${first}`);
    }
  }
}

async function correctAttendance(hr, co, d, r) {
  const day = previousWorkday(co, d);
  const rows = (await hr.get(`Open daily attendance for ${iso(day)}`, `/attendance/daily?date=${iso(day)}`)).data ?? [];
  // People who already asked for a fix themselves are left to their approver.
  const asked = new Set(((await hr.get('Open pending attendance requests', '/attendance/requests/approvals?status=Pending')).data ?? [])
    .filter((x) => x.date === iso(day)).map((x) => x.employeeId));
  const open = rows.filter((x) => x.record && !x.record.checkOut && !asked.has(x.employeeId));
  for (const row of r.shuffle(open).slice(0, 4)) {
    const inAt = row.record.checkIn.slice(11, 19);
    const out = Math.max(minutesOf(inAt) + 240, r.int(1075, 1110));
    const res = await hr.post(`Correct ${row.fullName}'s missing check-out on ${iso(day)}`, '/attendance/manual',
      { employeeId: row.employeeId, date: iso(day), checkIn: inAt, checkOut: hhmm(Math.min(out, 1435)) }, { refuse: REFUSALS.manual });
    if (res.ok) hr.did(`Corrected ${row.fullName}'s missing check-out on ${iso(day)} (set to ${hhmm(Math.min(out, 1435)).slice(0, 5)})`);
  }
  if (!open.length) hr.did(`Checked attendance for ${iso(day)}: nothing to correct`);
}

async function announce(hr, d, r) {
  const existing = new Set(((await hr.get('Open announcements to manage', '/announcements/manage')).data ?? []).map((a) => a.title));
  const fresh = ANNOUNCEMENTS.filter(([t]) => !existing.has(t));
  if (!fresh.length) return;
  const [title, kind, body] = r.pick(fresh);
  const res = await hr.post(`Post announcement “${title}”`, '/announcements', {
    title, body, kind, publishFrom: iso(d), publishTo: iso(addDays(d, 30)), departmentIds: [], isPinned: r.chance(0.2), linkUrl: null, notify: false,
  });
  if (res.ok) hr.did(`Posted the announcement “${title}”`);
}

async function recruit(hr, co, d, r) {
  const jobs = ((await hr.get('Open job posts', '/recruitment/jobs')).data ?? []).filter((j) => j.status === 'Open');
  if (!jobs.length) return;
  const job = r.pick(jobs);
  // New applicant now and then.
  if (r.chance(0.35)) {
    const female = r.chance(0.4);
    const [first, last] = [r.pick(female ? FEMALE : MALE), r.pick(LAST)];
    const f = new FormData();
    for (const [k, v] of Object.entries({
      FirstName: first, LastName: last, Email: `${first}.${last}.${r.int(100, 999)}@gmail.com`.toLowerCase(), Phone: `+92 3${r.int(0, 4)}${r.int(0, 9)} ${r.int(1000000, 9999999)}`,
      City: r.pick(['Lahore', 'Islamabad', 'Karachi', 'Faisalabad']), CurrentTitle: r.pick(['Software Engineer', 'Associate Engineer', 'QA Engineer', 'Executive']),
      CurrentCompany: r.pick(['NetSol Technologies', 'Arbisoft', '10Pearls', 'Techlogix', 'Contour Software', 'VentureDive', 'Systems Limited']),
      ExperienceYears: r.int(1, 8), ExpectedSalary: (job.salaryMin ?? 150000) + r.int(0, 10) * 10000, NoticeDays: r.pick([15, 30, 60]), Skills: 'Communication, MS Office', Source: r.pick(['CareersPage', 'Referral', 'Advertisement', 'Agency', 'WalkIn']),
    })) f.append(k, String(v));
    const res = await hr.req(`Add applicant ${first} ${last} to ${job.title}`, 'POST', `/recruitment/jobs/${job.id}/candidates`, f);
    if (res.ok) hr.did(`Added applicant ${first} ${last} to “${job.title}”`);
  }
  const pipeline = (await hr.get(`Open the ${job.title} pipeline`, `/recruitment/jobs/${job.id}/pipeline`)).data ?? [];
  const move = async (a, stage, reason) => {
    const res = await hr.post(`Move ${a.name} to ${stage}`, `/recruitment/applications/${a.id}/move`, { stage, reason: reason ?? null, emailCandidate: false });
    if (res.ok) hr.did(`Moved ${a.name} (${job.title}) to ${stage}${reason ? `: “${reason}”` : ''}`);
    return res.ok;
  };
  for (const a of r.shuffle(pipeline.filter((x) => !x.isBlacklisted)).slice(0, 2)) {
    if (a.stage === 'Applied') {
      if (r.chance(0.3)) await move(a, 'Rejected', 'Experience does not match the role.');
      else await move(a, 'Shortlisted');
    } else if (a.stage === 'Shortlisted') {
      if (await move(a, 'Interview')) {
        const when = workdaysFrom(co, addDays(REAL_TODAY, r.int(1, 5)), 1)[0];
        const interviewers = [hr.employeeId, job.hiringManagerEmployeeId].filter(Boolean);
        const res = await hr.post(`Schedule an interview for ${a.name}`, `/recruitment/applications/${a.id}/interviews`, {
          title: 'Technical interview', startsAtUtc: `${iso(when)}T${pad(r.int(5, 9))}:00:00Z`, durationMinutes: 60, mode: r.pick(['Onsite', 'Online']),
          where: r.pick(['Meeting room 2', 'Google Meet', 'Microsoft Teams']), interviewerIds: [...new Set(interviewers)], notes: null, emailCandidate: false,
        });
        if (res.ok) hr.did(`Scheduled an interview with ${a.name} on ${iso(when)}`);
      }
    } else if (a.stage === 'Interview') {
      if (r.chance(0.35)) await move(a, 'Rejected', 'Did not clear the technical round.');
      else {
        const salary = Math.round(((job.salaryMin ?? 150000) + (job.salaryMax ?? job.salaryMin ?? 200000)) / 2 / 5000) * 5000;
        const res = await hr.post(`Make ${a.name} an offer`, `/recruitment/applications/${a.id}/offer`,
          { salary, joiningDate: iso(workdaysFrom(co, addDays(d, 30), 1)[0]), note: 'Hybrid, three days a week in the office', emailCandidate: false });
        if (res.ok) hr.did(`Offered ${a.name} Rs ${salary.toLocaleString('en-PK')} for ${job.title}`);
      }
    } else if (a.stage === 'Offer' && (a.offerStatus === 'Sent' || a.offerStatus === 'Pending' || a.offerStatus == null)) {
      const accepted = r.chance(0.7);
      const res = await hr.post(`Record ${a.name}'s answer to the offer`, `/recruitment/applications/${a.id}/offer-response`,
        { accepted, note: accepted ? 'Accepted over the phone; will send the signed copy.' : 'Got a counter offer from the current employer.' });
      if (res.ok) hr.did(`${a.name} ${accepted ? 'accepted' : 'declined'} the offer for ${job.title}`);
    } else if (a.stage === 'Offer' && a.offerStatus === 'Accepted' && r.chance(0.5)) {
      const res = await hr.post(`Hire ${a.name}`, `/recruitment/applications/${a.id}/hire`, { employeeCode: null });
      if (res.ok) hr.did(`Hired ${a.name} as an employee (${res.data?.employeeCode})`);
    }
  }
}

async function payroll(hr, d) {
  const dom = d.getUTCDate();
  const nextMonth = addDays(d, 1).getUTCMonth() !== d.getUTCMonth();
  let year = d.getUTCFullYear(), month = d.getUTCMonth() + 1;
  if (dom <= 3) {
    const prev = addDays(new Date(Date.UTC(year, month - 1, 1)), -1);
    [year, month] = [prev.getUTCFullYear(), prev.getUTCMonth() + 1];
  } else if (dom < 27 && !nextMonth) return;
  const runs = (await hr.get('Open payroll runs', '/payroll/runs')).data ?? [];
  const label = `${year}-${pad(month)}`;
  const existing = runs.find((x) => (x.frequency ?? 'Monthly') === 'Monthly' && x.year === year && x.month === month)
    ?? runs.find((x) => (x.frequency ?? 'Monthly') === 'Monthly' && String(x.periodStart ?? '').startsWith(label));
  if (existing && existing.status !== 'Draft') {
    hr.did(`Payroll for ${label} is already ${existing.status.toLowerCase()}; nothing to do`);
    return;
  }
  const res = await hr.post(`Process (preview) payroll for ${label}`, '/payroll/runs', { year, month }, { refuse: REFUSALS.payroll });
  if (!res.ok) return;
  const run = res.data.run ?? {};
  const slips = res.data.payslips ?? [];
  const net = run.totalNet ?? slips.reduce((s, p) => s + (p.netPay ?? p.net ?? 0), 0);
  hr.did(`Processed draft payroll for ${label}: ${slips.length} payslips, net Rs ${Math.round(net).toLocaleString('en-PK')}${res.data.warnings?.length ? `; warnings: ${res.data.warnings.join(' / ')}` : ''}`);
  if (slips[0]) await hr.get('Open a payslip from the draft run', `/payroll/payslips/${slips[0].id}`);
}

/** Replies to and resolves tickets in the queue the agent sees; HR only takes the HR categories when an IT agent exists. */
async function answerTickets(agent, co, r, { onlyCategories } = {}) {
  const queue = (await agent.get('Open the helpdesk queue', '/helpdesk/tickets?scope=all&status=open')).data ?? [];
  const mine = queue.filter((t) => !onlyCategories || onlyCategories.includes(t.categoryId));
  for (const t of mine.slice(0, 5)) {
    if (t.status === 'Open') {
      if (!t.assigneeEmployeeId && agent.employeeId) {
        const cat = co.ref.helpdesk.find((c) => c.id === t.categoryId);
        const res = await agent.put(`Assign ticket ${t.code} to myself`, `/helpdesk/tickets/${t.id}`, { assigneeEmployeeId: agent.employeeId, priority: t.priority, categoryId: t.categoryId, typeId: t.typeId ?? null },
          { refuse: [/Assign it to one of the/] });
        if (res.ok) agent.did(`Picked up ticket ${t.code} “${t.subject}”${cat ? ` (${cat.name})` : ''}`);
      }
      const reply = r.pick(['Looking into it. Will update you by this evening.', 'Thanks for reporting. Can you share a screenshot of the error?', 'Noted, we are checking this with the vendor.']);
      const c = await agent.post(`Reply on ticket ${t.code}`, `/helpdesk/tickets/${t.id}/comments`, { body: reply, isInternal: false });
      if (c.ok) agent.did(`Replied on ${t.code}: “${reply}”`);
      const s = await agent.post(`Mark ticket ${t.code} in progress`, `/helpdesk/tickets/${t.id}/status`, { status: 'InProgress', note: null }, { refuse: REFUSALS.helpdesk });
      if (s.ok) agent.did(`Set ${t.code} to in progress`);
    } else if (t.status === 'InProgress' && r.chance(0.6)) {
      const fix = agent.role === 'agent'
        ? r.pick(['Fixed. Please check and close the ticket if it works.', 'Done from our side; restart your laptop once.', 'Replaced the cable and updated the driver.'])
        : r.pick(['This has been corrected; it will show from the next payslip.', 'Done. Please check the portal and close the ticket if it looks right.', 'Shared the document on your email.']);
      const s = await agent.post(`Resolve ticket ${t.code}`, `/helpdesk/tickets/${t.id}/status`, { status: 'Resolved', note: fix }, { refuse: REFUSALS.helpdesk });
      if (s.ok) agent.did(`Resolved ${t.code} “${t.subject}”: “${fix}”`);
    }
  }
  if (!mine.length) agent.did('Helpdesk queue was clear');
}

async function reviewSuggestions(hr, r) {
  const list = (await hr.get('Open suggestions to review', '/suggestions/review')).data ?? [];
  for (const s of list.filter((x) => x.status === 'New').slice(0, 2)) {
    const accepted = r.chance(0.4);
    const status = accepted ? 'Accepted' : 'UnderReview';
    const response = accepted ? 'Good idea. Admin will arrange this within the month.' : 'Thank you. We are discussing this with management and will update you.';
    const res = await hr.put(`Respond to suggestion “${s.title}”`, `/suggestions/${s.id}/respond`, { status, response });
    if (res.ok) hr.did(`Responded to the suggestion “${s.title}” (${status === 'Accepted' ? 'accepted' : 'under review'})`);
  }
}

async function hrDay(hr, co, d, r, opts) {
  await attendance(hr, co, d, r, opts);
  if (!opts.workday) return;
  await workInbox(hr, r, { hr: true });
  await correctAttendance(hr, co, d, r);
  if (r.chance(0.35)) await addJoiner(hr, co, d, r);
  if (r.chance(0.25)) await announce(hr, d, r);
  if (r.chance(0.6)) await recruit(hr, co, d, r);
  await payroll(hr, d);
  const hrCats = co.ref.helpdesk.filter((c) => /HR|payroll/i.test(c.name)).map((c) => c.id);
  await answerTickets(hr, co, r, co.agents.length ? { onlyCategories: hrCats } : {});
  if (r.chance(0.5)) await reviewSuggestions(hr, r);
  await hr.get('Open the HR dashboard', '/dashboard');
  await moreModules(hr, co, d, r, HR_MODULES);
}

// ---------- The other modules: pay, loans, comp-off, exemptions, shifts, travel, assets, desks, funds, documents, AI, performance ----------

/** The server dates requests by its own clock: a simulated day ahead of it counts as the server's today. */
const serverDay = (d) => (d > REAL_TODAY ? REAL_TODAY : d);
const monthStart = (d) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1));
const phoneNumber = (r) => `+92 3${r.int(0, 4)}${r.int(0, 9)} ${r.int(1000000, 9999999)}`;
const CITIES = ['Karachi', 'Islamabad', 'Multan', 'Faisalabad', 'Peshawar', 'Quetta'];

const AI_QUESTIONS = ['How many casual leaves do I have left this year?', 'When is the next public holiday?', 'What was my net pay last month?',
  'How do I apply for a loan?', 'Who approves my leave requests?', 'How many days was I late this month?', 'What is the notice period for resignation?'];
const GOALS = [['Ship the customer portal revamp', 'Released to production by the end of the period with no P1 bugs'],
  ['Cut average ticket resolution time', 'From 3 days to under 2 days'], ['Raise unit test coverage of my modules', 'Above 70% line coverage'],
  ['Mentor a junior team member', 'Weekly one-to-ones and one completed starter project'], ['Earn a cloud certification', 'AWS or Azure associate level, passed'],
  ['Automate the monthly release checklist', 'Release time down from a day to two hours'], ['Document the integration APIs', 'Every public endpoint documented in the wiki']];

/** This month's and last month's attendance records (fresh: what earlier runs did may have changed them). */
async function recentRecords(p, day) {
  const out = [];
  for (const m of [addDays(monthStart(day), -1), day]) {
    const list = (await p.get('Open my attendance', `/attendance/my?year=${m.getUTCFullYear()}&month=${m.getUTCMonth() + 1}`)).data ?? [];
    out.push(...list);
  }
  return out;
}

/** Overtime for a recent late evening, or an advance against next salary. */
async function payRequest(p, co, d, r) {
  const today = serverDay(d);
  const [year, month] = [REAL_TODAY.getUTCFullYear(), REAL_TODAY.getUTCMonth() + 1];
  if (r.chance(0.6)) {
    const day = previousWorkday(co, today);
    const hours = r.pick([1, 1.5, 2, 2.5, 3]);
    const reason = r.pick(['Stayed back for the production release.', 'Client call ran late into the evening.', 'Month-end closing work.']);
    const res = await p.post(`Claim ${hours} h overtime for ${iso(day)}`, '/pay/requests',
      { kind: 'Overtime', title: null, amount: null, isTaxable: false, year, month, workDate: iso(day), hours, reason }, { refuse: REFUSALS.pay });
    if (res.ok) p.did(`Claimed ${hours} h overtime for ${iso(day)} (Rs ${Number(res.data.amount).toLocaleString('en-PK')}): “${reason}”`);
    return;
  }
  const amount = r.int(2, 8) * 5000;
  const reason = r.pick(['School fees for the children are due this week.', 'Car repair after an accident.', 'Shifting to a new house this month.']);
  const res = await p.post(`Ask for a salary advance of Rs ${amount}`, '/pay/requests',
    { kind: 'AdvanceSalary', title: null, amount, isTaxable: false, year, month, workDate: null, hours: null, reason }, { refuse: REFUSALS.pay });
  if (res.ok) p.did(`Asked for a Rs ${amount.toLocaleString('en-PK')} salary advance: “${reason}”`);
}

/** A loan of a type they qualify for, within its limits, repaid from next month. */
async function requestLoan(p, co, d, r) {
  const mine = (await p.get('Open my loans', '/pay/loans/my')).data ?? [];
  if (mine.some((l) => l.status === 'Pending' || (l.status === 'Approved' && l.remaining > 0))) return;
  const joined = co.ref.staff.find((e) => e.id === p.employeeId)?.joiningDate;
  const eligible = co.ref.loanTypes.filter((t) => !t.minServiceMonths || !joined
    || iso(new Date(Date.UTC(+joined.slice(0, 4), +joined.slice(5, 7) - 1 + t.minServiceMonths, +joined.slice(8, 10)))) <= iso(REAL_TODAY));
  // Now and then someone asks for a type they are not yet eligible for (the app says no).
  const t = co.ref.loanTypes.length ? (eligible.length && r.chance(0.9) ? r.pick(eligible) : r.pick(co.ref.loanTypes)) : null;
  let amount = r.int(5, 40) * 10000;
  if (t?.maxAmount) amount = Math.min(amount, t.maxAmount);
  let installments = r.pick([6, 10, 12, 18, 24]);
  if (t?.maxInstallments) installments = Math.min(installments, t.maxInstallments);
  const start = new Date(Date.UTC(REAL_TODAY.getUTCFullYear(), REAL_TODAY.getUTCMonth() + 1, 1));
  const reason = r.pick(['Down payment for a motorbike.', 'Medical treatment for my mother.', 'Wedding of my sister.', 'Renovation of the house.']);
  const res = await p.post(`Apply for a ${t?.name ?? 'loan'} of Rs ${amount}`, '/pay/loans', {
    title: t?.name ?? 'Personal loan', amount, installments, startYear: start.getUTCFullYear(), startMonth: start.getUTCMonth() + 1, reason, loanTypeId: t?.id ?? null,
  }, { refuse: REFUSALS.loan });
  if (res.ok) p.did(`Applied for a ${t?.name ?? 'loan'} of Rs ${amount.toLocaleString('en-PK')} over ${installments} installments: “${reason}”`);
}

/** Comp-off for a recent day off they came in to work. */
async function claimCompOff(p, co, d, r) {
  const today = serverDay(d);
  const claimed = new Set(((await p.get('Open my comp-off claims', '/leave/comp-off/my')).data ?? [])
    .filter((c) => c.status === 'Pending' || c.status === 'Approved').map((c) => c.workedOn));
  const offDay = (key) => {
    const cell = co.cal.get(p.employeeId)?.get(key);
    return cell ? !isWorkday(cell) : isOff(co, parseDate(key));
  };
  const worked = (await recentRecords(p, today)).filter((a) => a.date < iso(today) && a.date >= iso(addDays(today, -50)) && !claimed.has(a.date)
    && offDay(a.date) && a.workedMinutes >= 240);
  const rec = worked.at(-1);
  if (!rec) return;
  const days = rec.workedMinutes >= 420 && r.chance(0.8) ? 1 : 0.5;
  const reason = r.pick(['Came in on the weekend for the server migration.', 'Weekend support for the client go-live.', 'Worked on the holiday to finish the audit.']);
  const res = await p.post(`Claim ${days} day comp-off for ${rec.date}`, '/leave/comp-off', { workedOn: rec.date, days, reason }, { refuse: REFUSALS.compOff });
  if (res.ok) p.did(`Claimed ${days === 1 ? 'a day' : 'half a day'} of comp-off for working on ${rec.date}: “${reason}”`);
}

/** Annual leave away from home, with the address and phone where they can be reached. */
async function applyStationLeave(p, co, d, r) {
  const type = leaveType(co, /annual/i) ?? leaveType(co, /casual/i);
  if (!type) return;
  const from = workdaysFrom(co, addDays(d, r.int(7, 30)), 1)[0];
  const span = workdaysFrom(co, from, r.int(2, 5));
  const to = span.at(-1);
  if (!from || to.getUTCFullYear() !== from.getUTCFullYear()) return;
  const city = r.pick(CITIES.concat(['Murree', 'Skardu', 'Swat']));
  const extra = {};
  const reasons = co.ref.leaveReasons.filter((x) => x.leaveTypeId === type.id);
  if (reasons.length) extra.reasonId = (reasons.find((x) => !x.requiresDocument) ?? reasons[0]).id;
  const reason = r.pick([`Family visit to ${city}.`, `Trip to ${city} with the family.`, `Attending a wedding in ${city}.`]);
  const res = await p.post(`Apply for station leave to ${city} ${iso(from)} to ${iso(to)}`, '/leave', {
    leaveTypeId: type.id, fromDate: iso(from), toDate: iso(to), reason, ...extra,
    isStationLeave: true, stationAddress: `House ${r.int(1, 300)}, Street ${r.int(1, 40)}, ${city}`, stationPhone: phoneNumber(r),
  }, { refuse: REFUSALS.leave });
  if (res.ok) p.did(`Applied for ${type.name} out of station (${city}) ${iso(from)} to ${iso(to)}: “${reason}”`);
}

/** Excuse for a recent late arrival or early leave, within the monthly quota. */
async function askExemption(p, co, d, r) {
  const quota = (await p.get('Check my exemption quota', '/exemptions/quota')).data;
  if (!quota?.allowed || quota.used >= quota.allowed) return;
  const today = serverDay(d);
  const asked = new Set(((await p.get('Open my exemption requests', '/exemptions/my')).data ?? [])
    .filter((x) => x.status === 'Pending' || x.status === 'Approved').map((x) => x.date));
  const flagged = (await recentRecords(p, today)).filter((a) => a.date <= iso(today) && a.date >= iso(addDays(REAL_TODAY, -quota.withinDays)) && !asked.has(a.date)
    && a.date >= iso(monthStart(today)) && (['Late', 'HalfDay', 'ShortDay'].includes(a.status) || a.earlyLeaveMinutes > 0));
  const rec = flagged.at(-1);
  if (!rec) return;
  const reason = rec.status === 'Late'
    ? r.pick(['Stuck in traffic on the Canal Road after an accident.', 'Dropped my father at the hospital in the morning.', 'Heavy rain; the roads were flooded.'])
    : r.pick(['Left early for a doctor’s appointment.', 'Had to pick up my child from school; the van did not come.', 'Bank work that could only be done before 5 pm.']);
  const res = await p.post(`Ask to excuse ${rec.date} (${rec.status === 'Late' ? `${rec.lateMinutes} min late` : rec.status})`, '/exemptions', { date: rec.date, reason },
    { refuse: REFUSALS.exemption });
  if (res.ok) p.did(`Asked HR to excuse ${rec.date} (${rec.status === 'Late' ? `late by ${rec.lateMinutes} min` : rec.status === 'Present' ? 'left early' : rec.status}): “${reason}”`);
}

/** Different weekly days off for a few weeks. */
async function requestOffDays(p, co, d, r) {
  const base = d > REAL_TODAY ? d : REAL_TODAY;
  const from = addDays(base, 7 - ((base.getUTCDay() + 6) % 7)); // next Monday
  const to = addDays(from, 7 * r.int(2, 6) - 1);
  const usual = co.ref.defaultShift?.weeklyOffDays ?? ['Saturday', 'Sunday'];
  const options = [['Friday', 'Saturday'], ['Sunday', 'Monday'], ['Saturday'], ['Friday', 'Sunday']]
    .filter((o) => [...o].sort().join() !== [...usual].sort().join());
  const offDays = r.pick(options);
  const reason = r.pick(['Weekend classes for my MBA on Saturdays.', 'Covering the client’s Friday support window for a few weeks.', 'Family business needs me on Mondays.']);
  const res = await p.post(`Ask for ${offDays.join(' and ')} off ${iso(from)} to ${iso(to)}`, '/attendance-rules/off-days',
    { fromDate: iso(from), toDate: iso(to), offDays, reason }, { refuse: REFUSALS.offDay });
  if (res.ok) p.did(`Asked for ${offDays.join(' and ')} as days off from ${iso(from)} to ${iso(to)}: “${reason}”`);
}

/** Another shift for a few days, or a swap with a colleague on a different shift. */
async function requestShiftChange(p, co, d, r) {
  const base = d > REAL_TODAY ? d : REAL_TODAY;
  const mine = co.cal.get(p.employeeId)?.get(iso(d))?.defaultShiftId ?? co.ref.defaultShift?.id;
  const from = workdaysFrom(co, addDays(base, r.int(2, 10)), 1)[0];
  const to = workdaysFrom(co, from, r.int(1, 5)).at(-1);
  // A swap only makes sense with someone whose roster differs from theirs on one of those days.
  const slot = (id, key) => {
    const c = co.cal.get(id)?.get(key);
    return c ? `${c.isOff}|${c.shiftId ?? c.defaultShiftId}` : null;
  };
  const keys = Array.from({ length: (to - from) / 86_400_000 + 1 }, (_, i) => iso(addDays(from, i)));
  const colleagues = co.ref.staff.filter((e) => e.id !== p.employeeId && co.cal.has(e.id) && keys.some((k) => slot(e.id, k) && slot(p.employeeId, k) && slot(e.id, k) !== slot(p.employeeId, k)));
  if (colleagues.length && r.chance(0.3)) {
    const c = r.pick(colleagues);
    const reason = r.pick(['Have evening classes those days.', 'Need the mornings free for my mother’s physiotherapy.']);
    const res = await p.post(`Ask ${c.fullName} to swap shifts ${iso(from)} to ${iso(to)}`, '/attendance/shift-requests/swap',
      { colleagueId: c.id, from: iso(from), to: iso(to), reason }, { refuse: REFUSALS.shift });
    if (res.ok) p.did(`Asked ${c.fullName} to swap shifts from ${iso(from)} to ${iso(to)}: “${reason}”`);
    return;
  }
  // The shifts the roster already has them on in those days (the roster can differ from their usual shift).
  const current = new Set(keys.map((k) => co.cal.get(p.employeeId)?.get(k)).map((c) => c?.shiftId ?? c?.defaultShiftId ?? mine));
  const others = co.ref.shifts.filter((s) => !current.has(s.id) && s.id !== mine && s.isActive !== false);
  if (!others.length) return;
  const s = r.pick(others);
  const reason = r.pick(['Covering for a colleague on leave.', 'Support for the US client’s release window.', 'Doctor’s appointments in the mornings this week.']);
  const res = await p.post(`Ask to work the ${s.name} shift ${iso(from)} to ${iso(to)}`, '/attendance/shift-requests/change',
    { from: iso(from), to: iso(to), shiftId: s.id, reason }, { refuse: REFUSALS.shift });
  if (res.ok) p.did(`Asked to work the ${s.name} shift from ${iso(from)} to ${iso(to)}: “${reason}”`);
}

async function requestExpenseAdvance(p, co, d, r) {
  const mine = (await p.get('Open my expense advances', '/expense-setup/advances/my')).data ?? [];
  if (mine.some((a) => a.status === 'Pending' || (a.status === 'Approved' && !a.closedAtUtc))) return;
  const [purpose, amount] = r.pick([['Client visit to Karachi next week', 40000], ['Team offsite arrangements', 25000], ['Trade fair stall material', 30000],
    ['Field visit to the Multan site', 15000]]);
  const res = await p.post(`Ask for an expense advance: ${purpose}`, '/expense-setup/advances', { purpose, amount }, { refuse: REFUSALS.advance });
  if (res.ok) p.did(`Asked for an expense advance of Rs ${amount.toLocaleString('en-PK')} for “${purpose}”`);
}

/** A claim on one of the company's expense types, within what is left for them this month and year. */
async function claimTypedExpense(p, co, d, r) {
  const types = (await p.get('Open my expense types', '/expense-setup/types/for')).data ?? [];
  if (!types.length) return;
  const t = r.pick(types);
  const left = Math.min(t.perClaimLimit ?? Infinity, t.yearlyLimit != null ? t.yearlyLimit - t.usedThisYear : Infinity,
    t.monthlyLimit != null ? t.monthlyLimit - t.usedThisMonth : Infinity);
  if (left < 300) return;
  const today = serverDay(d);
  let day = addDays(today, -r.int(0, 5));
  if (day < monthStart(REAL_TODAY)) day = monthStart(REAL_TODAY);
  const item = { date: iso(day), category: t.category, expenseTypeId: t.id, description: '', amount: 0, receipt: null, quantity: null, fromCity: null, toCity: null };
  if (t.calculation === 'Distance') {
    const routes = ((await p.get('Open the distance list', '/expense-setup/routes')).data ?? []).filter((x) => (t.rate ?? 0) * x.km <= left);
    if (!routes.length) return;
    const route = r.pick(routes);
    Object.assign(item, { fromCity: route.fromCity, toCity: route.toCity, amount: Math.round((t.rate ?? 0) * route.km), description: `Drove ${route.fromCity} to ${route.toCity} for a client visit` });
  } else if (t.calculation === 'PerUnit') {
    const rate = t.rate ?? 0;
    const qty = rate ? Math.min(r.int(1, 4), Math.floor(left / rate)) : r.int(1, 3);
    if (qty < 1) return;
    Object.assign(item, { quantity: qty, amount: Math.round(rate * qty), description: `${qty} ${t.unit ?? 'unit'}${qty > 1 ? 's' : ''} of ${t.name.toLowerCase()}` });
  } else {
    const amount = Math.max(100, Math.floor(Math.min(left, r.int(1000, 8000)) / 50) * 50);
    Object.assign(item, { amount, description: `${t.name}, ${day.toLocaleString('en-GB', { month: 'long', timeZone: 'UTC' })}` });
  }
  if (t.receiptRequired || r.chance(0.4)) item.receipt = tinyReceipt;
  const open = r.chance(0.5) ? ((await p.get('Open my open advances', '/expense-setup/advances/open')).data ?? []) : [];
  const advanceId = open.find((a) => a.outstanding > 0)?.id ?? null;
  const res = await p.post(`Claim ${t.name}`, '/expenses', { title: t.name, items: [item], advanceId }, { refuse: REFUSALS.typedExpense });
  if (res.ok) p.did(`Claimed ${t.name}: ${item.description}, Rs ${Number(res.data.total).toLocaleString('en-PK')}${advanceId ? ' (against an advance)' : ''}`);
}

/** A return trip from Lahore with a hotel, giving the notice the company asks for. */
async function requestTravel(p, co, d, r) {
  const settings = (await p.get('Open travel', '/travel/me')).data;
  const base = d > REAL_TODAY ? d : REAL_TODAY;
  const depart = workdaysFrom(co, addDays(base, (settings?.minNoticeDays ?? 0) + r.int(1, 20)), 1)[0];
  const back = addDays(depart, r.int(1, 3));
  const to = r.pick(CITIES);
  const mode = to === 'Karachi' || to === 'Quetta' ? 'Air' : r.pick(['Air', 'Car', 'CompanyVehicle', 'Train']);
  const reason = co.ref.travelReasons.length ? r.pick(co.ref.travelReasons) : null;
  const purpose = r.pick([`Client meetings in ${to}`, `Requirement workshop at the ${to} office`, `Vendor visit in ${to}`, `Conference in ${to}`]);
  const res = await p.post(`Ask to travel to ${to} ${iso(depart)} to ${iso(back)}`, '/travel', {
    reasonId: reason?.id ?? null, purpose, tripType: 'Return', isInternational: false, returnOn: iso(back),
    legs: [{ from: 'Lahore', to, date: iso(depart), mode, preference: mode === 'Air' ? 'Morning flight, aisle seat' : null },
      { from: to, to: 'Lahore', date: iso(back), mode, preference: null }],
    urgentReason: null, needsHotel: true, hotelCity: to, checkIn: iso(depart), checkOut: iso(back), needsPickDrop: r.chance(0.5), pickDropNotes: 'Airport pick-up please.',
    estimatedCost: r.int(4, 12) * 10000, advanceRequested: r.chance(0.4) ? r.int(1, 4) * 5000 : null,
  }, { refuse: REFUSALS.travel });
  if (res.ok) p.did(`Asked to travel to ${to} (${mode}) ${iso(depart)} to ${iso(back)}: “${purpose}”`);
}

/** A new laptop, mouse or access card; now and then a return of something they hold. */
async function requestAsset(p, co, d, r) {
  if (!co.ref.assetTypes.length) return;
  const my = (await p.get('Open my assets', '/assets/my')).data;
  if (!my) return;
  const back = my.holding.filter((a) => !a.returnRequested);
  if (back.length && r.chance(0.15)) {
    const a = r.pick(back);
    const res = await p.post(`Ask to return ${a.tag}`, '/assets/requests/return', { assignmentId: a.id, reason: 'No longer needed; working from the office desktop now.' },
      { refuse: REFUSALS.asset });
    if (res.ok) p.did(`Asked to return ${a.assetName} (${a.tag})`);
    return;
  }
  const open = new Set(my.requests.filter((x) => x.kind === 'New' && (x.status === 'Pending' || x.status === 'Approved')).map((x) => x.typeId));
  const held = new Set(my.holding.map((a) => a.type));
  const choices = co.ref.assetTypes.filter((t) => !open.has(t.id) && (t.allowMultiple || !held.has(t.name)));
  if (!choices.length) return;
  const t = r.pick(choices);
  const reason = r.pick([`My ${t.name.toLowerCase()} is needed for the new project.`, `Lost my ${t.name.toLowerCase()} on the way home; reported to admin.`,
    `Starting on-site work at the client and need a ${t.name.toLowerCase()}.`]);
  const neededBy = workdaysFrom(co, addDays(serverDay(d), 3), 1)[0];
  const res = await p.post(`Ask for a ${t.name}`, '/assets/requests/new', { typeId: t.id, reason, neededBy: iso(neededBy), returnBy: null }, { refuse: REFUSALS.asset });
  if (res.ok) p.did(`Asked for a ${t.name.toLowerCase()} by ${iso(neededBy)}: “${reason}”`);
}

/** Checks in to today's desk if it needs a check-in, and books a free desk on a coming day. */
async function bookDesk(p, co, d, r) {
  if (!co.ref.floors.length) return;
  const mine = (await p.get('Open my desk bookings', '/desks/bookings/my')).data ?? [];
  const now = mine.find((b) => b.canCheckIn);
  if (now && iso(d) === iso(REAL_TODAY)) {
    const res = await p.post(`Check in at desk ${now.space}`, `/desks/bookings/${now.id}/check-in`, {}, { refuse: REFUSALS.desk });
    if (res.ok) p.did(`Checked in at desk ${now.space} (${now.floor})`);
  }
  const settings = (await p.get('Open desk settings', '/desks/settings')).data;
  const ahead = settings?.maxDaysAhead ?? 7;
  const base = d > REAL_TODAY ? d : REAL_TODAY;
  const day = workdaysFrom(co, addDays(base, r.int(1, Math.max(1, Math.min(ahead, 6)))), 1)[0];
  if ((day - REAL_TODAY) / 86_400_000 > ahead) return;
  if (mine.some((b) => b.date === iso(day) && b.kind === 'Desk' && (b.status === 'Booked' || b.status === 'CheckedIn'))) return;
  const floor = r.pick(co.ref.floors);
  const map = (await p.get(`Open the ${floor.name} desk map`, `/desks/floors/${floor.id}/day?date=${iso(day)}`)).data;
  const free = (map?.spaces ?? []).filter((s) => s.kind === 'Desk' && s.free);
  if (!free.length) return;
  const desk = r.pick(free);
  const res = await p.post(`Book desk ${desk.name} for ${iso(day)}`, '/desks/bookings', { spaceId: desk.id, date: iso(day), from: null, to: null, title: null, attendees: 1 },
    { refuse: REFUSALS.desk });
  if (res.ok) p.did(`Booked desk ${desk.name} on ${floor.name} for ${iso(day)}`);
}

/** Members of the provident fund now and then take out part of their vested balance. */
async function withdrawFund(p, co, d, r) {
  const f = (await p.get('Open my provident fund', '/funds/me')).data;
  if (!f?.pfMember || f.pfVested < 20000) return;
  const mine = (await p.get('Open my fund withdrawals', '/funds/withdrawals/my')).data ?? [];
  if (mine.some((w) => w.fund === 'ProvidentFund' && w.status === 'Pending')) return;
  const amount = Math.max(1000, Math.floor((f.pfVested * r.int(10, 30)) / 100 / 1000) * 1000);
  const reason = r.pick(['Daughter’s university admission fee.', 'Hajj expenses.', 'Construction of the house roof.']);
  const res = await p.post(`Withdraw Rs ${amount} from the provident fund`, '/funds/withdrawals', { fund: 'ProvidentFund', amount, reason, payWithSalary: true },
    { refuse: REFUSALS.fund });
  if (res.ok) p.did(`Asked to withdraw Rs ${amount.toLocaleString('en-PK')} from the provident fund: “${reason}”`);
}

async function requestProfileChange(p, co, d, r) {
  const mine = (await p.get('Open my profile change requests', '/employees/profile-changes/my')).data ?? [];
  if (mine.some((x) => x.status === 'Pending')) return;
  const [what, changes, reason] = r.pick([
    ['phone number', { phone: phoneNumber(r) }, 'Changed my mobile number.'],
    ['address', { address: `House ${r.int(1, 400)}, Street ${r.int(1, 30)}, ${r.pick(AREAS)}` }, 'Moved to a new house.'],
    ['bank account', { bankName: r.pick(BANKS), bankAccount: `PK${r.int(10, 99)}${r.pick(['MEZN', 'HABB', 'ALFH', 'MUCB'])}000${r.int(1000000000, 9999999999)}` }, 'Opened a salary account with a new bank.'],
    ['personal email', { personalEmail: `${p.email.split('@')[0]}.${r.int(10, 99)}@gmail.com` }, 'Please use my personal email for payslips.'],
  ]);
  const res = await p.post(`Ask to change my ${what}`, '/employees/profile-changes', { changes, reason }, { refuse: REFUSALS.profile });
  if (res.ok) p.did(`Asked HR to update their ${what}: “${reason}”`);
}

/** A scanned document added to their own employee file. */
async function uploadDocument(p, co, d, r) {
  const list = (await p.get('Open my documents', `/attachments/EmployeeDocument/${p.employeeId}`)).data;
  if (!list?.canAdd || list.items.length >= 6) return;
  const have = new Set(list.items.map((x) => x.category));
  const choices = [['Cnic', 'cnic-front', 'CNIC (front and back)'], ['Degree', 'bs-degree', 'Bachelor’s degree'], ['ExperienceLetter', 'experience-letter', 'Experience letter from the last employer'],
    ['Medical', 'medical-certificate', 'Medical fitness certificate'], ['Passport', 'passport', 'Passport bio page']].filter(([c]) => !have.has(c));
  if (!choices.length) return;
  const [category, file, note] = r.pick(choices);
  const f = new FormData();
  f.append('File', new Blob([Buffer.from(tinyReceipt.base64, 'base64')], { type: 'image/png' }), `${file}.png`);
  f.append('Category', category);
  if (category === 'Cnic' || category === 'Passport') f.append('ExpiresOn', iso(addDays(REAL_TODAY, r.int(400, 3000))));
  f.append('Note', note);
  const res = await p.req(`Upload my ${note}`, 'POST', `/attachments/EmployeeDocument/${p.employeeId}`, f, { refuse: REFUSALS.document });
  if (res.ok) p.did(`Uploaded their ${note} to their employee file`);
}

/** One person a day (at most) asks the AI assistant something short; it runs on a small free quota. */
function aiAskerOf(co, key) {
  const r = rng(`${co.name}|ai|${key}|${SEED}`);
  if (!r.chance(0.6)) return null;
  return r.pick(co.employees.filter((p) => p.employeeId).map((p) => p.email).sort()) ?? null;
}
async function askAi(p, co, d, r) {
  const status = (await p.get('Check the AI assistant', '/ai/status')).data;
  if (!status?.enabled || status.used >= status.limit) return;
  const q = r.pick(AI_QUESTIONS);
  const res = await p.post(`Ask the AI assistant: ${q}`, '/ai/ask', { messages: [{ role: 'user', text: q }], voice: false }, { refuse: REFUSALS.ai });
  if (res.ok) p.did(`Asked the AI assistant “${q}” and got: “${String(res.data?.reply ?? '').replace(/\s+/g, ' ').slice(0, 120)}”`);
}

const POLICY_QUESTIONS = ['Maternity leave kitni hai?', 'How many casual leaves do we get in a year?', 'Can I carry forward my annual leave?',
  'Work from home ki policy kya hai?', 'Sick leave ke liye medical certificate kab chahiye?', 'When is the next public holiday?', 'What is the notice period for resignation?'];

/** Asks a question about company policy; the answer comes only from the company's own policies and leave settings. */
async function askPolicy(p, co, d, r) {
  const q = r.pick(POLICY_QUESTIONS);
  const res = await p.post(`Ask about policies: ${q}`, '/ai/policy', { question: q, language: r.chance(0.2) ? 'ur' : 'en' });
  if (res.ok)
    p.did(`Asked “${q}”: ${res.data.found ? `answer from ${res.data.sources.map((s) => s.title).join(', ')}` : 'not in company policy, ask HR'}`);
}

const REPORT_QUESTIONS = ['Lahore ke kitne log is mahine late aaye', 'Who was absent last month?', 'Headcount by department', 'Leave taken this month',
  'Payroll totals last month', 'New joiners this year', 'Who left this year?', 'Pending approvals', 'Documents expiring soon', 'Is mahine kitne log late aaye'];

/** HR asks for a report in plain words; the server picks a whitelisted report and runs it. Sometimes runs one straight from the list. */
async function askReport(hr, co, d, r) {
  let res;
  if (r.chance(0.3)) {
    const list = (await hr.get('Open the report list', '/ai/reports')).data ?? [];
    if (!list.length) return;
    const pick = r.pick(list);
    res = await hr.post(`Run the ${pick.title} report`, '/ai/report', { report: pick.key });
  } else {
    const q = r.pick(REPORT_QUESTIONS);
    res = await hr.post(`Ask for a report: ${q}`, '/ai/report', { question: q });
  }
  if (res.ok) hr.did(`Report “${res.data.title}” (${res.data.params.from} to ${res.data.params.to}): ${res.data.rows.length} rows${res.data.usedAi ? ', picked by AI' : ''}`);
}

/** Their own part of a running performance review: goals, self review, sign-off. */
async function myReview(p, co, d, r) {
  const list = (await p.get('Open my performance reviews', '/performance/reviews/my')).data ?? [];
  for (const item of list.filter((x) => ['Goals', 'SelfReview', 'SignOff'].includes(x.stage))) {
    const rv = (await p.get('Open my review', `/performance/reviews/${item.id}`)).data;
    if (!rv?.isEmployee) continue;
    if (rv.stage === 'Goals') {
      const goals = [];
      rv.sections.forEach((s, i) => {
        if (s.kind !== 'Goals') return;
        const picks = r.shuffle(GOALS).slice(0, r.int(2, 4));
        const each = Math.floor(100 / picks.length / 5) * 5;
        picks.forEach(([title, target], j) => goals.push({ sectionIndex: i, title, description: null, target, weight: j === picks.length - 1 ? 100 - each * (picks.length - 1) : each }));
      });
      if (!goals.length) continue;
      const saved = await p.put(`Write my goals for ${rv.cycle}`, `/performance/reviews/${rv.id}/goals`, { goals }, { refuse: REFUSALS.performance });
      if (!saved.ok) continue;
      const sent = await p.post(`Send my goals for ${rv.cycle}`, `/performance/reviews/${rv.id}/goals/submit`, {}, { refuse: REFUSALS.performance });
      if (sent.ok) p.did(`Wrote ${goals.length} goals for ${rv.cycle} and sent them to their manager`);
    } else if (rv.stage === 'SelfReview') {
      const scores = rv.items.map((i) => ({ itemId: i.id, score: r.pick([3, 4, 4, 5, 3, 4]), comment: null }));
      const comment = r.pick(['Delivered the main goals; the documentation goal slipped because of the release crunch.', 'A good half; took on more client work than planned.']);
      const res = await p.put(`Send my self review for ${rv.cycle}`, `/performance/reviews/${rv.id}/self`, { scores, comment, submit: true }, { refuse: REFUSALS.performance });
      if (res.ok) p.did(`Sent their self review for ${rv.cycle}`);
    } else if (rv.stage === 'SignOff') {
      const agree = r.chance(0.85);
      const res = await p.post(`Sign off my ${rv.cycle} review`, `/performance/reviews/${rv.id}/signoff`,
        { agree, comment: agree ? 'Fair review, thank you.' : 'The delivery goal was met; the delay came from the client side.' }, { refuse: REFUSALS.performance });
      if (res.ok) p.did(`Signed off their ${rv.cycle} review (${agree ? 'agrees' : 'disagrees'}, rated ${rv.finalLabel ?? rv.finalRating})`);
    }
  }
}

/** A manager's part of the team's reviews: approve goals, write the manager review, check reviews as the manager's manager. */
async function teamReviews(m, co, d, r) {
  const list = (await m.get('Open the team’s reviews', '/performance/reviews/team')).data ?? [];
  for (const item of list.filter((x) => ['GoalsSubmitted', 'ManagerReview', 'FinalReview'].includes(x.stage)).slice(0, 4)) {
    const rv = (await m.get(`Open ${item.employeeName}'s review`, `/performance/reviews/${item.id}`)).data;
    if (!rv || rv.isEmployee) continue;
    if (rv.stage === 'GoalsSubmitted' && rv.isReviewer) {
      const approve = r.chance(0.85);
      const note = approve ? 'Good goals, approved.' : 'Please make the targets measurable (numbers and dates).';
      const res = await m.post(`${approve ? 'Approve' : 'Send back'} ${rv.employeeName}'s goals`, `/performance/reviews/${rv.id}/goals/decide`, { approve, note },
        { refuse: REFUSALS.performance });
      if (res.ok) m.did(`${approve ? 'Approved' : 'Sent back'} ${rv.employeeName}'s goals for ${rv.cycle}: “${note}”`);
    } else if (rv.stage === 'ManagerReview' && rv.isReviewer) {
      const scores = rv.items.map((i) => ({ itemId: i.id, score: r.pick([3, 3, 4, 4, 5, 2]), comment: null }));
      const res = await m.put(`Write ${rv.employeeName}'s manager review`, `/performance/reviews/${rv.id}/manager`, {
        scores, comment: 'Reliable and improving; needs to speak up more in design discussions.', strengths: 'Ownership, quality of work',
        improvements: 'Estimation and stakeholder updates', developmentPlan: 'Lead one feature end to end next half', potential: r.pick([1, 2, 2, 3]), submit: true,
      }, { refuse: REFUSALS.performance });
      if (res.ok) m.did(`Wrote and sent ${rv.employeeName}'s manager review for ${rv.cycle}`);
    } else if (rv.stage === 'FinalReview' && rv.isFinalReviewer) {
      const approve = r.chance(0.9);
      const res = await m.post(`Check ${rv.employeeName}'s review`, `/performance/reviews/${rv.id}/final`,
        { approve, comment: approve ? 'Agree with the rating.' : 'The score looks high against the goals; please revisit.' }, { refuse: REFUSALS.performance });
      if (res.ok) m.did(`${approve ? 'Agreed with' : 'Sent back'} the manager review of ${rv.employeeName}`);
    }
  }
}

/** Ticks off onboarding tasks assigned to them (HR also gets the tasks for HR). */
async function onboardingTasks(p, co, d, r) {
  const tasks = (await p.get('Open my onboarding tasks', '/onboarding/my-tasks')).data ?? [];
  const due = tasks.filter((t) => t.canDo && t.dueOn <= iso(addDays(REAL_TODAY, 3))).slice(0, 3);
  for (const t of due) {
    const res = await p.post(`Tick off onboarding task “${t.title}”`, `/onboarding/tasks/${t.id}`, { done: true, note: r.chance(0.3) ? 'Done.' : null },
      { refuse: REFUSALS.onboarding });
    if (res.ok) p.did(`Ticked off “${t.title}” in ${t.forEmployee ?? 'a new joiner'}'s onboarding`);
  }
}

/** Swaps colleagues asked for, trips that need settling, the exit interview, a new joiner's introduction. */
async function followUps(p, co, d, r) {
  const swaps = ((await p.get('Open my shift requests', '/attendance/shift-requests/my')).data ?? [])
    .filter((x) => x.kind === 'Swap' && x.status === 'AwaitingColleague' && x.swapWithEmployeeId === p.employeeId);
  for (const s of swaps) {
    const accept = r.chance(0.8);
    const res = await p.post(`${accept ? 'Agree to' : 'Decline'} ${s.employeeName}'s shift swap`, `/attendance/shift-requests/${s.id}/${accept ? 'accept' : 'decline'}`, {},
      { refuse: REFUSALS.shift });
    if (res.ok) p.did(`${accept ? 'Agreed to' : 'Declined'} ${s.employeeName}'s shift swap (${s.fromDate} to ${s.toDate})`);
  }
  const trips = ((await p.get('Open my trips', '/travel/my')).data ?? []).filter((t) => (t.status === 'Approved' || t.status === 'Booked')
    && (t.settlement === 'None' || t.settlement === 'Returned') && t.returnOn <= iso(REAL_TODAY));
  for (const t of trips.slice(0, 1)) {
    const days = (parseDate(t.returnOn) - parseDate(t.departOn)) / 86_400_000 + 1;
    const expenses = [
      { date: t.departOn, category: 'Meals', description: 'Meals during the trip', amount: r.int(3, 9) * 1000 },
      { date: t.returnOn, category: 'Travel', description: 'Taxi to and from the airport', amount: r.int(2, 6) * 1000 },
    ];
    const res = await p.post(`Settle my trip to ${t.destination}`, `/travel/${t.id}/settlement`, { expenses, perDiemDays: days, note: null }, { refuse: REFUSALS.travel });
    if (res.ok) p.did(`Sent the settlement for the ${t.destination} trip (${days} days' allowance, Rs ${expenses.reduce((s, x) => s + x.amount, 0).toLocaleString('en-PK')} spent)`);
  }
  const exit = (await p.get('Open my exit', '/separations/my')).data;
  if (exit?.summary?.status === 'InProgress' && !exit.exitInterviewAtUtc) {
    const rating = r.pick([3, 4, 4, 5]);
    const res = await p.put('Fill in the exit interview', `/separations/${exit.summary.id}/exit-interview`, {
      exitReason: r.pick(['Better opportunity', 'Higher studies', 'Relocation']), rating, wouldRecommend: rating >= 4,
      feedback: 'Good team and learning; salary reviews could be more regular.',
    }, { refuse: REFUSALS.separation });
    if (res.ok) p.did(`Filled in the exit interview (${rating}/5)`);
  }
  if (p.joinedOn && p.joinedOn >= iso(addDays(REAL_TODAY, -30))) {
    const welcome = (await p.get('Open the welcome page', '/onboarding/welcome')).data;
    if (welcome?.onboarding && !welcome.onboarding.summary.introduced) {
      const res = await p.post('Introduce myself to the team', '/onboarding/introduce', {
        answers: { about: 'Happy to be here; I work mostly on backend services.', previous: r.pick(['Arbisoft', 'NetSol Technologies', 'FAST NUCES']), hobbies: r.pick(['Cricket and hiking', 'Reading and cooking', 'Photography']), funFact: null },
      }, { refuse: REFUSALS.onboarding });
      if (res.ok) p.did('Introduced themselves to the team on the welcome page');
    }
  }
}

/** Very rarely someone resigns (never the HR manager, the line manager or the helpdesk agent: the demo keeps its cast). */
async function resign(p, co, d, r) {
  if (p === co.hr || p === co.mgr || p.role !== 'employee' || co.agents.includes(p)) return;
  const mine = (await p.get('Open my exit', '/separations/my')).data;
  if (mine && ['Requested', 'InProgress'].includes(mine.summary?.status)) return;
  const last = workdaysFrom(co, addDays(serverDay(d), 30), 1)[0];
  const reason = r.pick(['Got an offer abroad.', 'Moving to Islamabad with my family.', 'Going for a master’s degree.']);
  const res = await p.post('Resign', '/separations/resign', { proposedLastDay: iso(last), reason }, { refuse: REFUSALS.separation });
  if (res.ok) p.did(`Resigned, proposing ${iso(last)} as the last working day: “${reason}”`);
}

// ---------- HR: the other modules ----------

/** Seeds a couple of loan types and an expense type if the company has none, as HR would before opening them up. */
async function hrSetup(hr, co, d, r) {
  if (!co.ref.loanTypes.length) {
    for (const t of [{ name: 'Personal loan', maxAmount: 300000, maxInstallments: 12, markupPercent: 0, processingFee: 0, minServiceMonths: 6, isActive: true },
      { name: 'Car loan', maxAmount: 1500000, maxInstallments: 36, markupPercent: 5, processingFee: 2000, minServiceMonths: 12, isActive: true }]) {
      const res = await hr.post(`Add the ${t.name} loan type`, '/pay/loan-types', t);
      if (res.ok) hr.did(`Added the ${t.name} loan type (up to Rs ${t.maxAmount.toLocaleString('en-PK')}, ${t.maxInstallments} installments)`);
    }
    co.ref.loanTypes = ((await hr.get('Open loan types', '/pay/loan-types')).data ?? []).filter((t) => t.isActive);
  }
  const types = (await hr.get('Open expense types', '/expense-setup/types')).data;
  if (types && !types.length) {
    const res = await hr.post('Add the Mobile bill expense type', '/expense-setup/types', {
      name: 'Mobile bill', category: 'Mobile', calculation: 'Amount', unit: null, rate: null, perClaimLimit: 5000, yearlyLimit: 36000, receiptRequired: false,
      gradeIds: [], isActive: true, gradeRates: [], monthlyLimit: 3000,
    });
    if (res.ok) hr.did('Added the Mobile bill expense type (Rs 3,000 a month)');
  }
}

/** Runs a few reports from the reports page. */
async function runReports(hr, co, d, r) {
  if (!co.ref.reports.length) return;
  const today = serverDay(d);
  const last = addDays(monthStart(today), -1);
  for (const def of r.shuffle(co.ref.reports).slice(0, r.int(1, 3))) {
    const payroll = def.key.startsWith('payroll.');
    const m = payroll ? last : today;
    const needEmployee = def.filters.includes('employee') && (['employees.profile', 'payroll.taxcertificate'].includes(def.key) || r.chance(0.25));
    const who = needEmployee ? r.pick(co.ref.staff) : null;
    const year = def.key === 'payroll.taxcertificate' ? today.getUTCFullYear() + (today.getUTCMonth() >= 6 ? 1 : 0) : m.getUTCFullYear();
    const res = await hr.post(`Run the ${def.name} report`, `/reports/${def.key}`, {
      from: iso(monthStart(m)), to: iso(payroll ? last : today), date: iso(today), year, month: m.getUTCMonth() + 1, departmentIds: [], employeeId: who?.id ?? null,
      includeLeft: false, groupIds: null,
    });
    if (res.ok) hr.did(`Ran the “${def.name}” report${who ? ` for ${who.fullName}` : ''} (${res.data?.rows?.length ?? 0} rows)`);
  }
}

async function previewScheduledReport(hr, co, d, r) {
  const list = (await hr.get('Open scheduled reports', '/scheduled/reports')).data ?? [];
  if (list.length && r.chance(0.7)) {
    const s = r.pick(list);
    const res = await hr.get(`Preview the scheduled report “${s.name}”`, `/scheduled/reports/${s.id}/preview`);
    if (res.ok) hr.did(`Previewed the scheduled report “${s.name}” (${res.data?.rows?.length ?? 0} rows)`);
    return;
  }
  const kind = r.pick(['Headcount', 'DailyAttendance', 'OnLeave', 'PendingApprovals', 'ExpiringDocuments', 'JoinersAndLeavers', 'ProbationDue']);
  const res = await hr.post(`Preview a ${kind} report before scheduling it`, '/scheduled/reports/preview', {
    name: '', kind, frequency: 'Weekly', day: 1, sendHour: 9, recipientIds: [], extraEmails: '', departmentIds: [], isActive: true,
  });
  if (res.ok) hr.did(`Previewed a weekly ${kind} report before scheduling it (${res.data?.rows?.length ?? 0} rows)`);
}

/** Launches a quarterly cycle when none is running, and calibrates and releases reviews that reached HR. */
async function performanceCycle(hr, co, d, r) {
  const cycles = (await hr.get('Open review cycles', '/performance/cycles')).data;
  if (!cycles) return;
  const active = cycles.filter((c) => c.status === 'Active');
  const today = serverDay(d);
  if (!active.length) {
    const q = Math.floor(today.getUTCMonth() / 3);
    const name = `Q${q + 1} ${today.getUTCFullYear()} review`;
    const templates = (await hr.get('Open review templates', '/performance/templates')).data ?? [];
    if (!cycles.some((c) => c.name === name) && templates.length) {
      const from = new Date(Date.UTC(today.getUTCFullYear(), q * 3, 1));
      const to = addDays(new Date(Date.UTC(today.getUTCFullYear(), q * 3 + 3, 1)), -1);
      const made = await hr.post(`Create the ${name} cycle`, '/performance/cycles', {
        name, periodFrom: iso(from), periodTo: iso(to), templateId: templates[0].id, departmentIds: [],
        goalsDue: iso(addDays(today, 7)), selfReviewDue: iso(addDays(today, 30)), managerReviewDue: iso(addDays(today, 45)), finalReview: false,
      }, { refuse: REFUSALS.performance });
      const cycle = made.ok ? ((await hr.get('Open review cycles', '/performance/cycles')).data ?? []).find((c) => c.name === name) : null;
      if (cycle) {
        const res = await hr.post(`Launch the ${name} cycle`, `/performance/cycles/${cycle.id}/launch`, {}, { refuse: REFUSALS.performance });
        if (res.ok) hr.did(`Launched the ${name} review cycle for ${res.data?.reviews ?? 0} people`);
      }
    }
  }
  for (const c of active.filter((x) => x.byStage?.Calibration)) {
    const cal = (await hr.get(`Open calibration for ${c.name}`, `/performance/cycles/${c.id}/calibration`)).data;
    const ids = [];
    for (const rv of (cal?.reviews ?? []).filter((x) => x.stage === 'Calibration' && x.employeeId !== hr.employeeId).slice(0, 5)) {
      const base = Math.round(rv.managerScore ?? rv.finalRating ?? 3);
      const finalRating = Math.min(5, Math.max(1, base + (r.chance(0.2) ? r.pick([-1, 1]) : 0)));
      const graceMarks = r.pick([0, 0, 0, 0.25, 0.5]);
      const res = await hr.post(`Calibrate ${rv.employeeName}'s review`, `/performance/reviews/${rv.id}/calibrate`,
        { finalRating, note: finalRating !== base ? 'Adjusted against the bell curve across teams.' : null, graceMarks }, { refuse: REFUSALS.performance });
      if (res.ok) {
        ids.push(rv.id);
        hr.did(`Calibrated ${rv.employeeName}'s ${c.name} review at ${finalRating}${graceMarks ? ` with ${graceMarks} grace marks` : ''}`);
      }
    }
    if (ids.length) {
      const res = await hr.post(`Release ${ids.length} reviews of ${c.name}`, `/performance/cycles/${c.id}/release`, { reviewIds: ids }, { refuse: REFUSALS.performance });
      if (res.ok) hr.did(`Released ${res.data?.released ?? ids.length} ${c.name} reviews to the employees`);
    }
  }
}

/** The travel desk: books approved trips, pays requested advances, settles returned trips. */
async function travelDesk(hr, co, d, r) {
  const mine = (t) => t.employeeId === hr.employeeId;
  for (const t of ((await hr.get('Open trips to book', '/travel/all?filter=book')).data ?? []).filter((x) => !mine(x)).slice(0, 2)) {
    const trip = (await hr.get(`Open ${t.employeeName}'s trip`, `/travel/${t.id}`)).data;
    if (!trip) continue;
    const nights = Math.max(1, (parseDate(trip.returnOn) - parseDate(trip.departOn)) / 86_400_000);
    const legs = trip.legs.map((l) => ({ legId: l.id, bookingRef: `${l.mode === 'Air' ? 'PK' : 'BK'}-${r.int(100000, 999999)}`, bookingCost: (l.mode === 'Air' ? r.int(18, 32) : r.int(3, 9)) * 1000 }));
    const hotel = trip.needsHotel ? r.pick(['Pearl Continental', 'Avari Hotel', 'Hotel One', 'Ramada']) : null;
    const res = await hr.post(`Book ${t.employeeName}'s trip to ${t.destination}`, `/travel/${t.id}/booking`, {
      legs, hotelName: hotel, hotelRef: hotel ? `HTL-${r.int(1000, 9999)}` : null, hotelCost: hotel ? nights * r.int(12, 22) * 1000 : null, notes: 'Tickets emailed to the traveller.',
    }, { refuse: REFUSALS.travel });
    if (res.ok) hr.did(`Booked ${t.employeeName}'s trip to ${t.destination}${hotel ? ` with ${hotel}` : ''}`);
  }
  for (const t of ((await hr.get('Open trips waiting for an advance', '/travel/all?filter=advance')).data ?? []).filter((x) => !mine(x)).slice(0, 2)) {
    const res = await hr.post(`Pay ${t.employeeName}'s travel advance`, `/travel/${t.id}/advance`, { amount: t.advanceRequested, paidOn: iso(REAL_TODAY), via: 'Bank transfer' },
      { refuse: REFUSALS.travel });
    if (res.ok) hr.did(`Paid ${t.employeeName} a travel advance of Rs ${Number(t.advanceRequested).toLocaleString('en-PK')}`);
  }
  for (const t of ((await hr.get('Open settlements to check', '/travel/all?filter=settle')).data ?? []).filter((x) => !mine(x)).slice(0, 2)) {
    const approve = r.chance(0.85);
    const note = approve ? 'Checked against the receipts.' : 'Please attach the hotel invoice and split the taxi fares.';
    const res = await hr.post(`${approve ? 'Approve' : 'Return'} ${t.employeeName}'s travel settlement`, `/travel/${t.id}/settlement/${approve ? 'approve' : 'return'}`, { note },
      { refuse: REFUSALS.travel });
    if (res.ok) hr.did(`${approve ? 'Approved' : 'Sent back'} ${t.employeeName}'s settlement for ${t.destination}: “${note}”`);
  }
}

/** Hands over approved asset requests from stock and takes back returns. */
async function fulfilAssets(hr, co, d, r) {
  const list = ((await hr.get('Open asset requests to fulfil', '/assets/requests')).data ?? []).filter((x) => x.canFulfill).slice(0, 3);
  for (const q of list) {
    if (q.kind === 'Return') {
      const res = await hr.post(`Take back ${q.assetLabel ?? q.type} from ${q.employeeName}`, `/assets/requests/${q.id}/fulfill`,
        { assetId: null, on: iso(REAL_TODAY), condition: 'Good', notes: 'Received back in working order.' }, { refuse: REFUSALS.asset });
      if (res.ok) hr.did(`Took back ${q.assetLabel ?? q.type} from ${q.employeeName}`);
      continue;
    }
    const stock = (await hr.get(`Find a ${q.type} in stock`, `/assets?status=Available&typeId=${q.typeId}`)).data ?? [];
    if (!stock.length) {
      hr.did(`No ${q.type.toLowerCase()} in stock for ${q.employeeName}; asked admin to buy one`);
      continue;
    }
    const a = stock[0];
    const res = await hr.post(`Hand over ${a.tag} to ${q.employeeName}`, `/assets/requests/${q.id}/fulfill`,
      { assetId: a.id, on: iso(REAL_TODAY), condition: a.condition === 'New' ? 'New' : 'Good', notes: 'Handed over at the admin desk.' }, { refuse: REFUSALS.asset });
    if (res.ok) hr.did(`Handed over ${a.name} (${a.tag}) to ${q.employeeName}`);
  }
}

/** Who is in the office, the month's trends and how much space documents take. */
async function lookAround(hr, co, d) {
  const inOffice = (await hr.get('See who is in the office', `/desks/in-office?date=${iso(serverDay(d))}`)).data;
  const trends = (await hr.get('Open attendance trends', '/dashboard/trends')).data;
  const docs = (await hr.get('Open document settings', '/attachments/settings')).data;
  if (inOffice || trends || docs)
    hr.did(`Looked at who is in (${inOffice?.length ?? 0} desks booked today), the attendance trends and document storage (${Math.round((docs?.totalBytes ?? 0) / 1024)} KB used)`);
}

/** AI Team: runs one robot a day (in turn), approves or rejects what it proposes and reads the activity log. */
async function aiTeam(hr, co, d, r) {
  if (r.chance(0.4)) {
    const kind = r.pick(['announcement', 'job_ad', 'letter', 'policy', 'email']);
    const w = await hr.post(`Write a ${kind.replace('_', ' ')} with AI`, '/ai/write',
      { kind, brief: `${co.name}: ${r.pick(['office timings change from next month', 'team lunch on Friday', 'hiring a sales executive in Lahore', 'experience letter for a leaving employee', 'remind staff to submit timesheets'])}`, tone: 'Professional and friendly', language: r.chance(0.2) ? 'ur' : 'en' });
    if (w.ok) hr.did(`AI wrote: ${(w.data.title ?? w.data.text).slice(0, 60)}`);
  }
  const team = (await hr.get('Open AI Team', '/ai-team')).data;
  if (!team?.robots?.length || team.stopped) return;
  const robot = team.robots[Math.floor(serverDay(d).getTime() / 86_400_000) % team.robots.length];
  if (robot.canPropose && robot.autonomy === 'Advisory')
    await hr.put(`Let ${robot.name} propose changes`, `/ai-team/${robot.id}`,
      { enabled: robot.enabled, autonomy: 'Supervised', ownerUserId: robot.ownerUserId, runHour: robot.runHour, runsPerMonth: robot.runsPerMonth });
  const run = await hr.post(`Run ${robot.name}`, `/ai-team/${robot.id}/run`, {});
  if (run.ok) hr.did(`Ran ${robot.name}: ${run.data.summary}`);
  const waiting = (await hr.get('Open the robots’ approval inbox', '/ai-team/proposals')).data ?? [];
  for (const p of waiting.slice(0, 3)) {
    const approve = r.chance(0.8);
    const res = await hr.post(`${approve ? 'Approve' : 'Reject'}: ${p.title}`, `/ai-team/proposals/${p.id}/decide`,
      { approve, note: approve ? null : 'Not needed this time.', body: null });
    if (res.ok) hr.did(`${approve ? 'Approved' : 'Rejected'} the robot's proposal: ${p.title}`);
  }
  await hr.get('Read the AI Team activity log', '/ai-team/runs');
}

/** Accepts resignations waiting for HR. */
async function handleResignations(hr, co, d, r) {
  const list = ((await hr.get('Open resignations', '/separations?status=Requested')).data ?? []).filter((s) => s.employeeId !== hr.employeeId);
  for (const s of list.slice(0, 2)) {
    const res = await hr.post(`Accept ${s.employeeName}'s resignation`, `/separations/${s.id}/accept`,
      { lastWorkingDay: s.proposedLastDay, noticePeriodDays: 30, note: 'Accepted after a talk with the line manager. The clearance checklist follows.' }, { refuse: REFUSALS.separation });
    if (res.ok) hr.did(`Accepted ${s.employeeName}'s resignation; last working day ${s.proposedLastDay}`);
  }
}

/** The other modules, per role: [task id, daily chance, label, action]. Kept rare: a 10–25 person company sees a few of each a week. */
const EMPLOYEE_MODULES = [
  ['pay-request', 0.04, 'Claim overtime or ask for an advance', payRequest],
  ['loan', 0.012, 'Apply for a loan', requestLoan],
  ['comp-off', 0.05, 'Claim comp-off for a day off worked', claimCompOff],
  ['station-leave', 0.015, 'Apply for leave out of station', applyStationLeave],
  ['exemption', 0.08, 'Ask to excuse a late arrival or early leave', askExemption],
  ['off-days', 0.012, 'Ask for different days off', requestOffDays],
  ['shift-change', 0.025, 'Ask for a shift change or swap', requestShiftChange],
  ['expense-advance', 0.015, 'Ask for an expense advance', requestExpenseAdvance],
  ['typed-expense', 0.05, 'Claim a typed expense', claimTypedExpense],
  ['travel', 0.02, 'Ask to travel', requestTravel],
  ['asset', 0.025, 'Ask for an asset', requestAsset],
  ['desk', 0.08, 'Book a desk', bookDesk],
  ['fund', 0.008, 'Withdraw from the provident fund', withdrawFund],
  ['profile-change', 0.015, 'Ask to update my profile', requestProfileChange],
  ['document', 0.025, 'Upload a document to my file', uploadDocument],
  ['my-review', 0.25, 'My performance review', myReview],
  ['follow-ups', 0.35, 'Swaps, trip settlements and other follow-ups', followUps],
  ['policy-question', 0.04, 'Ask about company policy', askPolicy],
  ['resign', 0.0006, 'Resign', resign],
];
const MANAGER_MODULES = [
  ['team-reviews', 0.4, 'The team’s performance reviews', teamReviews],
  ['my-review', 0.2, 'My performance review', myReview],
  ['onboarding', 0.3, 'Onboarding tasks', onboardingTasks],
  ['desk', 0.05, 'Book a desk', bookDesk],
];
const HR_MODULES = [
  ['setup-types', 0.3, 'Loan and expense types', hrSetup],
  ['reports', 0.25, 'Run reports', runReports],
  ['scheduled-report', 0.12, 'Preview a scheduled report', previewScheduledReport],
  ['performance', 0.25, 'Review cycle: launch, calibrate, release', performanceCycle],
  ['team-reviews', 0.3, 'The team’s performance reviews', teamReviews],
  ['my-review', 0.2, 'My performance review', myReview],
  ['onboarding', 0.5, 'Onboarding tasks for HR', onboardingTasks],
  ['travel-desk', 0.4, 'Travel desk: bookings, advances, settlements', travelDesk],
  ['assets', 0.4, 'Hand over and take back assets', fulfilAssets],
  ['look-around', 0.3, 'Who is in, trends and document storage', lookAround],
  ['resignations', 0.3, 'Resignations waiting for HR', handleResignations],
  ['ai-team', 0.6, 'AI Team: run a robot and decide its proposals', aiTeam],
  ['ai-report', 0.3, 'Ask for a report in plain words', askReport],
  ['policy-question', 0.1, 'Ask about company policy', askPolicy],
];

/** Backfill: the same modules played straight through for a past day. */
async function moreModules(p, co, d, r, modules) {
  for (const [, chance, label, fn] of modules) {
    if (!r.chance(chance)) continue;
    try {
      await fn(p, co, d, r);
    } catch (e) {
      record({ ...p.where(), persona: p.label, kind: 'failed', intent: label, method: '-', path: '-', query: '', status: 'script error', title: e.message, body: String(e.stack).slice(0, 400) });
    }
  }
  if (modules === EMPLOYEE_MODULES && aiAskerOf(co, iso(d)) === p.email) await askAi(p, co, d, r);
}

// ---------- Backfill: a whole past day at once ----------

async function simulateDay(co, d) {
  const r = rng(`${co.name}|${iso(d)}|${SEED}`);
  co.day = iso(d);
  const workday = !isOff(co, d);
  const kind = workday ? 'working day' : co.ref.holidays.has(iso(d)) ? 'holiday' : 'weekly off';
  console.log(`  ${co.name}, ${iso(d)} (${WEEKDAYS[d.getUTCDay()]}, ${kind}, backfill)`);

  // Everyone with a login who came in that day; past days are worked through fix requests, not live punches.
  const staff = co.employees.filter((p) => !p.joinedOn || p.joinedOn <= iso(d));
  const present = workday ? staff.filter(() => r.chance(0.9)) : [];
  const base = { live: false, workday, weekendWorker: false, submitDay: workday && (d.getUTCDay() === 5 || isOff(co, addDays(d, 1))) };
  await hrDay(co.hr, co, d, r, base);
  for (const p of r.shuffle(present)) await employeeDay(p, co, d, r, base);
  for (const a of co.agents) {
    if (!a.token) await a.signIn();
    if (!a.token || !workday) continue;
    await attendance(a, co, d, r, base);
    await answerTickets(a, co, r, { onlyCategories: co.ref.helpdesk.filter((c) => c.agentIds?.includes(a.employeeId)).map((c) => c.id) });
    await social(a, r);
  }
  await managerDay(co.mgr, co, d, r, base);
  if (workday) {
    if (co.mgr?.token) await workInbox(co.mgr, r);
    await workInbox(co.hr, r, { hr: true });
  }
}

// ---------- Working hours: everyone's plan for a day ----------

const cast = (co) => [co.hr, co.mgr, ...co.employees, ...co.agents].filter(Boolean);
const isWorkday = (cell) => !!cell && !cell.isOff && !cell.isHoliday;
const clampMinute = (m) => Math.max(0, Math.min(1439, Math.round(m)));
const dayStartMs = (key) => Date.parse(`${key}T00:00:00+05:00`);

/** Each person's shift, off days and holidays per date, read as HR from the roster, as the Roster screen shows them. */
async function loadCalendar(co, from, to) {
  co.cal = new Map();
  co.ref.shiftById = new Map(co.ref.shifts.map((x) => [x.id, x]));
  for (let f = from; f <= to; f = addDays(f, 42)) {
    const t = addDays(f, 41) < to ? addDays(f, 41) : to;
    const roster = (await co.hr.get('Open the roster', `/attendance/roster?from=${iso(f)}&to=${iso(t)}`)).data;
    for (const row of roster?.rows ?? []) {
      const days = co.cal.get(row.employeeId) ?? new Map();
      for (const c of row.days) days.set(c.date, { ...c, defaultShiftId: row.defaultShiftId });
      co.cal.set(row.employeeId, days);
    }
  }
}

/** On a day off, now and then one or two employees come in for urgent work. */
function weekendCrew(co, key) {
  const r = rng(`${co.name}|weekend|${key}|${SEED}`);
  if (!r.chance(0.45)) return [];
  const pool = co.employees.filter((p) => p.employeeId && co.cal.get(p.employeeId)?.has(key)).map((p) => p.email).sort();
  return r.shuffle(pool).slice(0, r.int(1, 2));
}

/**
 * One person's day: status, punch times and the work they do, as a list of actions at minutes of the day.
 * Worked out from the roster and a random stream seeded by company + person + date, so every run gets the same plan.
 */
function planDay(co, p, d) {
  const key = iso(d);
  p.plans ??= {};
  if (p.plans[key]) return p.plans[key];
  const plan = (p.plans[key] = { date: key, status: 'none', actions: [] });
  const cell = p.employeeId && co.cal.get(p.employeeId)?.get(key);
  // The roster lists anyone who joins within the range it shows, so days before joining are skipped here.
  const joined = co.ref.staff.find((e) => e.id === p.employeeId)?.joiningDate ?? p.joinedOn;
  if (!cell || (joined && key < joined)) return plan;
  const r = rng(`${co.name}|${p.email}|${key}|${SEED}`);
  const shift = co.ref.shiftById.get(cell.shiftId) ?? co.ref.shiftById.get(cell.defaultShiftId) ?? co.ref.defaultShift;
  const start = minutesOf(shift?.startTime) ?? 540;
  let end = minutesOf(shift?.endTime) ?? 1080;
  if (end <= start) end = 1435; // a shift past midnight ends at midnight here
  Object.assign(plan, { shift: shift?.name, start, end });
  const task = (id, at, label, run) => plan.actions.push({ id, at: clampMinute(at), label, kind: 'task', run });
  const punch = (id, at, label, extra = {}) => plan.actions.push({ id, at: clampMinute(at), label, kind: 'punch', ...extra });

  if (!isWorkday(cell)) {
    plan.status = cell.isHoliday ? 'holiday' : 'off';
    if (p.role !== 'employee' || !weekendCrew(co, key).includes(p.email)) return plan;
    plan.status = 'weekend work';
    const arrive = r.int(600, 690);
    const leave = arrive + r.int(180, 300);
    Object.assign(plan, { arrive, leave });
    punch('in', arrive, 'Check in for weekend work');
    punch('out', leave, 'Check out');
    task('social', arrive + r.int(5, 30), 'Catch up on announcements and notifications', (p, co, d, r) => social(p, r));
    if (r.chance(0.3)) task('ticket', arrive + r.int(40, 150), 'Raise a helpdesk ticket', (p, co, d, r) => raiseTicket(p, co, r));
    return plan;
  }

  const roll = r.next();
  if (roll < 0.03) {
    plan.status = 'sick';
    task('sick', start - r.int(20, 75), 'Call in sick', (p, co, d, r) => applySickToday(p, co, d, r));
    return plan;
  }
  if (roll < 0.06) {
    plan.status = 'absent';
    return plan;
  }
  plan.status = 'present';
  const earliest = shift?.earliestCheckInMinutes != null ? start - shift.earliestCheckInMinutes : 0;
  const late = r.chance(0.12);
  const arrive = Math.max(earliest, late ? start + r.int(16, 75) : start + r.int(-25, 12));
  const o = r.next();
  let leave = o < 0.15 ? end + r.int(30, 100) : o < 0.2 ? end - r.int(20, 60) : end + r.int(-8, 25);
  leave = Math.min(Math.max(leave, arrive + 240), 1435);
  const forgetsCheckOut = r.chance(0.04);
  Object.assign(plan, { arrive, leave, late, forgetsCheckOut, breaks: [] });
  punch('in', arrive, 'Check in');

  const wanted = [];
  if (r.chance(0.7)) {
    const b = start + Math.floor((end - start) / 2) - 45 + r.int(-20, 20);
    wanted.push([r.chance(0.5) ? 'Lunch' : 'Prayer', b, b + Math.max(15, (shift?.breakMinutes || 40) + r.int(-10, 15))]);
  }
  if (r.chance(0.3)) {
    const b = end - 110 + r.int(-15, 15);
    wanted.push(['Prayer', b, b + r.int(10, 20)]);
  }
  if (r.chance(0.2)) {
    const b = arrive + 120 + r.int(0, 40);
    wanted.push(['Tea', b, b + r.int(10, 20)]);
  }
  let free = arrive + 30;
  for (const [kind, from, to] of wanted.sort((x, y) => x[1] - y[1])) {
    if (from < free || to > leave - 20) continue;
    const i = plan.breaks.length;
    plan.breaks.push({ kind, from, to });
    punch(`break${i}-start`, from, `Start a ${kind.toLowerCase()} break`, { breakKind: kind, endsAt: to });
    punch(`break${i}-end`, to, `End the ${kind.toLowerCase()} break`, { breakKind: kind });
    free = to + 10;
  }
  if (!forgetsCheckOut) punch('out', leave, 'Check out');

  const at = (a = 0.05, b = 0.95) => arrive + Math.round((leave - arrive) * (a + r.next() * (b - a)));
  task('social', arrive + r.int(3, 30), 'Announcements, polls and notifications', (p, co, d, r) => social(p, r));
  task('fix', arrive + r.int(10, 90), 'Ask to fix earlier days', (p, co, d, r) => fixEarlierDays(p, co, d, r));
  if (r.chance(0.5)) task('social-2', at(0.5, 0.7), 'Notifications after lunch', (p, co, d, r) => social(p, r));

  if (p.role === 'hr') {
    task('dashboard', arrive + r.int(5, 20), 'Open the HR dashboard', (p) => p.get('Open the HR dashboard', '/dashboard'));
    task('inbox-1', arrive + r.int(10, 40), 'Morning approvals', (p, co, d, r) => workInbox(p, r, { hr: true }));
    task('inbox-2', at(0.4, 0.55), 'Approvals after lunch', (p, co, d, r) => workInbox(p, r, { hr: true }));
    task('inbox-3', leave - r.int(15, 45), 'Approvals before leaving', (p, co, d, r) => workInbox(p, r, { hr: true }));
    task('correct', Math.max(arrive + 20, 660 + r.int(-30, 30)), 'Correct missing check-outs', (p, co, d, r) => correctAttendance(p, co, d, r));
    if (r.chance(0.35)) task('joiner', at(0.1, 0.5), 'Add a new joiner', (p, co, d, r) => addJoiner(p, co, d, r));
    if (r.chance(0.25)) task('announce', arrive + r.int(20, 90), 'Post an announcement', (p, co, d, r) => announce(p, d, r));
    if (r.chance(0.6)) task('recruit', at(0.3, 0.8), 'Move candidates along', (p, co, d, r) => recruit(p, co, d, r));
    task('payroll', Math.min(leave - 30, Math.max(arrive + 30, 900 + r.int(-40, 40))), 'Payroll (month end)', (p, co, d) => payroll(p, d));
    const hrTickets = (p, co, d, r) => {
      const hrCats = co.ref.helpdesk.filter((c) => /HR|payroll/i.test(c.name)).map((c) => c.id);
      return answerTickets(p, co, r, co.agents.length ? { onlyCategories: hrCats } : {});
    };
    task('tickets-1', at(0.1, 0.3), 'Answer HR tickets', hrTickets);
    task('tickets-2', at(0.6, 0.8), 'Answer HR tickets', hrTickets);
    if (r.chance(0.5)) task('suggestions', at(0.3, 0.9), 'Review suggestions', (p, co, d, r) => reviewSuggestions(p, r));
    for (const [id, chance, label, fn] of HR_MODULES) if (r.chance(chance)) task(id, at(0.1, 0.9), label, fn);
  } else if (p.role === 'manager') {
    task('inbox-1', arrive + r.int(15, 45), 'Morning approvals', (p, co, d, r) => workInbox(p, r));
    task('team', arrive + r.int(30, 75), 'Look at who is in today', (p, co, d) => p.get('Look at the team’s attendance', `/attendance/daily?date=${iso(d)}`));
    task('inbox-2', at(0.45, 0.6), 'Approvals after lunch', (p, co, d, r) => workInbox(p, r));
    task('inbox-3', leave - r.int(20, 50), 'Approvals before leaving', (p, co, d, r) => workInbox(p, r));
    if (r.chance(0.2)) task('nominate', at(), 'Nominate the team for training', (p, co, d, r) => nominate(p, r));
    if (r.chance(0.08)) task('leave', at(), 'Apply for leave', (p, co, d, r) => applyLeave(p, co, d, r));
    if (r.chance(0.08)) task('expense', at(), 'Claim expenses', (p, co, d, r) => claimExpense(p, d, r));
    for (const [id, chance, label, fn] of MANAGER_MODULES) if (r.chance(chance)) task(id, at(), label, fn);
  } else if (p.role === 'agent') {
    const queue = (p, co, d, r) => answerTickets(p, co, r, { onlyCategories: co.ref.helpdesk.filter((c) => c.agentIds?.includes(p.employeeId)).map((c) => c.id) });
    task('tickets-1', at(0.03, 0.2), 'Work the helpdesk queue', queue);
    task('tickets-2', at(0.4, 0.6), 'Work the helpdesk queue', queue);
    task('tickets-3', at(0.75, 0.95), 'Work the helpdesk queue', queue);
  } else {
    const next = co.cal.get(p.employeeId)?.get(iso(addDays(d, 1)));
    const submitDay = d.getUTCDay() === 5 || (next ? !isWorkday(next) : isOff(co, addDays(d, 1)));
    if (r.chance(0.14)) task('leave', at(), 'Apply for leave', (p, co, d, r) => applyLeave(p, co, d, r));
    if (r.chance(0.04)) task('cancel-leave', at(), 'Cancel a pending leave', (p, co, d) => cancelLeave(p, d));
    if (r.chance(0.12)) task('expense', at(), 'Claim expenses', (p, co, d, r) => claimExpense(p, d, r));
    if (r.chance(0.14)) task('ticket', at(), 'Raise a helpdesk ticket', (p, co, d, r) => raiseTicket(p, co, r));
    if (r.chance(0.6)) task('follow-tickets', at(0.4, 0.9), 'Follow up on my tickets', (p, co, d, r) => followTickets(p, r));
    if (r.chance(0.07)) task('letter', at(), 'Ask for a letter', (p, co, d, r) => requestLetter(p, co, r));
    if (r.chance(0.7)) task('timesheet', leave - r.int(10, 40), 'Fill in the timesheet', (p, co, d, r) => timesheet(p, co, d, r, { submitDay }));
    if (r.chance(0.05)) task('training', at(), 'Ask for training', (p, co, d, r) => requestTraining(p, co, r));
    if (r.chance(0.05)) task('suggest', at(), 'Make a suggestion', (p, co, d, r) => suggest(p, co, r));
    for (const [id, chance, label, fn] of EMPLOYEE_MODULES) if (r.chance(chance)) task(id, at(), label, fn);
    if (aiAskerOf(co, key) === p.email) task('ai', at(0.2, 0.8), 'Ask the AI assistant', askAi);
  }
  return plan;
}

async function applySickToday(p, co, d, r) {
  const type = leaveType(co, /sick/i);
  if (!type) return;
  const extra = {};
  const reasons = co.ref.leaveReasons.filter((x) => x.leaveTypeId === type.id);
  if (reasons.length) extra.reasonId = (reasons.find((x) => !x.requiresDocument) ?? reasons[0]).id;
  const reason = r.pick(LEAVE_REASONS.sick);
  const res = await p.post(`Apply for ${type.name} for today, ${iso(d)}`, '/leave', { leaveTypeId: type.id, fromDate: iso(d), toDate: iso(d), reason, ...extra },
    { refuse: REFUSALS.leave });
  if (res.ok) p.did(`Called in sick and applied for ${type.name} for today: “${reason}”`);
}

const progressOf = (co, key) => ((co.state.progress ??= {})[key] ??= {});
const finished = (s) => s === 'done' || s === 'dry';

/**
 * The next morning a person asks for a fix for any of the last few working days whose punches did not all go in:
 * a missed check-in or check-out (the run came too late, they forgot) becomes a missed-punch request with the times
 * of their plan; an unexplained absence sometimes becomes a work-from-home request. Each day is handled once.
 */
async function fixEarlierDays(p, co, d, r) {
  let requests = null;
  for (let back = 1, seen = 0; back <= 7 && seen < 3; back++) {
    const d0 = addDays(d, -back);
    const k0 = iso(d0);
    if (co.state.firstDay && k0 < co.state.firstDay) break;
    const cell = co.cal.get(p.employeeId)?.get(k0);
    if (!cell) break;
    if (!isWorkday(cell) || k0 >= iso(REAL_TODAY)) continue; // with --now ahead of the server: only days the server has finished
    seen++;
    const prog0 = co.state.progress?.[k0];
    const key = `${p.email}|fixed`;
    if (prog0?.[key]) continue;
    const handled = () => {
      progressOf(co, k0)[key] = { s: 'done' };
      saveState();
    };
    const plan = planDay(co, p, d0);
    const state = (id) => prog0?.[`${p.email}|${id}`]?.s;
    let kind, inAt, outAt, reason;
    if (co.state.days.includes(k0)) {
      handled(); // a backfilled day: the backfill already asked for its fixes
      continue;
    }
    if (plan.status === 'none' || prog0?.[`${p.email}|day`]?.onLeave || (plan.status !== 'present' && plan.status !== 'absent')) {
      handled();
      continue;
    }
    if (plan.status === 'absent') {
      if (!r.chance(0.35)) {
        handled();
        continue;
      }
      [kind, inAt, outAt] = ['WorkFromHome', plan.start, plan.end];
      reason = r.pick(['Worked from home, the car was at the mechanic. Was on Teams all day.', 'Worked from home because of the smog; my asthma was bad.',
        'Waited at home for the internet technician; worked online.']);
    } else if (state('in')) {
      if (finished(state('in')) && finished(state('out'))) {
        handled();
        continue;
      }
      [kind, inAt, outAt] = ['MissedPunch', plan.arrive, plan.leave];
      if (!finished(state('in')))
        reason = r.pick(['The attendance app did not open in the morning, so no check-in was recorded.', 'Forgot to check in; was at my desk from the morning.']);
      else {
        if (state('in') === 'done') {
          const rec = await myRecord(p, d0);
          if (rec?.checkOut) {
            handled();
            continue;
          }
          if (rec) inAt = minutesOf(rec.checkIn.slice(11, 16));
        }
        reason = r.pick(['Forgot to check out, left after the team meeting.', 'Phone battery died before I could check out.', 'Left in a hurry for a family emergency and forgot to check out.']);
      }
    } else {
      // No run reached this person that day (the Mac was off): look at what the app has.
      const rec = await myRecord(p, d0);
      if (rec?.checkOut) {
        handled();
        continue;
      }
      [kind, inAt, outAt] = ['MissedPunch', rec ? minutesOf(rec.checkIn.slice(11, 16)) : plan.arrive, plan.leave];
      reason = rec ? 'Forgot to check out that day.' : 'The attendance app did not record my punches that day; I was in the office.';
    }
    requests ??= (await p.get('Open my attendance requests', '/attendance/requests/my')).data ?? [];
    if (requests.some((x) => x.date === k0)) {
      handled();
      continue;
    }
    p.cache ??= {};
    p.cache.leave ??= (await p.get('Open my leave', '/leave/my')).data ?? [];
    if (p.cache.leave.some((l) => l.status === 'Approved' && l.fromDate <= k0 && l.toDate >= k0)) {
      handled();
      continue;
    }
    outAt = Math.max(outAt, inAt + 60);
    const res = await p.post(`Ask to fix attendance for ${k0} (${kind})`, '/attendance/requests',
      { date: k0, kind, checkIn: hhmm(inAt), checkOut: hhmm(Math.min(outAt, 1439)), reason }, { refuse: REFUSALS.attendanceRequest });
    if (res.ok) p.did(`Asked to fix attendance for ${k0} (${kind === 'WorkFromHome' ? 'worked from home' : 'missed punch'}, ${hm(inAt)}–${hm(Math.min(outAt, 1439))}): “${reason}”`);
    handled();
  }
}

async function myRecord(p, d) {
  const month = `${d.getUTCFullYear()}-${d.getUTCMonth() + 1}`;
  p.cache ??= {};
  p.cache[month] ??= (await p.get('Open my attendance', `/attendance/my?year=${d.getUTCFullYear()}&month=${d.getUTCMonth() + 1}`)).data ?? [];
  return p.cache[month].find((a) => a.date === iso(d));
}

// ---------- Working hours: a run ----------

/** Whether the person is on approved full-day leave today; read once per day, the first time they act. */
async function dayStatus(co, p, d, prog) {
  const k = `${p.email}|day`;
  if (!prog[k]) {
    const mine = (await p.get('Open my leave', '/leave/my')).data ?? [];
    const onLeave = mine.some((l) => l.status === 'Approved' && l.fromDate <= iso(d) && l.toDate >= iso(d) && (l.duration ?? 'FullDay') === 'FullDay');
    prog[k] = { onLeave };
    if (onLeave) p.did('On approved leave today, not coming in');
    saveState();
  }
  return prog[k];
}

async function livePunch(co, p, a) {
  const today = (await p.get('Open today’s attendance', '/attendance/today')).data;
  if (!today) return 'failed';
  if (today.date !== iso(dayOf(clock()))) return 'skipped';
  const r = rng(`${co.name}|${p.email}|${today.date}|${a.id}|where|${SEED}`);
  const source = rng(`${co.name}|${p.email}|${today.date}|source|${SEED}`).chance(0.6) ? 'Mobile' : 'Web';
  const office = co.ref.offices[0];
  const where = () => {
    if (!today.locationRequired || !office) return {};
    const j = () => (r.next() - 0.5) * 0.002; // within ~120 m of the office gate
    return { latitude: +(office.latitude + j()).toFixed(6), longitude: +(office.longitude + j()).toFixed(6) };
  };
  const outcome = (res) => (res.ok ? 'done' : res.status === 400 && !res.validation ? 'refused' : 'failed');

  if (a.id === 'in') {
    if (today.record) {
      // Already in (an earlier run, a fix request or HR): never a second check-in.
      p.did(`Already checked in at ${today.record.checkIn?.slice(11, 16)}; no second check-in`);
      return 'done';
    }
    // Now and then someone taps check-in at home before leaving; the location check should turn it down.
    if (today.locationRequired && office && r.chance(0.05)) {
      const res = await p.post('Check in (still at home)', '/attendance/check-in', { latitude: 31.5204, longitude: 74.3587, source }, { refuse: REFUSALS.punch });
      if (res.ok) {
        p.did('Checked in from home (the location check let it through)');
        return 'done';
      }
    }
    const res = await p.post(a.label, '/attendance/check-in', { ...where(), source }, { refuse: REFUSALS.punch });
    if (!res.ok) return outcome(res);
    const rec = res.data;
    p.check(a.label, rec?.date === today.date && !!rec?.checkIn, 'POST', '/attendance/check-in', `Check-in returned date ${rec?.date} / checkIn ${rec?.checkIn}; today is ${today.date}.`);
    const after = (await p.get('Open today’s attendance after checking in', '/attendance/today')).data;
    if (after) p.check(a.label, after.record?.id === rec?.id, 'GET', '/attendance/today', `After checking in, today's record is ${after.record?.id ?? 'missing'}, expected ${rec?.id}.`);
    p.did(`${a.label.replace('Check in', 'Checked in')} from the ${source === 'Mobile' ? 'phone' : 'web'} at ${rec?.checkIn?.slice(11, 16)}${rec?.lateMinutes ? ` (${rec.lateMinutes} min late)` : ''}`);
    return 'done';
  }
  if (!today.record) return 'skipped';
  if (today.record.checkOut) {
    if (a.id === 'out') p.did(`Already checked out at ${today.record.checkOut.slice(11, 16)}; no second check-out`);
    return a.id === 'out' ? 'done' : 'skipped';
  }
  const open = (today.breaks ?? []).find((b) => !b.end);
  if (a.id === 'out') {
    const res = await p.post('Check out', '/attendance/check-out', { ...where(), source }, { refuse: REFUSALS.punch });
    if (!res.ok) return outcome(res);
    p.check('Check out', !!res.data?.checkOut && res.data.checkOut >= res.data.checkIn, 'POST', '/attendance/check-out',
      `Check-out came back as ${res.data?.checkOut} for a check-in at ${res.data?.checkIn}.`);
    p.did(`Checked out at ${res.data.checkOut?.slice(11, 16)} (${Math.round((res.data.workedMinutes ?? 0) / 6) / 10} h worked)`);
    return 'done';
  }
  if (a.id.endsWith('-start')) {
    if (open) return 'done';
    const res = await p.post(a.label, '/attendance/break/start', { kind: a.breakKind }, { refuse: REFUSALS.punch });
    if (res.ok) p.did(`Started a ${a.breakKind.toLowerCase()} break at ${res.data?.start?.slice(11, 16)}`);
    return outcome(res);
  }
  if (!open) return 'skipped';
  const res = await p.post(a.label, '/attendance/break/end', {}, { refuse: REFUSALS.punch });
  if (res.ok) p.did(`Back from the ${a.breakKind.toLowerCase()} break at ${res.data?.end?.slice(11, 16)} (${res.data?.minutes} min)`);
  return outcome(res);
}

/** Does one planned action, returning what became of it (kept in the state file). */
async function perform(co, p, a, d, prog) {
  if ((await dayStatus(co, p, d, prog)).onLeave) return 'on leave';
  const key = iso(d);
  if (a.kind === 'task') {
    try {
      await a.run(p, co, d, rng(`${co.name}|${p.email}|${key}|${a.id}|${SEED}`));
      return 'done';
    } catch (e) {
      record({ ...p.where(), persona: p.label, kind: 'failed', intent: a.label, method: '-', path: '-', query: '', status: 'script error', title: e.message, body: String(e.stack).slice(0, 400) });
      return 'failed';
    }
  }
  // Punches go in order: breaks and check-out need the check-in, a break end needs its start.
  const before = a.id === 'in' ? null : a.id.endsWith('-end') ? a.id.replace(/-end$/, '-start') : 'in';
  const prior = before && prog[`${p.email}|${before}`]?.s;
  if (before && !finished(prior)) return 'skipped';
  if (a.id !== 'out' && a.id !== 'in' && prog[`${p.email}|out`]) return 'skipped';
  if (DRY_PUNCHES || prior === 'dry') {
    p.did(`${a.label} at ${hm(a.at)} (dry: --now is not the server's clock, so the punch was not sent)`);
    return 'dry';
  }
  const late = minuteOf(clock()) - a.at;
  if (a.endsAt != null && minuteOf(clock()) >= a.endsAt) {
    p.did(`Did not log the ${a.breakKind.toLowerCase()} break of ${hm(a.at)}–${hm(a.endsAt)} (the run came after it ended)`);
    return 'missed';
  }
  if (late > LATE_OK) {
    const big = a.id === 'in' || a.id === 'out';
    p.did(`Did not ${a.label.toLowerCase()} (due at ${hm(a.at)}, the run came ${late} min later)${big ? '; asks for a missed-punch fix on the next working day' : ''}`);
    return 'missed';
  }
  return livePunch(co, p, a);
}

/** Everything due for this company now: overdue actions at once, then the next --lookahead minutes on their minute. */
async function runToday(co, d) {
  const key = iso(d);
  co.day = key;
  const prog = progressOf(co, key);
  co.state.firstDay ??= [...co.state.days, key].sort()[0];
  const all = [];
  for (const p of cast(co).filter((x) => x.token && x.employeeId)) for (const a of planDay(co, p, d).actions) all.push({ p, a });
  const rank = (a) => (a.id === 'in' ? 0 : a.kind === 'punch' && a.id !== 'out' ? 1 : a.kind === 'task' ? 2 : 3);
  all.sort((x, y) => x.a.at - y.a.at || rank(x.a) - rank(y.a));
  const nowMinute = () => (iso(dayOf(clock())) === key ? minuteOf(clock()) : 1440);
  const horizon = Math.min(1439, Math.max(nowMinute(), iso(dayOf(START)) === key ? minuteOf(START) + (DRY_PUNCHES ? 0 : LOOKAHEAD) : 0));
  const stats = { actions: 0, punches: 0, missed: 0 };
  for (const { p, a } of all) {
    const k = `${p.email}|${a.id}`;
    if (prog[k] || a.at > horizon) continue;
    const wait = dayStartMs(key) + a.at * 60_000 - clock();
    if (wait > 0) await sleep(wait + Math.floor(Math.random() * 40_000));
    if (nowMinute() >= 1440) break; // past midnight: tomorrow's runs take over
    const s = await perform(co, p, a, d, prog);
    prog[k] = { s, at: hm(Math.min(nowMinute(), 1439)) };
    saveState();
    if (s === 'skipped' || s === 'on leave') continue;
    stats.actions++;
    if (a.kind === 'punch' && (s === 'done' || s === 'dry')) stats.punches++;
    if (s === 'missed') stats.missed++;
  }
  const blocked = ({ p, a }) => {
    if (a.kind !== 'punch' || a.id === 'in') return prog[`${p.email}|day`]?.onLeave;
    const before = a.id.endsWith('-end') ? a.id.replace(/-end$/, '-start') : 'in';
    const s = prog[`${p.email}|${before}`]?.s;
    return s && !finished(s);
  };
  const next = all.find((x) => !prog[`${x.p.email}|${x.a.id}`] && !blocked(x));
  return { stats, next: next ? `${hm(next.a.at)} ${next.p.label.replace(/ \(.*\)$/, '')}: ${next.a.label.toLowerCase()}` : 'nothing more today' };
}

function printPlan(co, d) {
  const key = iso(d);
  const prog = co.state.progress?.[key] ?? {};
  console.log(`${co.name}, ${key} (${WEEKDAYS[d.getUTCDay()]})`);
  for (const p of cast(co).filter((x) => x.token && x.employeeId)) {
    const plan = planDay(co, p, d);
    const times = plan.status === 'present' || plan.status === 'weekend work'
      ? ` ${hm(plan.arrive)}–${plan.forgetsCheckOut ? `(forgets to check out, leaves ${hm(plan.leave)})` : hm(plan.leave)}${plan.late ? ' late' : ''}`
      + (plan.breaks?.length ? `, breaks ${plan.breaks.map((b) => `${b.kind} ${hm(b.from)}–${hm(b.to)}`).join(', ')}` : '')
      : '';
    console.log(`  ${p.label} [${p.role}${plan.shift ? `, ${plan.shift}` : ''}]: ${plan.status}${times}`);
    for (const a of [...plan.actions].sort((x, y) => x.at - y.at)) {
      const s = prog[`${p.email}|${a.id}`];
      console.log(`      ${hm(a.at)} ${a.label}${s ? `  [${s.s} at ${s.at}]` : ''}`);
    }
  }
}

// ---------- Report ----------

const callKey = (f) => `${f.method} ${f.path.replace(/[0-9a-f]{8}-[0-9a-f-]{27}/gi, '{id}')} → ${f.status}`;

/** Adds this run's section to the day's report (one file per day, kept across runs). */
function appendReport(day, title) {
  const failed = events.filter((e) => e.kind === 'failed');
  const refused = events.filter((e) => e.kind === 'refused');
  const did = events.filter((e) => e.kind === 'did');
  mkdirSync(SECRETS, { recursive: true });
  const file = join(SECRETS, `virtual-clients-report-${day}.md`);
  const out = [];
  if (!existsSync(file))
    out.push(`# Virtual clients, ${day}`, '', `Server: ${BASE}. Each run adds a section. Failures are app bugs (or script mistakes); refusals are business rules a real user meets.`, '');
  out.push(`## ${title}`, '', `Actions: ${did.length} · refused: ${refused.length} · **failures: ${failed.length}**`, '');
  if (failed.length) {
    const groups = new Map();
    for (const f of failed) groups.set(callKey(f), [...(groups.get(callKey(f)) ?? []), f]);
    out.push('### Failures', '');
    for (const [k, list] of groups) {
      out.push(`#### \`${k}\` (${list.length}×)`, '');
      for (const f of list.slice(0, 6))
        out.push(`- ${f.company}, ${f.date} ${f.at}, **${f.persona}** trying to *${f.intent}*: \`${f.method} ${f.path}${f.query ?? ''}\` → ${f.status}`, `  \`\`\`\n  ${f.body || '(empty body)'}\n  \`\`\``);
      if (list.length > 6) out.push(`- … and ${list.length - 6} more like this`);
      out.push('');
    }
  }
  if (refused.length) {
    out.push('### Refused by business rules', '');
    for (const e of refused) out.push(`- ${e.company}, ${e.date} ${e.at}, ${e.persona}: ${e.intent} → “${e.title}”`);
    out.push('');
  }
  for (const c of [...new Set(events.map((e) => e.company))]) {
    for (const date of [...new Set(events.filter((e) => e.company === c).map((e) => e.date))].sort()) {
      const evs = events.filter((e) => e.company === c && e.date === date);
      out.push(`### ${c}${date === day ? '' : `, ${date}`}`, '');
      for (const e of evs) {
        const text = e.kind === 'did' ? e.text : e.kind === 'refused' ? `REFUSED: ${e.intent} (“${e.title}”)` : `FAILED: ${e.intent} (\`${e.method} ${e.path}\` → ${e.status})`;
        out.push(`- ${e.at} **${e.persona}**: ${text}`);
      }
      out.push('');
    }
  }
  if (!events.length) out.push('Nothing was due.', '');
  appendFileSync(file, out.join('\n') + '\n');
  return { file, failed, refused, did };
}

// ---------- Run ----------

const LOCK = join(SECRETS, 'virtual-clients.lock');
function takeLock() {
  mkdirSync(SECRETS, { recursive: true });
  if (existsSync(LOCK)) {
    const pid = Number(readFileSync(LOCK, 'utf8'));
    try {
      process.kill(pid, 0);
      console.log(`${iso(TODAY)} ${hm(minuteOf(START))} an earlier run (pid ${pid}) is still going; nothing to do.`);
      process.exit(0);
    } catch {
      // stale lock from a run that died
    }
  }
  writeFileSync(LOCK, String(process.pid));
  process.on('exit', () => {
    try {
      unlinkSync(LOCK);
    } catch {
      // already gone
    }
  });
  for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => process.exit(130));
}

async function main() {
  if (!PLAN_ONLY) takeLock();
  const stamp = `${iso(TODAY)} ${hm(minuteOf(START))}`;
  if (DRY_PUNCHES) console.log(`(--now ${NOW_ARG} is not the real time: punches are logged, not sent)`);
  if (BACKFILL) console.log(`Virtual clients on ${BASE}: backfill of ${DAYS} day(s) ending ${iso(LAST_DAY)}`);
  const lines = [];
  // Set up one company at a time (sign-in is rate limited) ...
  const companies = [];
  for (const file of LOGIN_FILES) {
    ctx.company = file;
    ctx.date = iso(TODAY);
    try {
      const co = await setupCompany(file);
      ctx.company = co.name;
      co.day = iso(TODAY);
      await loadReference(co);
      await growCast(co, rng(`${co.name}|cast|${SEED}`));
      for (const p of cast(co)) if (!p.token) await p.signIn();
      await loadCalendar(co, addDays(BACKFILL ? addDays(LAST_DAY, -(DAYS - 1)) : TODAY, -10), addDays(TODAY, 21)); // ahead: shift requests check the coming roster
      companies.push(co);
    } catch (e) {
      lines.push(`${stamp} ${file}: ${e.message}`);
      if (!e.skip)
        record({ persona: 'setup', kind: 'failed', intent: `Start ${file}`, method: '-', path: '-', query: '', status: 0, title: e.message, body: e.message });
    }
  }
  if (PLAN_ONLY) {
    for (const co of companies) printPlan(co, TODAY);
    return;
  }
  if (!companies.length) {
    // No company's turn here (another computer is working): nothing to report.
    for (const l of lines) console.log(l);
    process.exit(0);
  }
  // ... then let them all work at the same time, so every company's punches land on their minute.
  const title = BACKFILL ? `Backfill run at ${stamp.slice(11)}: ${DAYS} day(s) ending ${iso(LAST_DAY)}` : `Run at ${stamp.slice(11)}${DRY_PUNCHES ? ' (--now, dry punches)' : ''}`;
  let reported = false;
  const finish = (note = '') => {
    if (reported) return [];
    reported = true;
    const { file, failed } = appendReport(iso(TODAY), title + note);
    for (const l of lines) console.log(l);
    const seen = new Set();
    for (const f of failed) {
      if (seen.has(callKey(f))) continue;
      seen.add(callKey(f));
      console.log(`  FAILED ${callKey(f)}: ${f.intent} — ${(f.title || f.body).slice(0, 140)}`);
    }
    console.log(`  report: ${file}`);
    return failed;
  };
  // Stopped half way (launchd, Ctrl-C): what was done is in the state file; put it in the report too.
  for (const sig of ['SIGINT', 'SIGTERM']) {
    process.removeAllListeners(sig);
    process.on(sig, () => {
      finish(' (stopped before the end)');
      endTurns().finally(() => process.exit(130));
    });
  }
  await Promise.all(companies.map(async (co) => {
    let result = null;
    if (BACKFILL) {
      for (let i = DAYS - 1; i >= 0; i--) {
        const d = addDays(LAST_DAY, -i);
        if (iso(d) === iso(TODAY)) {
          result = await runToday(co, d);
          continue;
        }
        if (co.state.days.includes(iso(d)) && !FORCE) {
          console.log(`  ${co.name}, ${iso(d)}: already played (use --force to play it again)`);
          continue;
        }
        if (co.state.progress?.[iso(d)] && !FORCE) {
          console.log(`  ${co.name}, ${iso(d)}: was played hour by hour; not backfilling it`);
          continue;
        }
        await simulateDay(co, d);
        if (!co.state.days.includes(iso(d))) co.state.days.push(iso(d));
        saveState();
      }
    } else result = await runToday(co, TODAY);
    // Forget hour-by-hour progress after three weeks.
    for (const k of Object.keys(co.state.progress ?? {})) if (k < iso(addDays(TODAY, -21))) delete co.state.progress[k];
    saveState();
    const mine = events.filter((e) => e.company === co.name);
    const n = (kind) => mine.filter((e) => e.kind === kind).length;
    lines.push(`${stamp} ${co.name}: ${result ? `${result.stats.actions} due action(s), ${result.stats.punches} punch(es)${result.stats.missed ? `, ${result.stats.missed} missed` : ''}, ` : ''}`
      + `${n('did')} done, ${n('refused')} refused, ${n('failed')} failed${result ? `; next ${result.next}` : ''}`);
  }));
  await endTurns();
  const failed = finish();
  process.exit(failed.length ? 2 : 0);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
