#!/usr/bin/env node
// Virtual clients: simulates ordinary working days inside companies that seed-demo.mjs filled, acting through the
// public API as the company's own people, and reports every call the app got wrong.
//
//   node deploy/virtual-clients.mjs --logins deploy/secrets/demo-logins.txt
//   node deploy/virtual-clients.mjs --base http://localhost:5080 --logins a.txt --logins b.txt --days 3
//
// Flags
//   --base <url>      API server (default https://qaventra.runasp.net)
//   --logins <file>   a demo-logins.txt written by seed-demo.mjs; repeat the flag (or comma-separate) for several companies
//   --days <n>        simulate the n calendar days ending on --date (default 1)
//   --date <y-m-d>    last simulated day (default: today in Pakistan). Live punches (check-in/out, breaks) only happen on
//                     the real today; earlier days are worked through missed-punch and work-from-home requests instead.
//   --force           simulate days that an earlier run already simulated for that company
//   --seed <text>     changes the day's random choices (default: none; the same day always plays out the same way)
//
// Who acts: the HR manager, the line manager and the employees from the logins file. On the first run HR also gives
// logins (same shared password) to a few more of the manager's team and to an IT helpdesk agent, and every new joiner
// HR adds gets one too, so the cast grows over time. Logins and their tokens are kept in
// deploy/secrets/virtual-clients-state.json (git-ignored) so sign-in, which is rate limited, is rarely needed.
//
// Outcome of every call: ok; "refused" when the app turned the request down for a business reason a real user can run
// into (no leave balance, overlapping dates, ...); "failed" for 5xx, network errors and any other 4xx. Failures are
// what to look at: each is an app bug or something this script got wrong. The report is written to
// deploy/secrets/virtual-clients-report-<date>.md.

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

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
const DAYS = Math.max(1, Number(arg('days', '1')) || 1);
const FORCE = flag('force');
const SEED = arg('seed', '');
const SECRETS = join(here, 'secrets');
const STATE_FILE = join(SECRETS, 'virtual-clients-state.json');

if (!LOGIN_FILES.length) {
  const fallback = join(SECRETS, 'demo-logins.txt');
  if (existsSync(fallback)) LOGIN_FILES.push(fallback);
  else {
    console.error('Give at least one --logins file (the demo-logins.txt that seed-demo.mjs writes).');
    process.exit(1);
  }
}

// ---------- Dates (company-local, Pakistan UTC+5) ----------

const pad = (n) => String(n).padStart(2, '0');
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (d, n) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() + n));
const parseDate = (s) => new Date(`${s}T00:00:00Z`);
const localNow = () => new Date(Date.now() + 5 * 3600_000);
const REAL_TODAY = (() => {
  const n = localNow();
  return new Date(Date.UTC(n.getUTCFullYear(), n.getUTCMonth(), n.getUTCDate()));
})();
const LAST_DAY = arg('date') ? parseDate(arg('date')) : REAL_TODAY;
if (LAST_DAY > REAL_TODAY) {
  console.error('--date cannot be in the future.');
  process.exit(1);
}
const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const minutesOf = (t) => (t ? Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5)) : null);
const hhmm = (m) => `${pad(Math.floor(m / 60))}:${pad(m % 60)}:00`;
const nowMinutes = () => localNow().getUTCHours() * 60 + localNow().getUTCMinutes();

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
const record = (e) => events.push({ company: ctx.company, date: ctx.date, ...e });

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
    /only be marked from the office network/, /Check-in opens at/, /Location is required/],
  expense: [/is limited to Rs/, /needs a receipt/, /is not open to your grade/],
  letter: [/already asked for this letter/],
  training: [/already asked for this course/, /Only \d+ seats are left/, /not open for nominations/, /already started/],
  decide: [/This request is already/, /is not waiting for approval/, /needs a supporting document/],
  manual: [/is closed\. Ask HR to reopen/, /after the employee left/],
  timesheet: [/is closed\. Ask HR to reopen/, /already (submitted|approved)/i],
  helpdesk: [/the employee closes the ticket after you resolve it/],
  payroll: [/already (finalized|approved|submitted)/i],
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

  did(text) {
    record({ persona: this.label, kind: 'did', text });
  }

  /** The call worked but what came back is wrong: recorded as a failure (status shows what the server answered). */
  check(intent, ok, method, path, detail) {
    if (ok) return true;
    record({ persona: this.label, kind: 'failed', intent, method, path, query: '', status: 'wrong data', title: detail, body: detail });
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
      persona: this.label, kind: refused ? 'refused' : 'failed', intent, method, path: path.split('?')[0], query: path.includes('?') ? path.slice(path.indexOf('?')) : '',
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
        record({ persona: this.label, kind: 'failed', intent: 'Sign in', method: 'POST', path: '/auth/login', query: '', status: r.status, title: r.title,
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
    record({ persona: this.label, kind: 'failed', intent: 'Sign in', method: 'POST', path: '/auth/login', query: '', status: 429, title: 'Rate limited after retries', body: '' });
  }
}

// ---------- State ----------

const state = existsSync(STATE_FILE) ? JSON.parse(readFileSync(STATE_FILE, 'utf8')) : { companies: {} };
function saveState() {
  mkdirSync(SECRETS, { recursive: true });
  writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
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

/** Attendance for one person on one day. Live punches only on the real today. */
async function attendance(p, co, d, r, { live, workday, weekendWorker }) {
  if (live) {
    const today = (await p.get('Open today’s attendance', '/attendance/today')).data;
    if (!today) return;
    if (!workday && !weekendWorker) return;
    const shiftStart = minutesOf(today.shiftStart) ?? 540;
    const shiftEnd = minutesOf(today.shiftEnd) ?? 1080;
    const office = co.ref.offices[0];
    const where = () => {
      if (!today.locationRequired || !office) return {};
      // Within ~120 m of the office gate.
      const j = () => (r.next() - 0.5) * 0.002;
      return { latitude: +(office.latitude + j()).toFixed(6), longitude: +(office.longitude + j()).toFixed(6) };
    };
    const source = r.chance(0.6) ? 'Mobile' : 'Web';
    let rec = today.record;
    if (!rec) {
      if (nowMinutes() < shiftStart - 60 && workday) return; // too early, nobody is in yet
      // Now and then someone taps check-in from home before leaving.
      if (today.locationRequired && office && r.chance(0.08)) {
        const res = await p.post('Check in (still at home)', '/attendance/check-in', { latitude: 31.5204, longitude: 74.3587, source }, { refuse: REFUSALS.punch });
        if (res.ok) p.did('Checked in from home (the location check let it through)');
      }
      const res = await p.post(weekendWorker ? 'Check in for weekend work' : 'Check in', '/attendance/check-in', { ...where(), source }, { refuse: REFUSALS.punch });
      if (!res.ok) return;
      rec = res.data;
      p.check('Check in', rec?.date === today.date && !!rec?.checkIn, 'POST', '/attendance/check-in', `Check-in returned date ${rec?.date} / checkIn ${rec?.checkIn}; today is ${today.date}.`);
      const after = (await p.get('Open today’s attendance after checking in', '/attendance/today')).data;
      if (after) p.check('Check in', after.record?.id === rec.id, 'GET', '/attendance/today', `After checking in, today's record is ${after.record?.id ?? 'missing'}, expected ${rec.id}.`);
      p.did(`${weekendWorker ? 'Came in on a weekend and checked' : 'Checked'} in from the ${source === 'Mobile' ? 'phone' : 'web'} at ${rec.checkIn?.slice(11, 16)}${rec.lateMinutes ? ` (${rec.lateMinutes} min late)` : ''}`);
    }
    if (rec.checkOut) return;
    if (r.chance(0.4)) {
      const kind = r.pick(['Lunch', 'Prayer', 'Tea']);
      const s = await p.post(`Start a ${kind.toLowerCase()} break`, '/attendance/break/start', { kind }, { refuse: [/already on a break/] });
      if (s.ok) {
        const e = await p.post(`End the ${kind.toLowerCase()} break`, '/attendance/break/end', {});
        if (e.ok) p.did(`Took a ${kind.toLowerCase()} break`);
      }
    }
    const leaving = nowMinutes() >= shiftEnd - 15 || weekendWorker || r.chance(0.05);
    if (leaving) {
      const res = await p.post('Check out', '/attendance/check-out', { ...where(), source }, { refuse: REFUSALS.punch });
      if (res.ok) p.did(`Checked out at ${res.data.checkOut?.slice(11, 16)} (${Math.round((res.data.workedMinutes ?? 0) / 6) / 10} h)`);
    } else p.did('Still at work when the run ended (no check-out yet)');
    return;
  }
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

async function managerDay(m, co, d, r, opts) {
  if (!m?.token) return;
  await attendance(m, co, d, r, opts);
  if (!opts.workday && !opts.weekendWorker) return;
  await workInbox(m, r);
  await m.get('Look at the team’s attendance', `/attendance/daily?date=${iso(d)}`);
  await social(m, r);
  // Nominate team members for an upcoming training session.
  if (r.chance(0.2)) {
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
  if (r.chance(0.08)) await applyLeave(m, co, d, r);
  if (r.chance(0.08)) await claimExpense(m, d, r);
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
  const joining = isOff(co, d) ? workdaysFrom(co, d, 1)[0] : d;
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
  const open = rows.filter((x) => x.record && !x.record.checkOut);
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
}

// ---------- A day ----------

async function simulateDay(co, d) {
  const r = rng(`${co.name}|${iso(d)}|${SEED}`);
  ctx.date = iso(d);
  const live = iso(d) === iso(REAL_TODAY);
  const workday = !isOff(co, d);
  const kind = workday ? 'working day' : co.ref.holidays.has(iso(d)) ? 'holiday' : 'weekly off';
  console.log(`  ${iso(d)} (${WEEKDAYS[d.getUTCDay()]}, ${kind}${live ? ', live' : ''})`);

  // Everyone with a login who comes in today; on a day off one or two people still come in for urgent work.
  const staff = co.employees.filter((p) => !p.joinedOn || p.joinedOn <= iso(d));
  const present = workday ? staff.filter(() => r.chance(0.9)) : [];
  const weekendCrew = !workday && live ? r.shuffle(staff).slice(0, r.int(1, 2)) : [];

  const base = { live, workday, submitDay: workday && (d.getUTCDay() === 5 || isOff(co, addDays(d, 1))) };
  await hrDay(co.hr, co, d, r, { ...base, weekendWorker: false });
  for (const p of r.shuffle([...present, ...weekendCrew])) await employeeDay(p, co, d, r, { ...base, weekendWorker: weekendCrew.includes(p) });
  for (const a of co.agents) {
    if (!a.token) await a.signIn();
    if (!a.token || !workday) continue;
    await attendance(a, co, d, r, { ...base, weekendWorker: false });
    await answerTickets(a, co, r, { onlyCategories: co.ref.helpdesk.filter((c) => c.agentIds?.includes(a.employeeId)).map((c) => c.id) });
    await social(a, r);
  }
  await managerDay(co.mgr, co, d, r, { ...base, weekendWorker: false });
  // End of the day: the line manager and HR look at what came in since the morning.
  if (workday) {
    if (co.mgr?.token) await workInbox(co.mgr, r);
    await workInbox(co.hr, r, { hr: true });
  }
}

// ---------- Report ----------

function writeReport() {
  const failed = events.filter((e) => e.kind === 'failed');
  const refused = events.filter((e) => e.kind === 'refused');
  const did = events.filter((e) => e.kind === 'did');
  const out = [];
  out.push(`# Virtual clients report, ${iso(REAL_TODAY)}`, '');
  out.push(`Server: ${BASE}  `, `Days: ${DAYS} ending ${iso(LAST_DAY)}  `, `Run at: ${new Date().toISOString()}  `,
    `Actions done: ${did.length} · refused (business rules): ${refused.length} · **failures: ${failed.length}**`, '');

  out.push('## Failures', '');
  if (!failed.length) out.push('None.', '');
  else {
    const groups = new Map();
    for (const f of failed) {
      const key = `${f.method} ${f.path.replace(/[0-9a-f]{8}-[0-9a-f-]{27}/gi, '{id}')} → ${f.status}`;
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(f);
    }
    out.push('| # | Call | Times | First seen trying to |', '|---|---|---|---|');
    [...groups.entries()].forEach(([k, list], i) => out.push(`| ${i + 1} | \`${k}\` | ${list.length} | ${list[0].intent} (${list[0].persona}) |`));
    out.push('');
    [...groups.entries()].forEach(([k, list], i) => {
      out.push(`### ${i + 1}. \`${k}\``, '');
      for (const f of list.slice(0, 6))
        out.push(`- ${f.company}, ${f.date}, **${f.persona}** trying to *${f.intent}*: \`${f.method} ${f.path}${f.query ?? ''}\` → ${f.status}`, `  \`\`\`\n  ${f.body || '(empty body)'}\n  \`\`\``);
      if (list.length > 6) out.push(`- … and ${list.length - 6} more like this`);
      out.push('');
    });
  }

  out.push('## Refused by business rules (expected)', '');
  if (!refused.length) out.push('None.', '');
  for (const e of refused) out.push(`- ${e.company}, ${e.date}, ${e.persona}: ${e.intent} → “${e.title}”`);
  out.push('');

  out.push('## What each persona did', '');
  const companies = [...new Set(events.map((e) => e.company))];
  for (const c of companies) {
    out.push(`### ${c}`, '');
    const dates = [...new Set(events.filter((e) => e.company === c).map((e) => e.date))].filter(Boolean).sort();
    for (const date of dates) {
      out.push(`#### ${date}`, '');
      const evs = events.filter((e) => e.company === c && e.date === date);
      for (const persona of [...new Set(evs.map((e) => e.persona))]) {
        out.push(`**${persona}**`, '');
        for (const e of evs.filter((x) => x.persona === persona)) {
          if (e.kind === 'did') out.push(`- ${e.text}`);
          else if (e.kind === 'refused') out.push(`- REFUSED: ${e.intent} (“${e.title}”)`);
          else out.push(`- FAILED: ${e.intent} (\`${e.method} ${e.path}\` → ${e.status})`);
        }
        out.push('');
      }
    }
  }
  mkdirSync(SECRETS, { recursive: true });
  const file = join(SECRETS, `virtual-clients-report-${iso(REAL_TODAY)}.md`);
  writeFileSync(file, out.join('\n'));
  return { file, failed, refused, did };
}

// ---------- Run ----------

async function main() {
  console.log(`Virtual clients on ${BASE}: ${DAYS} day(s) ending ${iso(LAST_DAY)}\n`);
  for (const file of LOGIN_FILES) {
    let co;
    try {
      co = await setupCompany(file);
    } catch (e) {
      console.log(`  ${file}: ${e.message}`);
      record({ persona: 'setup', kind: 'failed', intent: `Start ${file}`, method: '-', path: '-', query: '', status: 0, title: e.message, body: e.message });
      continue;
    }
    ctx.company = co.name;
    ctx.date = '';
    console.log(`${co.name}`);
    await loadReference(co);
    await growCast(co, rng(`${co.name}|cast|${SEED}`));
    for (let i = DAYS - 1; i >= 0; i--) {
      const d = addDays(LAST_DAY, -i);
      if (co.state.days.includes(iso(d)) && !FORCE) {
        console.log(`  ${iso(d)} already simulated (use --force to run it again)`);
        continue;
      }
      await simulateDay(co, d);
      if (!co.state.days.includes(iso(d))) co.state.days.push(iso(d));
      saveState();
    }
    console.log('');
  }
  const { file, failed, refused, did } = writeReport();
  console.log(`Done: ${did.length} actions, ${refused.length} refused by business rules, ${failed.length} failures.`);
  const seen = new Set();
  for (const f of failed) {
    const k = `${f.method} ${f.path.replace(/[0-9a-f]{8}-[0-9a-f-]{27}/gi, '{id}')} → ${f.status}`;
    if (seen.has(k)) continue;
    seen.add(k);
    console.log(`  FAILED ${k}: ${f.intent} — ${(f.title || f.body).slice(0, 140)}`);
  }
  console.log(`Report: ${file}`);
  process.exit(failed.length ? 2 : 0);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
