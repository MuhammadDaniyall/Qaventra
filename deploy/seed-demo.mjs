#!/usr/bin/env node
// Fills a demo company with realistic data through the public API, so every screen has something to show.
//
//   node deploy/seed-demo.mjs                                  # production, owner login from deploy/secrets/production.json
//   node deploy/seed-demo.mjs --base http://localhost:5199     # any other server; set QV_OWNER_EMAIL / QV_OWNER_PASSWORD
//
// Signed in as the platform owner it creates the company (stops if it already exists). Signed in as a company admin it
// fills that admin's own company instead (stops if the demo people are already there). Then departments, people, logins, attendance from a biometric
// machine, leave, a finalized payroll with its journal, performance reviews, hiring, training, helpdesk and more.
// The demo logins are written to deploy/secrets/demo-logins.txt (git-ignored).

import { randomBytes } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const arg = (name, fallback) => {
  const i = process.argv.indexOf(`--${name}`);
  return i > 0 ? process.argv[i + 1] : fallback;
};
const BASE = arg('base', 'https://qaventra.runasp.net').replace(/\/$/, '');
const COMPANY = arg('company', 'Systems Limited Demo');
const DOMAIN = arg('domain', 'sysltd-demo.example');

function ownerLogin() {
  if (process.env.QV_OWNER_EMAIL && process.env.QV_OWNER_PASSWORD)
    return { email: process.env.QV_OWNER_EMAIL, password: process.env.QV_OWNER_PASSWORD };
  const file = join(here, 'secrets', 'production.json');
  if (!existsSync(file)) throw new Error('No owner login: set QV_OWNER_EMAIL and QV_OWNER_PASSWORD, or create deploy/secrets/production.json.');
  const s = JSON.parse(readFileSync(file, 'utf8'));
  return { email: s.OwnerEmail, password: s.OwnerPassword };
}

// ---------- HTTP ----------

class Api {
  constructor(token) {
    this.token = token;
  }
  async call(method, path, body, headers = {}) {
    const isForm = body instanceof FormData;
    const res = await fetch(`${BASE}/api${path}`, {
      method,
      headers: {
        ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
        ...(body && !isForm ? { 'Content-Type': 'application/json' } : {}),
        ...headers,
      },
      body: body == null ? undefined : isForm ? body : JSON.stringify(body),
    });
    const text = await res.text();
    if (!res.ok) throw new Error(`${method} ${path} → ${res.status} ${text.slice(0, 400)}`);
    return text ? JSON.parse(text) : null;
  }
  get = (p) => this.call('GET', p);
  post = (p, b, h) => this.call('POST', p, b ?? {}, h);
  put = (p, b) => this.call('PUT', p, b);
}

async function signIn(email, password) {
  const s = await new Api().post('/auth/login', { email, password });
  const api = new Api(s.token);
  api.me = s.user;
  return api;
}

// ---------- Helpers ----------

let seed = 20261026;
const rand = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
const pick = (xs) => xs[Math.floor(rand() * xs.length)];
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (d, n) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() + n));
const pad = (n) => String(n).padStart(2, '0');

// Company-local "today" (Pakistan, UTC+5).
const TODAY = (() => {
  const now = new Date(Date.now() + 5 * 3600_000);
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
})();
const isWeekend = (d) => d.getUTCDay() === 0 || d.getUTCDay() === 6;
// Last month is the payroll that gets finalized; attendance covers it and this month so far.
const PAY = (() => {
  const d = new Date(Date.UTC(TODAY.getUTCFullYear(), TODAY.getUTCMonth() - 1, 1));
  return { year: d.getUTCFullYear(), month: d.getUTCMonth() + 1, first: d };
})();

const failures = [];
async function step(name, fn) {
  process.stdout.write(`• ${name} … `);
  try {
    const r = await fn();
    console.log('ok');
    return r;
  } catch (e) {
    console.log('FAILED');
    console.log(`    ${e.message}`);
    failures.push(name);
    return undefined;
  }
}

// ---------- Data ----------

const DEPARTMENTS = ['Management', 'Engineering', 'Quality Assurance', 'Human Resources', 'Finance', 'Sales & Marketing', 'Project Delivery', 'IT Support'];
const DESIGNATIONS = [
  'Chief Executive Officer', 'Head of Engineering', 'Engineering Manager', 'Senior Software Engineer', 'Software Engineer', 'Associate Software Engineer',
  'QA Lead', 'SQA Engineer', 'HR Manager', 'HR Executive', 'Finance Manager', 'Accountant', 'Sales Manager', 'Business Development Executive',
  'Project Manager', 'IT Support Engineer',
];

// key, first, last, gender, department, designation, manager key, basic salary, joined, blood group
const PEOPLE = [
  ['ceo', 'Asif', 'Peer', 'Male', 'Management', 'Chief Executive Officer', null, 1500000, '2012-03-01', 'B+'],
  ['hoe', 'Kamran', 'Siddiqui', 'Male', 'Engineering', 'Head of Engineering', 'ceo', 900000, '2014-07-15', 'O+'],
  ['em', 'Usman', 'Tariq', 'Male', 'Engineering', 'Engineering Manager', 'hoe', 550000, '2017-02-01', 'A+'],
  ['em2', 'Ayesha', 'Malik', 'Female', 'Engineering', 'Engineering Manager', 'hoe', 540000, '2018-05-21', 'AB+'],
  ['se1', 'Ali', 'Raza', 'Male', 'Engineering', 'Senior Software Engineer', 'em', 380000, '2020-10-26', 'B+'],
  ['se2', 'Hamza', 'Iqbal', 'Male', 'Engineering', 'Senior Software Engineer', 'em', 360000, '2021-01-11', 'O-'],
  ['se3', 'Fatima', 'Noor', 'Female', 'Engineering', 'Software Engineer', 'em', 240000, '2022-08-01', 'A-'],
  ['se4', 'Bilal', 'Ahmed', 'Male', 'Engineering', 'Software Engineer', 'em', 230000, '2023-03-06', 'B+'],
  ['se5', 'Zainab', 'Hussain', 'Female', 'Engineering', 'Associate Software Engineer', 'em', 150000, '2025-07-01', 'O+'],
  ['se6', 'Hassan', 'Javed', 'Male', 'Engineering', 'Senior Software Engineer', 'em2', 370000, '2019-09-16', 'AB-'],
  ['se7', 'Maryam', 'Khalid', 'Female', 'Engineering', 'Software Engineer', 'em2', 225000, '2023-10-26', 'B-'],
  ['se8', 'Omer', 'Farooq', 'Male', 'Engineering', 'Associate Software Engineer', 'em2', 145000, '2025-09-01', 'A+'],
  ['qal', 'Saba', 'Qureshi', 'Female', 'Quality Assurance', 'QA Lead', 'hoe', 320000, '2018-11-05', 'O+'],
  ['qa1', 'Danish', 'Ali', 'Male', 'Quality Assurance', 'SQA Engineer', 'qal', 180000, '2022-02-14', 'B+'],
  ['qa2', 'Hira', 'Shah', 'Female', 'Quality Assurance', 'SQA Engineer', 'qal', 170000, '2024-01-08', 'A+'],
  ['hrm', 'Sana', 'Butt', 'Female', 'Human Resources', 'HR Manager', 'ceo', 420000, '2016-04-18', 'B+'],
  ['hre', 'Rabia', 'Anwar', 'Female', 'Human Resources', 'HR Executive', 'hrm', 140000, '2023-06-12', 'O+'],
  ['fm', 'Imran', 'Sheikh', 'Male', 'Finance', 'Finance Manager', 'ceo', 480000, '2015-01-05', 'A+'],
  ['acc', 'Nadia', 'Akram', 'Female', 'Finance', 'Accountant', 'fm', 160000, '2021-11-22', 'AB+'],
  ['sm', 'Faisal', 'Mirza', 'Male', 'Sales & Marketing', 'Sales Manager', 'ceo', 500000, '2016-09-01', 'O+'],
  ['bd1', 'Mehwish', 'Rana', 'Female', 'Sales & Marketing', 'Business Development Executive', 'sm', 190000, '2022-05-02', 'B+'],
  ['bd2', 'Shoaib', 'Akhtar', 'Male', 'Sales & Marketing', 'Business Development Executive', 'sm', 185000, '2024-04-15', 'A-'],
  ['pm', 'Adeel', 'Chaudhry', 'Male', 'Project Delivery', 'Project Manager', 'ceo', 450000, '2017-08-14', 'B+'],
  ['pm2', 'Sadia', 'Imtiaz', 'Female', 'Project Delivery', 'Project Manager', 'pm', 400000, '2020-02-03', 'O-'],
  ['it1', 'Waqas', 'Younis', 'Male', 'IT Support', 'IT Support Engineer', 'hoe', 130000, '2021-07-19', 'A+'],
  ['it2', 'Junaid', 'Khan', 'Male', 'IT Support', 'IT Support Engineer', 'it1', 110000, '2024-10-01', 'B+'],
];

// Birthdays: two today, a few this week, the rest spread over the year.
function dateOfBirth(index) {
  const year = 1975 + (index * 7) % 26;
  if (index === 4 || index === 14) return `${year}-${pad(TODAY.getUTCMonth() + 1)}-${pad(TODAY.getUTCDate())}`;
  if (index % 6 === 1) {
    const d = addDays(TODAY, 1 + (index % 5));
    return `${year}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
  }
  return `${year}-${pad(1 + (index * 5) % 12)}-${pad(1 + (index * 11) % 28)}`;
}

// ---------- Existing company ----------

// Picks people for the demo roles by title, gives everyone a salary and punch code if missing, and puts two
// birthdays on today so the dashboard has something to celebrate.
async function useExisting(admin) {
  const list = [];
  for (let page = 1; ; page++) {
    const r = await admin.get(`/employees?page=${page}&pageSize=200`);
    list.push(...r.items);
    if (list.length >= r.total || !r.items.length) break;
  }
  const people = [];
  for (const e of list) people.push(await admin.get(`/employees/${e.id}`));
  const used = new Set();
  const take = (test) => {
    const p = people.find((x) => !used.has(x.id) && test(x));
    if (p) used.add(p.id);
    return p?.id;
  };
  const title = (t) => (x) => (x.designation ?? '').toLowerCase().includes(t.toLowerCase());
  const reportsTo = (id) => (x) => x.managerId === id;
  const keys = {};
  keys.ceo = take(title('Chief Executive'));
  keys.hoe = take(title('VP Engineering')) ?? take(title('Head'));
  keys.em = take(title('Team Lead - Web')) ?? take(title('Team Lead'));
  keys.em2 = take(title('Team Lead - Backend')) ?? take(title('Team Lead'));
  for (const k of ['se1', 'se2', 'se3', 'se4', 'se5']) keys[k] = take(reportsTo(keys.em)) ?? take(title('Software Engineer'));
  for (const k of ['se6', 'se7', 'se8']) keys[k] = take(reportsTo(keys.em2)) ?? take(title('Software Engineer'));
  keys.qal = take(title('Head of Quality')) ?? take(title('QA Lead'));
  keys.qa1 = take(title('SQA Engineer'));
  keys.qa2 = take(title('SQA Engineer'));
  keys.hrm = take(title('HR Manager'));
  keys.hre = take(title('HR Executive')) ?? take(title('HR'));
  keys.fm = take(title('Chief Financial')) ?? take(title('Finance'));
  keys.acc = take(title('Accountant'));
  keys.sm = take(title('Director Sales')) ?? take(title('Sales'));
  keys.bd1 = take(title('Business Development'));
  keys.bd2 = take(title('Business Development'));
  keys.pm = take(title('Head of Product'));
  keys.pm2 = take(title('Product Manager'));
  keys.it1 = take(title('IT & Admin Manager')) ?? take(title('IT'));
  keys.it2 = take(title('System Administrator')) ?? take(title('Network'));
  for (const k of Object.keys(keys)) keys[k] ??= take(() => true);

  const level = (t = '') =>
    /chief|ceo|cfo|coo/i.test(t) ? 1200000 : /vp|director|head/i.test(t) ? 750000 : /manager|lead/i.test(t) ? 450000
      : /senior/i.test(t) ? 330000 : /associate|assistant|executive/i.test(t) ? 140000 : 220000;
  const punchers = [];
  for (const [i, p] of people.entries()) {
    if (!(await admin.call('GET', `/employees/${p.id}/salary`))) {
      const basic = level(p.designation);
      await admin.put(`/employees/${p.id}/salary`, {
        effectiveFrom: p.joiningDate, basicSalary: Math.round(basic * 0.6), eobiApplicable: true,
        components: [
          { name: 'House rent', kind: 'Earning', amount: Math.round(basic * 0.3), isTaxable: true },
          { name: 'Utilities', kind: 'Earning', amount: Math.round(basic * 0.1), isTaxable: true },
        ],
      });
    }
    const file = await admin.get(`/employees/${p.id}/file`);
    let code = file?.punchCode;
    if (!code) {
      code = String(5000 + i);
      await admin.put(`/employees/${p.id}/file`, { ...(file ?? {}), punchCode: code });
    }
    punchers.push([p.id, code]);
  }
  // Two birthdays today.
  for (const id of [keys.se1, keys.qal]) {
    const p = people.find((x) => x.id === id);
    const dob = `${(p.dateOfBirth ?? '1992-01-01').slice(0, 4)}-${pad(TODAY.getUTCMonth() + 1)}-${pad(TODAY.getUTCDate())}`;
    await admin.put(`/employees/${id}`, { ...p, dateOfBirth: dob });
  }
  return { keys, punchers };
}

// ---------- Run ----------

async function main() {
  console.log(`Seeding demo data on ${BASE}\n`);
  const owner = ownerLogin();
  const first = await signIn(owner.email, owner.password);
  const password = `Demo-${randomBytes(6).toString('base64url')}-26`;
  const ownCompany = first.me.role !== 'SuperAdmin' && first.me.role !== 0;
  let admin, adminEmail;

  if (ownCompany) {
    // A company admin's login: fill that company.
    admin = first;
    adminEmail = `${first.me.email} (your own password)`;
    console.log(`Signed in to "${first.me.companyName}" as ${first.me.email}\n`);
    if ((await admin.get('/biometric/devices')).some((d) => d.name === 'Head Office Main Gate')) {
      console.log('Demo data is already in this company. Nothing was changed.');
      process.exit(1);
    }
  } else {
    const existing = (await first.get('/platform/companies')).find((c) => c.name.toLowerCase() === COMPANY.toLowerCase());
    if (existing) {
      console.log(`"${COMPANY}" already exists. Nothing was changed. Delete it from the platform screen first to seed again.`);
      process.exit(1);
    }
    adminEmail = `admin@${DOMAIN}`;
    const created = await step('Company and its admin', () =>
      first.post('/platform/companies', {
        name: COMPANY, adminFirstName: 'Demo', adminLastName: 'Admin', adminEmail, adminPassword: password,
        phone: '+92 42 111 797 836', city: 'Lahore', address: 'E-1, Sehjpal Road, Near DHA Phase VIII, Lahore Cantt', maxEmployees: 200,
      }),
    );
    if (!created) throw new Error('Could not create the company.');
    admin = await signIn(adminEmail, password);
  }

  // Organization
  const dept = {};
  await step('Departments', async () => {
    for (const d of await admin.get('/departments')) dept[d.name] = d.id;
    for (const name of DEPARTMENTS) dept[name] ??= (await admin.post('/departments', { name })).id;
  });
  const desig = {};
  await step('Designations', async () => {
    // A new company already has a few titles; reuse those.
    for (const d of await admin.get('/designations')) desig[d.title] = d.id;
    for (const title of DESIGNATIONS) desig[title] ??= (await admin.post('/designations', { title })).id;
  });
  const shift = await step('Office shift (9 to 6, Sat/Sun off)', async () =>
    (await admin.get('/setup/shifts')).find((x) => x.isDefault) ??
    admin.post('/setup/shifts', { name: 'General 9-6', startTime: '09:00:00', endTime: '18:00:00', graceMinutes: 15, weeklyOffDays: ['Saturday', 'Sunday'], isDefault: true, breakMinutes: 60 }),
  );
  if (!ownCompany) await step('Office location', () => admin.post('/setup/office-locations', { name: 'Head Office Lahore', latitude: 31.4697, longitude: 74.4091, radiusMeters: 300 }));

  // People
  const emp = {};
  dept.Engineering ??= dept['Software Engineering'];
  // Everyone whose attendance the machine records: [employeeId, punch code].
  let punchers = PEOPLE.map((_, i) => [null, String(1001 + i)]);
  if (ownCompany) {
    const roster = await step('Existing people: salaries, punch codes, birthdays', () => useExisting(admin));
    if (!roster) throw new Error('Could not read the people in this company.');
    Object.assign(emp, roster.keys);
    punchers = roster.punchers;
  } else await step(`${PEOPLE.length} employees with salaries`, async () => {
    for (const [i, [key, first, last, gender, d, title, mgr, basic, joined, blood]] of PEOPLE.entries()) {
      const e = await admin.post('/employees', {
        firstName: first, lastName: last, joiningDate: joined, status: i % 9 === 8 ? 'Probation' : 'Active',
        email: `${first}.${last}@${DOMAIN}`.toLowerCase(), phone: `+92 3${pad(i + 10)} ${1000000 + i * 7919}`,
        cnic: `35202-${String(1000000 + i * 104729).slice(0, 7)}-${i % 10}`, dateOfBirth: dateOfBirth(i), gender,
        departmentId: dept[d], designationId: desig[title], managerId: mgr ? emp[mgr] : null, shiftId: shift?.id,
        bankName: pick(['Meezan Bank', 'HBL', 'Bank Alfalah', 'MCB']), bankAccount: `PK${pad(i + 11)}MEZN000${1234567890 + i}`,
        address: `${pick(['DHA Phase 5', 'Johar Town', 'Model Town', 'Gulberg III', 'Bahria Town'])}, Lahore`, bloodGroup: blood,
        maritalStatus: i % 3 ? 'Married' : 'Single',
      });
      emp[key] = e.id;
      punchers[i][0] = e.id;
      await admin.put(`/employees/${e.id}/salary`, {
        effectiveFrom: joined, basicSalary: Math.round(basic * 0.6), eobiApplicable: true,
        components: [
          { name: 'House rent', kind: 'Earning', amount: Math.round(basic * 0.3), isTaxable: true },
          { name: 'Utilities', kind: 'Earning', amount: Math.round(basic * 0.1), isTaxable: true },
          ...(basic >= 300000 ? [{ name: 'Fuel', kind: 'Earning', amount: 25000, isTaxable: false }] : []),
        ],
      });
      await admin.put(`/employees/${e.id}/file`, { punchCode: String(1001 + i) });
    }
  });
  if (!ownCompany) await step('Department heads', async () => {
    const heads = { Management: 'ceo', Engineering: 'hoe', 'Quality Assurance': 'qal', 'Human Resources': 'hrm', Finance: 'fm', 'Sales & Marketing': 'sm', 'Project Delivery': 'pm', 'IT Support': 'it1' };
    for (const [name, key] of Object.entries(heads)) await admin.put(`/departments/${dept[name]}`, { name, headEmployeeId: emp[key] });
  });

  // Logins for the people you sign in as during a demo.
  const LOGINS = [['hrm', 'HrManager'], ['em', 'Manager'], ['se1', 'Employee'], ['se2', 'Employee']];
  const loginEmail = (key) => {
    const p = PEOPLE.find((x) => x[0] === key);
    return `${p[1]}.${p[2]}@${DOMAIN}`.toLowerCase();
  };
  // An existing company keeps its own logins; the admin then acts for people where the API allows it.
  let hr = admin, manager = admin, ali = null, hamza = null;
  if (!ownCompany) {
    await step('Logins (HR, manager, two employees)', async () => {
      for (const [key, role] of LOGINS) await admin.post(`/employees/${emp[key]}/login`, { email: loginEmail(key), password, role });
    });
    hr = await signIn(loginEmail('hrm'), password);
    manager = await signIn(loginEmail('em'), password);
    ali = await signIn(loginEmail('se1'), password);
    hamza = await signIn(loginEmail('se2'), password);
  }

  // Calendar
  await step('Public holidays', async () => {
    const y = TODAY.getUTCFullYear();
    const days = [
      ['02-05', 'Kashmir Solidarity Day'], ['03-23', 'Pakistan Day'], ['05-01', 'Labour Day'], ['08-14', 'Independence Day'],
      ['11-09', 'Iqbal Day'], ['12-25', 'Quaid-e-Azam Day'], ['03-20', 'Eid ul Fitr'], ['03-21', 'Eid ul Fitr'], ['05-27', 'Eid ul Adha'],
      ['05-28', 'Eid ul Adha'], ['06-25', 'Ashura'], ['08-26', 'Eid Milad un Nabi'],
    ];
    const have = new Set((await admin.get(`/setup/holidays?year=${y}`)).map((h) => h.date));
    for (const [md, name] of days) if (!have.has(`${y}-${md}`)) await admin.post('/setup/holidays', { date: `${y}-${md}`, name, kind: 'Public' });
  });
  const holidays = new Set((await admin.get(`/setup/holidays?year=${TODAY.getUTCFullYear()}`)).map((h) => h.date));

  const leaveTypes = {};
  await step('Leave types', async () => {
    for (const t of await admin.get('/setup/leave-types')) leaveTypes[t.code] = t.id;
    for (const t of [
      { name: 'Annual Leave', code: 'AL', annualQuota: 14, isPaid: true, isActive: true, carryForwardMax: 7, minNoticeDays: 0 },
      { name: 'Casual Leave', code: 'CL', annualQuota: 10, isPaid: true, isActive: true, allowShortLeave: true },
      { name: 'Sick Leave', code: 'SL', annualQuota: 8, isPaid: true, isActive: true, documentAfterDays: 3 },
    ])
      leaveTypes[t.code] ??= (await admin.post('/setup/leave-types', t)).id;
  });

  // Leave: approved last month, some pending and one rejected this month and next.
  const onLeave = new Set();
  const workdays = (from, n) => {
    const out = [];
    for (let d = from; out.length < n; d = addDays(d, 1)) if (!isWeekend(d) && !holidays.has(iso(d))) out.push(d);
    return out;
  };
  await step('Leave requests', async () => {
    const plan = [
      ['se3', 'AL', addDays(PAY.first, 7), 3, 'approve', 'Family wedding in Faisalabad'],
      ['qa1', 'SL', addDays(PAY.first, 14), 2, 'approve', 'Flu'],
      ['bd1', 'CL', addDays(PAY.first, 21), 1, 'approve', 'Bank work'],
      ['se6', 'AL', addDays(TODAY, -12), 2, 'approve', 'Visiting parents'],
      ['se4', 'CL', addDays(TODAY, -5), 1, 'reject', 'Personal errand'],
      ['se1', 'AL', addDays(TODAY, 10), 3, 'pending', 'Trip to Hunza'],
      ['se5', 'CL', addDays(TODAY, 3), 1, 'pending', 'University convocation'],
      ['qa2', 'SL', addDays(TODAY, 1), 1, 'pending', 'Dentist appointment'],
    ];
    for (const [key, code, start, n, decision, reason] of plan) {
      const days = workdays(start, n);
      const l = await admin.post('/leave', { leaveTypeId: leaveTypes[code], fromDate: iso(days[0]), toDate: iso(days.at(-1)), reason, employeeId: emp[key] });
      if (decision === 'approve') {
        await admin.post(`/leave/${l.id}/approve`, { note: 'Enjoy' });
        for (const d of days) onLeave.add(`${emp[key]}|${iso(d)}`);
      } else if (decision === 'reject') await admin.post(`/leave/${l.id}/reject`, { note: 'Release week, please move it' });
    }
  });

  // Attendance from a biometric machine: last month and this month up to yesterday.
  const device = await step('Biometric machine', () => admin.post('/biometric/devices', { name: 'Head Office Main Gate', serialNumber: 'ZK-K40-7781', location: 'Ground floor' }));
  await step('Machine punches for last month and this month', async () => {
    const punches = [];
    for (let d = PAY.first; d < TODAY; d = addDays(d, 1)) {
      if (isWeekend(d) || holidays.has(iso(d))) continue;
      punchers.forEach(([id, code]) => {
        if (onLeave.has(`${id}|${iso(d)}`) || rand() < 0.03) return; // leave or absent
        const late = rand() < 0.12;
        const inMin = late ? 20 + Math.floor(rand() * 50) : Math.floor(rand() * 18) - 10;
        const outMin = Math.floor(rand() * 75) - (rand() < 0.05 ? 120 : 0);
        const at = (h, m) => `${iso(d)}T${pad(h + Math.floor(m / 60))}:${pad(((m % 60) + 60) % 60)}:00`;
        punches.push({ punchCode: code, time: inMin < 0 ? at(8, 60 + inMin) : at(9, inMin) });
        punches.push({ punchCode: code, time: at(18, outMin < 0 ? 0 : outMin) });
      });
    }
    for (let i = 0; i < punches.length; i += 4000) await new Api().post('/biometric/punches', { punches: punches.slice(i, i + 4000) }, { 'X-Device-Token': device.token });
  });

  // Payroll for last month, finalized, with a chart of accounts mapped for the journal.
  await step('Payroll settings and chart of accounts', async () => {
    const accounts = {};
    for (const a of await admin.get('/payroll/gl/accounts')) accounts[a.code] = a.id;
    for (const [code, name] of [['5100', 'Salaries & Wages'], ['5110', 'Allowances'], ['5120', 'Employer EOBI'], ['2100', 'Salaries Payable'], ['2110', 'Income Tax Payable'], ['2120', 'EOBI Payable'], ['2130', 'Other Deductions Payable']])
      accounts[code] ??= (await admin.post('/payroll/gl/accounts', { code, name })).id;
    const mapping = await admin.get('/payroll/gl/mapping');
    const accountFor = (line) => {
      const l = line.toLowerCase();
      if (l.includes('tax')) return accounts['2110'];
      if (l.includes('eobi') && l.includes('employer')) return accounts['5120'];
      if (l.includes('eobi') || l.includes('social')) return accounts['2120'];
      if (l.includes('net') || l.includes('payable') || l.includes('bank') || l.includes('cash')) return accounts['2100'];
      if (l.includes('allowance') || l.includes('overtime') || l.includes('bonus') || l.includes('arrear')) return accounts['5110'];
      if (l.includes('deduction') || l.includes('loan') || l.includes('advance') || l.includes('fund') || l.includes('penalt')) return accounts['2130'];
      return accounts['5100'];
    };
    await admin.put('/payroll/gl/mapping', { items: mapping.map((m) => ({ line: m.line, glAccountId: accountFor(m.line) })) });
  });
  const run = await step(`Payroll ${PAY.year}-${pad(PAY.month)} processed`, async () => (await admin.post('/payroll/runs', { year: PAY.year, month: PAY.month })).run);
  if (run) {
    await step('Payroll finalized and marked paid', async () => {
      await admin.post(`/payroll/runs/${run.id}/finalize`);
      await admin.post(`/payroll/runs/${run.id}/disburse`, { via: 'Meezan Bank bulk transfer' });
    });
  }

  // Announcements and a poll
  await step('Announcements and a poll', async () => {
    await admin.post('/announcements', { title: 'Welcome to Qaventra', body: 'All leave, attendance and payslips now live in one place. Install the app on your phone from the browser menu.', kind: 'News', publishFrom: iso(addDays(TODAY, -6)), isPinned: true, notify: false });
    await admin.post('/announcements', { title: 'Updated remote work policy', body: 'Engineering teams may work from home on Fridays. Please mark attendance from the app before 10 am.', kind: 'Policy', publishFrom: iso(addDays(TODAY, -3)), isPinned: false, notify: false });
    await admin.post('/announcements', { title: 'Annual family day', body: 'Join us at Jilani Park on Saturday, 14 November, from 4 pm. Families welcome!', kind: 'Event', publishFrom: iso(TODAY), isPinned: false, notify: false });
    await admin.post('/announcements/polls', { question: 'Which day suits the monthly town hall?', opensOn: iso(TODAY), closesOn: iso(addDays(TODAY, 5)), isAnonymous: false, resultsVisibility: 'AfterVoting', options: ['First Monday', 'Last Thursday', 'Mid-month Wednesday'] });
  });

  await step('Birthday wish', async () => {
    const today = (await admin.get('/dashboard/celebrations')).filter((c) => c.isToday ?? c.kind === 'Birthday');
    if (today[0]) await hr.post(`/dashboard/celebrations/${today[0].employeeId}/wish`, { kind: today[0].kind ?? 'Birthday' });
  });

  // Performance: KPI library, a launched cycle, goals, feedback and check-ins.
  await step('KPI library', async () => {
    for (const k of [
      { name: 'Story points delivered', category: 'Engineering', unit: 'points', defaultTarget: '40 per sprint', defaultWeight: 30 },
      { name: 'Production defects', category: 'Engineering', unit: 'count', defaultTarget: 'Under 3 per quarter', defaultWeight: 20 },
      { name: 'Code review turnaround', category: 'Engineering', unit: 'hours', defaultTarget: 'Within 24', defaultWeight: 15 },
      { name: 'Test coverage', category: 'Quality', unit: '%', defaultTarget: '80', defaultWeight: 20 },
      { name: 'Revenue closed', category: 'Sales', unit: 'PKR', defaultTarget: '25,000,000', defaultWeight: 40 },
      { name: 'Client satisfaction (CSAT)', category: 'Delivery', unit: 'score', defaultTarget: '4.5 / 5', defaultWeight: 25 },
    ])
      await admin.post('/performance/kpis', k);
  });
  let reviewId;
  await step('Performance cycle launched', async () => {
    await admin.post('/performance/templates', {
      name: 'Engineering half-year review', description: 'Goals plus the core competencies', selfReview: true,
      sections: [
        { name: 'Goals', weight: 70, kind: 'Goals', items: [] },
        { name: 'Competencies', weight: 30, kind: 'Competencies', items: [{ title: 'Ownership' }, { title: 'Communication' }, { title: 'Technical depth' }] },
      ],
    });
    const template = (await admin.get('/performance/templates')).find((t) => t.name === 'Engineering half-year review').id;
    const y = TODAY.getUTCFullYear();
    await admin.post('/performance/cycles', {
      name: `H2 ${y}`, periodFrom: `${y}-07-01`, periodTo: `${y}-12-31`, templateId: template, departmentIds: [dept.Engineering, dept['Quality Assurance']],
      goalsDue: iso(addDays(TODAY, 7)), selfReviewDue: `${y}-12-15`, managerReviewDue: `${y}-12-24`,
    });
    const cycle = (await admin.get('/performance/cycles')).find((c) => c.name === `H2 ${y}`).id;
    await admin.post(`/performance/cycles/${cycle}/launch`);
    reviewId = ali ? (await ali.get('/performance/reviews/my'))[0].id : (await admin.get('/performance/reviews/team')).find((r) => r.employeeId === emp.se1)?.id;
    if (!ali) return;
    await ali.put(`/performance/reviews/${reviewId}/goals`, {
      goals: [
        { sectionIndex: 0, title: 'Ship the payments microservice', target: 'Live by 30 Nov', weight: 40 },
        { sectionIndex: 0, title: 'Story points delivered', target: '40 per sprint', weight: 30 },
        { sectionIndex: 0, title: 'Mentor two associate engineers', target: 'Weekly pairing', weight: 30 },
      ],
    });
    await ali.post(`/performance/reviews/${reviewId}/goals/submit`);
  });
  if (reviewId) {
    await step('Peer feedback and check-ins', async () => {
      await manager.post(`/performance/reviews/${reviewId}/feedback`, { employeeIds: [emp.se2, emp.se3] });
      if (hamza) {
        const req = (await hamza.get('/performance/feedback/mine'))[0];
        await hamza.put(`/performance/feedback/${req.id}`, { rating: 5, comment: 'Unblocked the team twice during the release and reviews code quickly.' });
      }
      if (ali) await ali.post(`/performance/reviews/${reviewId}/check-ins`, { period: `Q3 ${TODAY.getUTCFullYear()}`, progress: 55, note: 'Payments service API done; integration tests in progress.' });
      await manager.post(`/performance/reviews/${reviewId}/check-ins`, { period: `Q3 ${TODAY.getUTCFullYear()}`, note: 'Good pace. Keep an eye on the load-test results.' });
    });
  }

  // Hiring
  await step('Hiring: jobs, candidates, interview template', async () => {
    await admin.post('/recruitment-extras/interview-templates', {
      name: 'Backend engineer technical round',
      questions: [
        { text: 'Design a rate limiter for a public API', category: 'System design', maxMarks: 10 },
        { text: 'Explain database indexing trade-offs', category: 'Databases', maxMarks: 10 },
        { text: 'Live coding: parse and validate a CNIC', category: 'Coding', maxMarks: 10 },
        { text: 'Communication and attitude', category: 'Culture', maxMarks: 5 },
      ],
    });
    await admin.post('/recruitment/requests', { title: 'DevOps Engineer', departmentId: dept.Engineering, designationId: desig['Software Engineer'], positions: 1, reason: 'Cloud migration project for a banking client' });
    const jobs = [
      { title: 'Senior .NET Developer', department: 'Engineering', designation: 'Senior Software Engineer', min: 300000, max: 450000, positions: 2 },
      { title: 'SQA Engineer', department: 'Quality Assurance', designation: 'SQA Engineer', min: 150000, max: 220000, positions: 1 },
    ];
    const candidates = [
      ['Ahmad', 'Nawaz', 'Software Engineer', 'NetSol Technologies', 4, 'Interview'], ['Iqra', 'Aziz', 'Senior Developer', 'Arbisoft', 6, 'Offer'],
      ['Talha', 'Saeed', 'Developer', '10Pearls', 3, 'Shortlisted'], ['Komal', 'Riaz', 'Software Engineer', 'Techlogix', 5, 'Applied'],
      ['Usama', 'Bashir', 'QA Engineer', 'Contour Software', 3, 'Interview'], ['Sidra', 'Latif', 'SQA', 'VentureDive', 2, 'Applied'],
    ];
    for (const [j, job] of jobs.entries()) {
      const post = await admin.post('/recruitment/jobs', {
        title: job.title, departmentId: dept[job.department], designationId: desig[job.designation], location: 'Lahore (hybrid)', employmentType: 'FullTime',
        description: `We are hiring a ${job.title} to build enterprise software for banks and telecoms in Pakistan and the Gulf.`,
        requirements: 'Strong fundamentals, clean code, and good English communication.', salaryMin: job.min, salaryMax: job.max, showSalary: false,
        positions: job.positions, closesOn: iso(addDays(TODAY, 30)), hiringManagerEmployeeId: j === 0 ? emp.em : emp.qal,
      });
      await admin.post(`/recruitment/jobs/${post.id}/status`, { status: 'Open' });
      for (const [first, last, title, company, years, stage] of candidates.slice(j * 4, j * 4 + 4)) {
        const f = new FormData();
        for (const [k, v] of Object.entries({ FirstName: first, LastName: last, Email: `${first}.${last}@candidates.example`.toLowerCase(), Phone: '+92 300 0000000', City: 'Lahore', CurrentTitle: title, CurrentCompany: company, ExperienceYears: years, ExpectedSalary: job.min + 50000, NoticeDays: 30, Skills: j === 0 ? 'C#, .NET, SQL Server, Azure' : 'Selenium, Postman, JMeter', Source: 'Manual' }))
          f.append(k, String(v));
        const card = await admin.post(`/recruitment/jobs/${post.id}/candidates`, f);
        const appId = card.applicationId ?? card.id;
        const path = ['Shortlisted', 'Interview'];
        for (const s of path.slice(0, stage === 'Offer' ? 2 : path.indexOf(stage) + 1)) await admin.post(`/recruitment/applications/${appId}/move`, { stage: s, emailCandidate: false });
        if (stage === 'Offer')
          await admin.post(`/recruitment/applications/${appId}/offer`, { salary: job.min + 80000, joiningDate: iso(addDays(TODAY, 35)), note: 'Hybrid, 3 days in office', emailCandidate: false });
        if (stage === 'Interview')
          await admin.post(`/recruitment/applications/${appId}/interviews`, {
            title: 'Technical interview', startsAtUtc: `${iso(addDays(TODAY, 2))}T06:00:00Z`, durationMinutes: 60, mode: 'Online', where: 'Google Meet',
            interviewerIds: [j === 0 ? emp.em : emp.qal], emailCandidate: false,
          });
      }
    }
  });

  // Training
  await step('Training programme and session', async () => {
    await admin.post('/training/categories', { name: 'Technical' });
    await admin.post('/training/categories', { name: 'Compliance' });
    const cats = await admin.get('/training/categories');
    const catId = (n) => cats.find((c) => c.name === n)?.id;
    await admin.post('/training/programs', { title: 'Azure Fundamentals (AZ-900)', categoryId: catId('Technical'), mode: 'Classroom', durationHours: 8, isMandatory: false, description: 'Cloud basics for every engineer.' });
    await admin.post('/training/programs', { title: 'Information Security Awareness', categoryId: catId('Compliance'), mode: 'Online', durationHours: 2, isMandatory: true, completeWithinDays: 30, repeatEveryMonths: 12, link: 'https://example.com/infosec' });
    const program = (await admin.get('/training/programs')).find((p) => p.title.startsWith('Azure'));
    const s = await admin.post('/training/sessions', { programId: program.id, startsAtUtc: `${iso(addDays(TODAY, 9))}T05:00:00Z`, endsAtUtc: `${iso(addDays(TODAY, 9))}T13:00:00Z`, venue: 'Training Room 2, Head Office', trainerEmployeeId: emp.hoe, capacity: 20 });
    await admin.post(`/training/sessions/${s.id}/enroll`, { employeeIds: [emp.se1, emp.se3, emp.se4, emp.se5, emp.se7, emp.se8, emp.qa1].filter(Boolean) });
  });

  // Helpdesk
  await step('Helpdesk categories and tickets', async () => {
    await admin.post('/helpdesk/categories', { name: 'IT support', description: 'Laptops, VPN, email and accounts', targetHours: 8, agentIds: [emp.it1, emp.it2], assignmentMode: 'RoundRobin', agentCanClose: true });
    await admin.post('/helpdesk/categories', { name: 'HR & payroll queries', description: 'Payslips, leave balance, letters', targetHours: 24, agentIds: [emp.hrm, emp.hre], assignmentMode: 'Manual' });
    const cats = await admin.get('/helpdesk/categories');
    const it = cats.find((c) => c.name === 'IT support').id;
    const hrq = cats.find((c) => c.name.startsWith('HR')).id;
    const asPerson = (key) => (ali && key === 'se1' ? [ali, {}] : hamza && key === 'se2' ? [hamza, {}] : [admin, { forEmployeeId: emp[key] }]);
    const ticket = (key, body) => {
      const [api, extra] = asPerson(key);
      return api.post('/helpdesk/tickets', { ...body, ...extra });
    };
    await ticket('se1', { categoryId: it, subject: 'VPN disconnects every 10 minutes', description: 'Since Monday the FortiClient VPN drops every 10 minutes when working from home.', priority: 'High' });
    await ticket('se2', { categoryId: it, subject: 'Need a second monitor', description: 'Requesting a 24 inch monitor for my desk on the 3rd floor.', priority: 'Low' });
    await ticket('se1', { categoryId: hrq, subject: 'Tax certificate for last year', description: 'Please share my salary tax certificate for the previous tax year for my return.', priority: 'Medium' });
  });

  // Assets
  await step('Assets', async () => {
    for (const name of ['Laptop', 'Mobile phone', 'Access card']) await admin.post('/assets/types', { name, allowMultiple: false, returnOnExit: true, handlerIds: [emp.it1] });
    const types = await admin.get('/assets/types');
    const [laptop, phone, card] = ['Laptop', 'Mobile phone', 'Access card'].map((n) => types.find((t) => t.name === n).id);
    for (let i = 1; i <= 8; i++)
      await admin.post('/assets', { typeId: laptop, tag: `LT-${pad(i)}`, name: i <= 5 ? 'Dell Latitude 5440' : 'MacBook Pro 14"', serialNumber: `SN-LT-${7700 + i}`, purchasedOn: '2025-01-15', cost: i <= 5 ? 285000 : 520000, warrantyUntil: '2028-01-14', condition: 'Good' });
    for (let i = 1; i <= 3; i++) await admin.post('/assets', { typeId: phone, tag: `PH-${pad(i)}`, name: 'Samsung Galaxy A55', serialNumber: `SN-PH-${300 + i}`, purchasedOn: '2025-06-01', cost: 120000, condition: 'New' });
    for (let i = 1; i <= 10; i++) await admin.post('/assets', { typeId: card, tag: `AC-${pad(i)}`, name: 'HID access card', condition: 'New' });
    await admin.post('/assets/kits', { name: 'New engineer', typeIds: [laptop, card] });
    if (ali) await ali.post('/assets/requests/new', { typeId: phone, reason: 'On-call support for the banking client', returnBy: iso(addDays(TODAY, 60)) });
  });

  // Expenses
  await step('Expense claims', async () => {
    await (ali ?? admin).post('/expenses', {
      title: 'Client visit to Karachi',
      ...(ali ? {} : { employeeId: emp.se1 }),
      items: [
        { date: iso(addDays(TODAY, -9)), category: 'Travel', description: 'PIA return ticket LHE-KHI', amount: 48500 },
        { date: iso(addDays(TODAY, -9)), category: 'Accommodation', description: 'Hotel, 2 nights', amount: 36000 },
        { date: iso(addDays(TODAY, -8)), category: 'Meals', description: 'Team dinner with client', amount: 12500 },
      ],
    });
    await (hamza ?? admin).post('/expenses', { title: 'Fuel, October', ...(hamza ? {} : { employeeId: emp.se2 }), items: [{ date: iso(addDays(TODAY, -4)), category: 'Fuel', description: 'Office commute top-up', amount: 9000 }] });
  });

  // Company setup screens
  await step('Email template and security', async () => {
    await admin.put('/setup/email/templates/Announcement', { subject: '[Systems] {title}', intro: 'Dear {name}, here is an update from {company}.', enabled: true });
    // Limiting sign-in to the demo domain would lock out an existing company's own people.
    if (!ownCompany) await admin.put('/setup/security', { allowedEmailDomains: [DOMAIN] });
    await admin.post('/setup/api-tokens', { name: 'Power BI dashboard' });
  });

  if (ownCompany) return finish();
  // Write the logins down.
  const out = join(here, 'secrets', 'demo-logins.txt');
  const lines = [
    `${ownCompany ? first.me.companyName : COMPANY} on ${BASE}`,
    `Created ${new Date().toISOString()}`,
    '',
    `Password for the logins below (except the company admin): ${password}`,
    '',
    `Company admin   ${adminEmail}`,
    `HR manager      ${loginEmail('hrm')}`,
    `Manager         ${loginEmail('em')}`,
    `Employee        ${loginEmail('se1')}`,
    `Employee        ${loginEmail('se2')}`,
    '',
  ];
  writeFileSync(out, lines.join('\n'));
  console.log(`\nLogins saved to ${out}`);
  finish();
}

function finish() {
  if (failures.length) {
    console.log(`\n${failures.length} step(s) failed: ${failures.join(', ')}`);
    process.exit(2);
  }
  console.log('\nDone.');
}

main().catch((e) => {
  console.error(`\n${e.message}`);
  process.exit(1);
});
