#!/usr/bin/env node
// Adds demo data for the features built since 2026-11-04 (course quizzes and certificates too) to companies that were seeded earlier, signed in as their HR
// manager (from a logins file written by seed-demo.mjs). Safe to run again: it skips what is already there.
//
//   node deploy/seed-new-features.mjs --logins deploy/secrets/logins-alnoor.txt,deploy/secrets/logins-indus.txt
//   --base <url>   API server (default https://qaventra.runasp.net)

import { readFileSync } from 'node:fs';

const argv = process.argv.slice(2);
const arg = (n, f) => (argv.includes(`--${n}`) ? argv[argv.indexOf(`--${n}`) + 1] : f);
const BASE = arg('base', 'https://qaventra.runasp.net').replace(/\/$/, '');
const FILES = (arg('logins', '') || '').split(',').filter(Boolean);
if (!FILES.length) {
  console.error('Give --logins <file>[,<file>]');
  process.exit(1);
}

const today = new Date(Date.now() + 5 * 3600_000).toISOString().slice(0, 10);
const addDays = (iso, n) => new Date(Date.parse(iso) + n * 86_400_000).toISOString().slice(0, 10);

function readLogins(file) {
  const text = readFileSync(file, 'utf8');
  return {
    company: text.split('\n')[0].replace(/ on https?:.*$/, '').trim(),
    password: text.match(/Password for the logins below[^:]*:\s*(\S+)/)[1],
    hr: text.match(/HR manager\s+(\S+)/)[1],
    // People who can take a quiz: employees first, then the manager and HR.
    employees: ['Employee', 'Manager', 'HR manager'].flatMap((role) => [...text.matchAll(new RegExp(`^${role}\\s+(\\S+)`, 'gm'))].map((m) => m[1])),
  };
}

async function run(file) {
  const l = readLogins(file);
  let token = null;
  let failures = 0;
  const call = async (method, path, body, as = token) => {
    const res = await fetch(`${BASE}/api${path}`, {
      method,
      headers: { ...(as ? { Authorization: `Bearer ${as}` } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    const text = await res.text();
    let data = null;
    try {
      data = text ? JSON.parse(text) : null;
    } catch {
      data = text;
    }
    return { ok: res.ok, status: res.status, data };
  };
  const step = async (label, method, path, body) => {
    const r = await call(method, path, body);
    if (r.ok) console.log(`  ✓ ${label}`);
    else {
      failures++;
      console.log(`  ✗ ${label}: ${r.status} ${typeof r.data === 'object' ? r.data?.title ?? '' : String(r.data).slice(0, 120)}`);
    }
    return r;
  };

  console.log(`${l.company}`);
  const login = await call('POST', '/auth/login', { email: l.hr, password: l.password });
  if (!login.ok) throw new Error(`Could not sign in as ${l.hr} (${login.status})`);
  token = login.data.token;
  const me = (await call('GET', '/auth/me')).data;

  // Lists for banks, cities and nationalities.
  const lookups = (await call('GET', '/lookups')).data ?? [];
  const lists = {
    Bank: ['Meezan Bank', 'HBL', 'UBL', 'MCB Bank', 'Allied Bank', 'Bank Alfalah'],
    City: ['Karachi', 'Lahore', 'Islamabad', 'Rawalpindi', 'Faisalabad', 'Multan', 'Peshawar', 'Hyderabad'],
    Nationality: ['Pakistani', 'Afghan', 'British', 'American'],
  };
  for (const [kind, names] of Object.entries(lists))
    for (const name of names)
      if (!lookups.some((x) => x.kind === kind && x.name === name)) await step(`${kind}: ${name}`, 'POST', '/lookups', { kind, name, isActive: true });

  // Codes for designations that have none.
  const designations = (await call('GET', '/designations')).data ?? [];
  let n = 0;
  for (const d of designations.filter((x) => !x.code)) {
    const code = d.title.split(/\s+/).map((w) => w[0]).join('').toUpperCase().slice(0, 4) + '-' + String(++n).padStart(2, '0');
    await step(`Designation code ${d.title} → ${code}`, 'PUT', `/designations/${d.id}`, { title: d.title, code });
  }

  // Loan types.
  const loanTypes = (await call('GET', '/pay/loan-types')).data ?? [];
  const wantLoans = [
    { name: 'Personal loan', maxAmount: 300000, maxInstallments: 12, markupPercent: 0, processingFee: 0, minServiceMonths: 6, isActive: true },
    { name: 'Car loan', maxAmount: 1500000, maxInstallments: 36, markupPercent: 8, processingFee: 2500, minServiceMonths: 24, isActive: true },
    { name: 'Emergency loan', maxAmount: 100000, maxInstallments: 6, markupPercent: 0, processingFee: 0, minServiceMonths: 0, isActive: true },
  ];
  for (const t of wantLoans) if (!loanTypes.some((x) => x.name === t.name)) await step(`Loan type ${t.name}`, 'POST', '/pay/loan-types', t);

  // Expense types with quotas, a women-only type and a taxable type.
  const expenseTypes = (await call('GET', '/expense-setup/types')).data ?? [];
  const wantTypes = [
    { name: 'Fuel allowance', category: 'Fuel', calculation: 'Amount', yearlyLimit: 120000, monthlyLimit: 12000, proRata: true, carryForward: false },
    { name: 'OPD medical', category: 'Medical', calculation: 'Amount', yearlyLimit: 60000, monthlyLimit: null, proRata: true, carryForward: true, receiptRequired: true },
    { name: 'Maternity medical', category: 'Medical', calculation: 'Amount', yearlyLimit: 150000, forGender: 'Female', receiptRequired: true },
    { name: 'Client entertainment', category: 'Meals', calculation: 'Amount', perClaimLimit: 15000, monthlyLimit: 30000, taxable: true },
    { name: 'Mobile bill', category: 'Mobile', calculation: 'Amount', monthlyLimit: 3000 },
  ];
  for (const t of wantTypes)
    if (!expenseTypes.some((x) => x.name === t.name))
      await step(`Expense type ${t.name}`, 'POST', '/expense-setup/types', {
        receiptRequired: false, isActive: true, gradeIds: [], gradeRates: [], proRata: false, carryForward: false, taxable: false, ...t,
      });

  // Penalty rules scoped to probation staff and to women.
  const rules = (await call('GET', '/attendance-rules/penalties')).data ?? [];
  if (!rules.some((r) => r.name === 'Probation lates'))
    await step('Penalty rule for probation staff', 'POST', '/attendance-rules/penalties', {
      name: 'Probation lates', flag: 'Late', everyCount: 3, freeCount: 1, minMinutes: 15, repeat: true, action: 'DeductPay', days: 0.5, forStatus: 'Probation',
    });

  // Onboarding that needs approval, approved by HR.
  const templates = (await call('GET', '/onboarding/templates')).data ?? [];
  if (!templates.some((t) => t.name === 'Joiner with sign-off')) {
    await step('Onboarding template that needs approval', 'POST', '/onboarding/templates', {
      name: 'Joiner with sign-off', description: 'For roles where the department head signs off the joining.', isDefault: false, requiresApproval: true,
      welcomeMessage: 'Welcome aboard! Please bring your CNIC and two photographs on your first day.',
      tasks: [
        { title: 'Collect CNIC copy and photographs', category: 'Paperwork', assigneeKind: 'Hr', dueOffsetDays: 0 },
        { title: 'Laptop and email account', category: 'ItSetup', assigneeKind: 'Hr', dueOffsetDays: -1 },
        { title: 'First-week plan', category: 'Training', assigneeKind: 'LineManager', dueOffsetDays: 0 },
        { title: 'Upload degree and experience letters', category: 'Paperwork', assigneeKind: 'NewJoiner', dueOffsetDays: 3 },
      ],
    });
    await step('Onboarding approval chain: HR', 'PUT', '/approvals/flows/Onboarding', { steps: [{ kind: 'Hr' }] });
  }

  // A review cycle with a final review by the manager's manager.
  const cycles = (await call('GET', '/performance/cycles')).data ?? [];
  if (!cycles.some((c) => c.finalReview)) {
    const perfTemplates = (await call('GET', '/performance/templates')).data ?? [];
    if (perfTemplates[0]) {
      const name = `Year-end review ${today.slice(0, 4)}`;
      const created = await step(`Review cycle “${name}” with a final review`, 'POST', '/performance/cycles', {
        name, periodFrom: `${today.slice(0, 4)}-07-01`, periodTo: `${today.slice(0, 4)}-12-31`, templateId: perfTemplates[0].id, departmentIds: [],
        goalsDue: addDays(today, 10), selfReviewDue: addDays(today, 30), managerReviewDue: addDays(today, 45), finalReview: true,
      });
      if (created.ok) {
        const c = ((await call('GET', '/performance/cycles')).data ?? []).find((x) => x.name === name);
        if (c && c.status === 'Draft') await step('Launch it', 'POST', `/performance/cycles/${c.id}/launch`);
      }
    } else console.log('  - no performance template; review cycle skipped');
  }

  // Scheduled reports: last day of the month and every two weeks.
  const reports = (await call('GET', '/scheduled/reports')).data ?? [];
  const to = me?.employeeId ? [me.employeeId] : [];
  if (to.length && !reports.some((r) => r.name === 'Month-end headcount'))
    await step('Scheduled report on the last day of the month', 'POST', '/scheduled/reports', { name: 'Month-end headcount', kind: 'Headcount', frequency: 'Monthly', day: 0, sendHour: 17, recipientIds: to });
  if (to.length && !reports.some((r) => r.name === 'Fortnightly leave outlook'))
    await step('Scheduled report every 14 days', 'POST', '/scheduled/reports', { name: 'Fortnightly leave outlook', kind: 'OnLeave', frequency: 'EveryNDays', day: 14, sendHour: 9, recipientIds: to });

  // Documents: smaller CVs, announcements without attachments.
  await step('Documents: CVs up to 2 MB', 'PUT', '/attachments/settings/Candidate', { enabled: true, maxSizeMb: 2 });
  await step('Documents: travel files up to 3 MB', 'PUT', '/attachments/settings/Travel', { enabled: true, maxSizeMb: 3 });

  // ID card design.
  const card = (await call('GET', '/employees/id-card-template')).data ?? {};
  if (!card.color)
    await step('ID card design', 'PUT', '/employees/id-card-template', {
      color: '#0F766E', showBloodGroup: true, showJoining: true, showPhone: false, showQr: true, footer: 'If found, please return to the HR office.',
      portrait: true, printBack: true, backLine2: 'Emergency: call HR on the company number',
    });

  // A poll people can vote in (and take back).
  const polls = (await call('GET', '/announcements/polls/manage')).data ?? [];
  if (!polls.some((p) => p.question?.startsWith('Which day suits the annual dinner')))
    await step('Poll: annual dinner day', 'POST', '/announcements/polls', {
      question: 'Which day suits the annual dinner best?', description: 'You can change or take back your vote until it closes.',
      opensOn: today, closesOn: addDays(today, 10), departmentIds: [], isAnonymous: false, resultsVisibility: 'AfterVoting', options: ['Thursday', 'Friday', 'Saturday'],
    });

  // An hourly-paid part-timer.
  const staff = (await call('GET', '/employees?search=Hourly&pageSize=50')).data;
  const people = Array.isArray(staff) ? staff : staff?.items ?? [];
  if (!people.some((e) => (e.fullName ?? `${e.firstName} ${e.lastName}`) === 'Bilal Hourly')) {
    const emp = await step('Part-time employee paid by the hour', 'POST', '/employees', {
      firstName: 'Bilal', lastName: 'Hourly', joiningDate: addDays(today, -40), status: 'Active', gender: 'Male', email: `bilal.hourly@${l.hr.split('@')[1]}`,
    });
    if (emp.ok)
      await step('Rs 900 an hour', 'PUT', `/employees/${emp.data.id}/salary`, {
        effectiveFrom: addDays(today, -40), basicSalary: 0, eobiApplicable: false, payBasis: 'Hourly', rate: 900, components: [],
      });
  }

  // Budget positions with codes and salary bands.
  const plans = (await call('GET', '/budget/plans')).data ?? [];
  let plan = plans.find((p) => p.status === 'Draft');
  if (!plan && !plans.some((p) => p.name === `Manpower plan ${today.slice(0, 4)}-${Number(today.slice(0, 4)) + 1}`)) {
    const created = await step('Manpower plan from current staff', 'POST', '/budget/plans?fromStaff=true', {
      name: `Manpower plan ${today.slice(0, 4)}-${Number(today.slice(0, 4)) + 1}`, fromDate: today, toDate: addDays(today, 364), notes: null,
    });
    if (created.ok) plan = created.data;
  }
  if (plan) {
    const detail = (await call('GET', `/budget/plans/${plan.id}`)).data;
    if (detail?.canEdit && detail.lines.some((x) => !x.code)) {
      let i = 0;
      const lines = detail.lines.map((x) => {
        const code = x.code ?? `POS-${String(++i).padStart(3, '0')}`;
        return {
          departmentId: x.departmentId, designationId: x.designationId, headcount: x.headcount, monthlyCost: x.monthlyCost, filledFrom: x.filledFrom, notes: x.notes,
          code, gradeId: x.gradeId ?? null, costCenterId: x.costCenterId ?? null, reportsToCode: null,
          salaryMin: x.salaryMin ?? Math.round((x.monthlyCost * 0.85) / 1000) * 1000, salaryMax: x.salaryMax ?? Math.round((x.monthlyCost * 1.2) / 1000) * 1000,
          jobDescription: x.jobDescription ?? `${x.designation} in ${x.department}: delivers the team's goals and reports to the department head.`,
        };
      });
      await step(`Positions with codes and salary bands (${lines.length})`, 'PUT', `/budget/plans/${plan.id}/positions`, { positions: lines });
    }
  }
  // Course quizzes with certificates: employees take them, some pass (certificate) and one fails.
  const QUIZZES = [
    {
      course: 'Information Security Awareness', mode: 'Online', hours: 1,
      description: 'Passwords, phishing emails, locking your screen and keeping company data safe.',
      title: 'Information security quiz', pass: 70, minutes: 10, attempts: 3,
      questions: [
        ['An email asks you to confirm your password through a link. What do you do?', ['Click and confirm', 'Reply with the password', 'Report it to IT and delete it', 'Forward it to colleagues'], 2],
        ['Which password is the strongest?', ['ahmed123', 'Pakistan2026', 'Tea-Kettle-Rain-47', 'password'], 2],
        ['You leave your desk for lunch. What should you do with your computer?', ['Leave it open', 'Lock the screen', 'Turn off the monitor only', 'Ask a colleague to watch it'], 1],
        ['Where may you keep customer data?', ['Personal Gmail', 'A USB drive at home', 'Company systems only', 'WhatsApp'], 2],
        ['A visitor without a badge walks into the office. What do you do?', ['Ignore it', 'Politely take them to reception', 'Give them your badge', 'Let them in'], 1],
      ],
      takers: [[0, true], [1, true], [2, false]],
    },
    {
      course: 'Fire Safety and Evacuation', mode: 'Classroom', hours: 2,
      description: 'Fire exits, assembly point, using an extinguisher and what to do when the alarm rings.',
      title: 'Fire safety quiz', pass: 60, minutes: 8, attempts: 2,
      questions: [
        ['The fire alarm rings. What is the first thing to do?', ['Finish your work', 'Leave by the nearest fire exit', 'Take the lift', 'Call home'], 1],
        ['Which extinguisher is for electrical fires?', ['Water', 'CO2', 'Sand bucket only', 'None'], 1],
        ['Where do staff gather after leaving the building?', ['Car park exit', 'The assembly point', 'Canteen', 'Their cars'], 1],
        ['Should you use the lift during a fire?', ['Yes', 'Only if it is fast', 'No, never', 'Only with a manager'], 2],
      ],
      takers: [[1, true], [3, true]],
    },
  ];
  const programs = (await call('GET', '/training/programs')).data ?? [];
  const quizzes = (await call('GET', '/training/quizzes')).data ?? [];
  const tokens = {};
  const signIn = async (email) => {
    if (!(email in tokens)) {
      let r = await call('POST', '/auth/login', { email, password: l.password }, null);
      // Sign-ins are rate limited per minute: wait and try again.
      for (let i = 0; r.status === 429 && i < 3; i++) {
        await new Promise((ok) => setTimeout(ok, 30_000));
        r = await call('POST', '/auth/login', { email, password: l.password }, null);
      }
      tokens[email] = r.ok ? r.data.token : null;
    }
    return tokens[email];
  };
  for (const q of QUIZZES) {
    let program = programs.find((p) => p.title === q.course);
    if (!program) {
      const r = await step(`Course ${q.course}`, 'POST', '/training/programs', {
        title: q.course, categoryId: null, description: q.description, mode: q.mode, durationHours: q.hours, link: null,
        isMandatory: false, departmentIds: [], completeWithinDays: null, repeatEveryMonths: null, isActive: true,
      });
      if (r.ok) program = ((await call('GET', '/training/programs')).data ?? []).find((p) => p.title === q.course);
    }
    if (!program) continue;
    let quiz = quizzes.find((x) => x.title === q.title);
    if (!quiz) {
      const r = await step(`Quiz ${q.title} (${q.questions.length} questions)`, 'POST', '/training/quizzes', {
        programId: program.id, title: q.title, instructions: 'Choose one answer for each question. You can see the right answers after you submit.',
        passPercent: q.pass, timeLimitMinutes: q.minutes, maxAttempts: q.attempts, isActive: true,
        questions: q.questions.map(([text, options, correctIndex]) => ({ text, options, correctIndex })),
      });
      if (!r.ok) continue;
      quiz = ((await call('GET', '/training/quizzes')).data ?? []).find((x) => x.title === q.title);
      if (!quiz) continue;
    } else if (quiz.takers > 0) continue;
    for (const [i, pass] of q.takers) {
      const email = l.employees[i];
      const as = email && (await signIn(email));
      if (!as) {
        console.log(`  - ${email ?? `person ${i + 1}`}: ${email ? 'cannot sign in' : 'not in the logins file'}, quiz attempt skipped`);
        continue;
      }
      const paper = await call('POST', `/training/quizzes/${quiz.id}/start`, null, as);
      if (!paper.ok) {
        console.log(`  - ${email}: ${paper.data?.detail ?? paper.status}`);
        continue;
      }
      const answers = {};
      paper.data.questions.forEach((pq, n) => {
        const right = q.questions.find(([text]) => text === pq.text)?.[2] ?? 0;
        // Someone who fails gets the first two wrong.
        answers[pq.id] = pass || n >= 2 ? right : (right + 1) % pq.options.length;
      });
      const r = await call('POST', `/training/quizzes/attempts/${paper.data.attemptId}/submit`, { answers }, as);
      if (r.ok) console.log(`  ✓ ${email} took ${q.title}: ${r.data.scorePercent}%${r.data.certificateCode ? `, certificate ${r.data.certificateCode}` : ''}`);
      else {
        failures++;
        console.log(`  ✗ ${email} ${q.title}: ${r.status}`);
      }
    }
  }

  return failures;
}

let failed = 0;
for (const f of FILES) {
  try {
    failed += await run(f);
  } catch (e) {
    failed++;
    console.log(`  ✗ ${e.message}`);
  }
}
console.log(failed ? `${failed} step(s) failed` : 'Done.');
process.exit(failed ? 1 : 0);
