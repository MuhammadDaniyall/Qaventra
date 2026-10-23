import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Ban, BarChart3, Briefcase, CalendarClock, Copy, Gift, Link2, MapPin, Pencil, Plus, Search, UserPlus, Users } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { useDepartments, useDesignations, useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  EMPLOYMENT_LABELS,
  EMPTY_CANDIDATE,
  MODE_LABELS,
  PIPELINE,
  recruitmentApi,
  SOURCE_LABELS,
  STAGE_LABELS,
  type ApplicationStage,
  type CandidateFormValues,
  type CandidateSource,
  type JobRequest,
  type Opening,
  type RecruitmentMe,
  type SaveJobRequest,
} from '../../api/recruitment';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  Drawer,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Tabs,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { formatCurrency, formatDate, formatDateTimeUtc, todayIso } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import { CandidateFields, careersUrl, cvProblem, JobPostModal, PostStatusBadge, StageBadge, useCopy } from './shared';

type Tab = 'jobs' | 'requests' | 'candidates' | 'interviews' | 'openings' | 'report';

/** Hiring: jobs and pipelines for HR and hiring teams, requests for managers, interviews and the internal job board for everyone. */
export function RecruitmentPage() {
  const me = useQuery({
    queryKey: qk.recruitmentMe,
    queryFn: recruitmentApi.me,
  });
  const [params, setParams] = useSearchParams();
  if (me.isLoading) return <SkeletonRows rows={6} cols={4} />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  const m = me.data!;
  const team = m.isHr || m.runsJobs;
  const allowed: Tab[] = [
    ...(team ? (['jobs'] as Tab[]) : []),
    ...(m.isHr || m.canRequest ? (['requests'] as Tab[]) : []),
    ...(team ? (['candidates'] as Tab[]) : []),
    'interviews',
    'openings',
    ...(team ? (['report'] as Tab[]) : []),
  ];
  const fallback: Tab = team ? 'jobs' : m.myInterviews > 0 ? 'interviews' : 'openings';
  const raw = params.get('tab') as Tab | null;
  const tab: Tab = raw && allowed.includes(raw) ? raw : fallback;
  const labels: Record<Tab, string> = {
    jobs: 'Jobs',
    requests: 'Hiring requests',
    candidates: 'Candidates',
    interviews: m.myInterviews ? `My interviews (${m.myInterviews})` : 'My interviews',
    openings: 'Open jobs',
    report: 'Reports',
  };
  return (
    <>
      <PageHeader title="Recruitment" description={team ? 'Jobs, candidates, interviews and offers.' : 'Open jobs, referrals and your interviews.'} />
      <Tabs<Tab> className="mb-5" value={tab} onChange={(t) => setParams({ tab: t })} items={allowed.map((t) => ({ value: t, label: labels[t] }))} />
      {tab === 'jobs' && <JobsTab me={m} />}
      {tab === 'requests' && <RequestsTab me={m} />}
      {tab === 'candidates' && <CandidatesTab me={m} />}
      {tab === 'interviews' && <InterviewsTab />}
      {tab === 'openings' && <OpeningsTab />}
      {tab === 'report' && <ReportTab />}
    </>
  );
}

// ---------- Jobs ----------

function JobsTab({ me }: { me: RecruitmentMe }) {
  const jobs = useQuery({
    queryKey: qk.jobPosts,
    queryFn: recruitmentApi.jobs,
  });
  const [creating, setCreating] = useState(false);
  const navigate = useNavigate();
  const copy = useCopy();
  return (
    <div className="space-y-5">
      {me.isHr && <CareersLinkCard slug={me.careersSlug} onCopy={copy} />}
      <Card>
        <CardHeader
          title="Jobs"
          description="Each job has its own pipeline. Open a job to put it on the careers page."
          actions={
            me.isHr && (
              <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setCreating(true)}>
                New job
              </Button>
            )
          }
        />
        {jobs.isLoading ? (
          <SkeletonRows rows={4} cols={5} />
        ) : jobs.isError ? (
          <ErrorState error={jobs.error} onRetry={() => jobs.refetch()} />
        ) : !jobs.data?.length ? (
          <EmptyState
            icon={Briefcase}
            title="No jobs yet"
            description={me.isHr ? 'Create a job, open it, and share the careers link.' : 'Jobs you recruit for show up here.'}
          />
        ) : (
          <Table>
            <THead>
              <Tr>
                <Th>Job</Th>
                <Th>Status</Th>
                <Th>Pipeline</Th>
                <Th>Team</Th>
                <Th className="text-right">Candidates</Th>
              </Tr>
            </THead>
            <TBody>
              {jobs.data.map((j) => (
                <Tr key={j.id} className="cursor-pointer" onClick={() => navigate(`/recruitment/jobs/${j.id}`)}>
                  <Td>
                    <Link
                      to={`/recruitment/jobs/${j.id}`}
                      className="font-medium text-zinc-900 hover:text-accent-600 dark:text-white"
                      onClick={(e) => e.stopPropagation()}
                    >
                      {j.title}
                    </Link>
                    <p className="text-xs text-zinc-500">
                      {[j.department, j.location, EMPLOYMENT_LABELS[j.employmentType], `${j.positions} position${j.positions === 1 ? '' : 's'}`]
                        .filter(Boolean)
                        .join(' · ')}
                    </p>
                  </Td>
                  <Td>
                    <PostStatusBadge status={j.status} />
                    {j.closesOn && j.status === 'Open' && <p className="mt-1 text-xs text-zinc-500">Closes {formatDate(j.closesOn)}</p>}
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-1">
                      {PIPELINE.filter((s) => j.byStage[s]).map((s) => (
                        <span key={s} className="rounded bg-zinc-100 px-1.5 py-0.5 text-[11px] text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                          {STAGE_LABELS[s]} {j.byStage[s]}
                        </span>
                      ))}
                    </div>
                  </Td>
                  <Td className="text-xs text-zinc-600 dark:text-zinc-300">
                    {j.recruiter && <p>Recruiter: {j.recruiter}</p>}
                    {j.hiringManager && <p>Manager: {j.hiringManager}</p>}
                  </Td>
                  <Td className="text-right tabular-nums">{j.total}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      {creating && <JobPostModal onClose={() => setCreating(false)} onSaved={(p) => navigate(`/recruitment/jobs/${p.id}`)} />}
    </div>
  );
}

function CareersLinkCard({ slug, onCopy }: { slug: string | null; onCopy: (text: string) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(slug ?? '');
  const save = useMutation({
    mutationFn: () => recruitmentApi.setCareersSlug(value),
    onSuccess: () => {
      toast.success('Careers link updated');
      qc.invalidateQueries({ queryKey: qk.recruitmentMe });
      setEditing(false);
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card className="flex flex-wrap items-center gap-3 px-5 py-4">
      <span className="flex size-9 items-center justify-center rounded-lg bg-accent-100 text-accent-700 dark:bg-accent-500/15 dark:text-accent-300">
        <Link2 className="size-4" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-zinc-900 dark:text-white">Careers page</p>
        {editing ? (
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <span className="text-xs text-zinc-500">{window.location.origin}/careers/</span>
            <Input className="h-8 w-56" value={value} maxLength={60} onChange={(e) => setValue(e.target.value)} />
            <Button size="sm" loading={save.isPending} onClick={() => save.mutate()}>
              Save
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
              Cancel
            </Button>
          </div>
        ) : slug ? (
          <a href={careersUrl(slug)} target="_blank" rel="noreferrer" className="block truncate text-xs text-accent-600 hover:underline">
            {careersUrl(slug)}
          </a>
        ) : (
          <p className="text-xs text-zinc-500">The link is made when you open your first job.</p>
        )}
      </div>
      {!editing && (
        <div className="flex gap-2">
          {slug && (
            <Button size="sm" variant="secondary" icon={<Copy className="size-3.5" />} onClick={() => onCopy(careersUrl(slug))}>
              Copy
            </Button>
          )}
          <Button size="sm" variant="ghost" icon={<Pencil className="size-3.5" />} onClick={() => setEditing(true)}>
            {slug ? 'Change' : 'Choose address'}
          </Button>
        </div>
      )}
    </Card>
  );
}

// ---------- Requests ----------

function RequestsTab({ me }: { me: RecruitmentMe }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const requests = useQuery({
    queryKey: qk.jobRequests,
    queryFn: recruitmentApi.requests,
  });
  const [asking, setAsking] = useState(false);
  const [posting, setPosting] = useState<JobRequest | null>(null);
  const cancel = useMutation({
    mutationFn: recruitmentApi.cancelRequest,
    onSuccess: () => {
      toast.success('Request cancelled');
      qc.invalidateQueries({ queryKey: qk.recruitment });
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader
        title="Hiring requests"
        description={me.isHr ? 'Approved requests are ready to become job posts.' : 'Ask HR to hire for your team. It goes through approval first.'}
        actions={
          (me.canRequest || me.isHr) && (
            <Button size="sm" icon={<UserPlus className="size-4" />} onClick={() => setAsking(true)}>
              Ask to hire
            </Button>
          )
        }
      />
      {requests.isLoading ? (
        <SkeletonRows rows={4} cols={4} />
      ) : requests.isError ? (
        <ErrorState error={requests.error} onRetry={() => requests.refetch()} />
      ) : !requests.data?.length ? (
        <EmptyState icon={UserPlus} title="No hiring requests" description="Requests to fill new or vacant positions show up here." />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Position</Th>
              <Th>Requested by</Th>
              <Th>Needed by</Th>
              <Th>Status</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {requests.data.map((r) => (
              <Tr key={r.id}>
                <Td>
                  <p className="font-medium text-zinc-900 dark:text-white">
                    {r.positions} × {r.title}
                  </p>
                  <p className="text-xs text-zinc-500">
                    {[r.department, r.replaces ? `Replaces ${r.replaces}` : 'New position', r.salaryMax ? `up to ${formatCurrency(r.salaryMax)}` : null]
                      .filter(Boolean)
                      .join(' · ')}
                  </p>
                  <p className="mt-0.5 line-clamp-2 text-xs text-zinc-600 dark:text-zinc-400">{r.reason}</p>
                </Td>
                <Td className="text-sm">
                  {r.requestedBy}
                  <p className="text-xs text-zinc-500">{formatDateTimeUtc(r.createdAtUtc)}</p>
                </Td>
                <Td className="text-sm">{r.neededBy ? formatDate(r.neededBy) : '—'}</Td>
                <Td>
                  <StatusBadge status={r.status} />
                  {r.decisionNote && <p className="mt-1 text-xs text-zinc-500">“{r.decisionNote}”</p>}
                </Td>
                <Td className="text-right">
                  <div className="flex justify-end gap-2">
                    {r.jobPostId ? (
                      me.isHr && (
                        <Button size="sm" variant="secondary" onClick={() => navigate(`/recruitment/jobs/${r.jobPostId}`)}>
                          View job
                        </Button>
                      )
                    ) : (
                      <>
                        {me.isHr && r.status === 'Approved' && (
                          <Button size="sm" onClick={() => setPosting(r)}>
                            Create job
                          </Button>
                        )}
                        {(r.status === 'Pending' || r.status === 'Approved') && (
                          <Button size="sm" variant="ghost" loading={cancel.isPending && cancel.variables === r.id} onClick={() => cancel.mutate(r.id)}>
                            Cancel
                          </Button>
                        )}
                      </>
                    )}
                  </div>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {asking && <JobRequestModal onClose={() => setAsking(false)} />}
      {posting && <JobPostModal fromRequest={posting} onClose={() => setPosting(null)} onSaved={(p) => navigate(`/recruitment/jobs/${p.id}`)} />}
    </Card>
  );
}

function JobRequestModal({ onClose }: { onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const departments = useDepartments();
  const designations = useDesignations();
  const people = useEmployeeLookup();
  const [replacement, setReplacement] = useState(false);
  const [f, setF] = useState({
    title: '',
    departmentId: '',
    designationId: '',
    positions: '1',
    replacesEmployeeId: '',
    salaryMin: '',
    salaryMax: '',
    experienceMinYears: '',
    qualification: '',
    skills: '',
    neededBy: '',
    reason: '',
  });
  const set = <K extends keyof typeof f>(k: K, v: string) => setF((x) => ({ ...x, [k]: v }));
  const n = (s: string) => (s.trim() === '' ? null : Number(s));
  const save = useMutation({
    mutationFn: () => {
      const b: SaveJobRequest = {
        title: f.title.trim(),
        departmentId: f.departmentId || null,
        designationId: f.designationId || null,
        positions: Number(f.positions) || 1,
        replacesEmployeeId: replacement ? f.replacesEmployeeId || null : null,
        salaryMin: n(f.salaryMin),
        salaryMax: n(f.salaryMax),
        experienceMinYears: n(f.experienceMinYears),
        qualification: f.qualification.trim() || null,
        skills: f.skills.trim() || null,
        neededBy: f.neededBy || null,
        reason: f.reason.trim(),
      };
      return recruitmentApi.createRequest(b);
    },
    onSuccess: (r) => {
      toast.success(r.status === 'Approved' ? 'Request approved' : 'Request sent for approval');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Ask to hire"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.title.trim() || !f.reason.trim()} onClick={() => save.mutate()}>
            Send request
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Position" required className="sm:col-span-2">
          <Input value={f.title} maxLength={150} onChange={(e) => set('title', e.target.value)} placeholder="Customer support officer" />
        </Field>
        <Field label="Department">
          <Select value={f.departmentId} onChange={(e) => set('departmentId', e.target.value)}>
            <option value="">—</option>
            {departments.data?.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Designation">
          <Select value={f.designationId} onChange={(e) => set('designationId', e.target.value)}>
            <option value="">—</option>
            {designations.data?.map((d) => (
              <option key={d.id} value={d.id}>
                {d.title}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="How many">
          <Input type="number" min={1} max={500} value={f.positions} onChange={(e) => set('positions', e.target.value)} />
        </Field>
        <Field label="Needed by">
          <Input type="date" min={todayIso()} value={f.neededBy} onChange={(e) => set('neededBy', e.target.value)} />
        </Field>
        <div className="sm:col-span-2">
          <Checkbox label="This replaces someone who is leaving" checked={replacement} onChange={(e) => setReplacement(e.target.checked)} />
          {replacement && (
            <Select className="mt-2" value={f.replacesEmployeeId} onChange={(e) => set('replacesEmployeeId', e.target.value)}>
              <option value="">Choose the person</option>
              {people.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          )}
        </div>
        <Field label="Salary from (PKR a month)">
          <Input type="number" min={0} value={f.salaryMin} onChange={(e) => set('salaryMin', e.target.value)} />
        </Field>
        <Field label="Salary to">
          <Input type="number" min={0} value={f.salaryMax} onChange={(e) => set('salaryMax', e.target.value)} />
        </Field>
        <Field label="Minimum experience (years)">
          <Input type="number" min={0} max={40} value={f.experienceMinYears} onChange={(e) => set('experienceMinYears', e.target.value)} />
        </Field>
        <Field label="Qualification">
          <Input value={f.qualification} maxLength={300} onChange={(e) => set('qualification', e.target.value)} placeholder="BBA or equivalent" />
        </Field>
        <Field label="Skills" className="sm:col-span-2">
          <Input value={f.skills} maxLength={1000} onChange={(e) => set('skills', e.target.value)} />
        </Field>
        <Field label="Why is this needed?" required className="sm:col-span-2">
          <Textarea rows={3} value={f.reason} maxLength={2000} onChange={(e) => set('reason', e.target.value)} />
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}

// ---------- Candidates ----------

function CandidatesTab({ me }: { me: RecruitmentMe }) {
  const [search, setSearch] = useState('');
  const [blacklisted, setBlacklisted] = useState(false);
  const q = useDebounced(search.trim(), 300);
  const list = useQuery({
    queryKey: qk.candidates(q, blacklisted),
    queryFn: () => recruitmentApi.candidates(q, blacklisted),
    placeholderData: keepPreviousData,
  });
  const [open, setOpen] = useState<string | null>(null);
  return (
    <Card>
      <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 px-5 py-3 dark:border-zinc-800">
        <div className="relative min-w-60 flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
          <Input className="pl-9" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search name, email, phone, skills, city…" />
        </div>
        {me.isHr && <Checkbox label="Do-not-hire list only" checked={blacklisted} onChange={(e) => setBlacklisted(e.target.checked)} />}
      </div>
      {list.isLoading ? (
        <SkeletonRows rows={5} cols={5} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={Users}
          title={q ? 'No one matches' : 'No candidates yet'}
          description="People who apply or are added to a job are kept here, one record per person."
        />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Candidate</Th>
              <Th>Experience</Th>
              <Th>Source</Th>
              <Th>Latest</Th>
              <Th>Added</Th>
            </Tr>
          </THead>
          <TBody>
            {list.data.map((c) => (
              <Tr key={c.id} className="cursor-pointer" onClick={() => setOpen(c.id)}>
                <Td>
                  <span className="flex items-center gap-2 font-medium text-zinc-900 dark:text-white">
                    {c.name}
                    {c.isBlacklisted && (
                      <Badge tone="red">
                        <Ban className="size-3" /> Do not hire
                      </Badge>
                    )}
                    {c.hired && <Badge tone="green">Employee</Badge>}
                  </span>
                  <p className="text-xs text-zinc-500">{[c.email, c.phone, c.city].filter(Boolean).join(' · ')}</p>
                </Td>
                <Td className="text-sm">
                  {c.currentTitle ?? '—'}
                  {c.experienceYears != null && <p className="text-xs text-zinc-500">{c.experienceYears} yrs</p>}
                </Td>
                <Td className="text-sm">{SOURCE_LABELS[c.source]}</Td>
                <Td className="text-sm">
                  {c.latestJob ?? '—'}
                  {c.latestStage && (
                    <div className="mt-1">
                      <StageBadge stage={c.latestStage} />
                    </div>
                  )}
                  {c.applications > 1 && <p className="text-xs text-zinc-500">{c.applications} jobs</p>}
                </Td>
                <Td className="text-xs text-zinc-500">{formatDateTimeUtc(c.createdAtUtc)}</Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {open && <CandidateDrawer id={open} isHr={me.isHr} onClose={() => setOpen(null)} onOpen={setOpen} />}
    </Card>
  );
}

function CandidateDrawer({ id, isHr, onClose, onOpen }: { id: string; isHr: boolean; onClose: () => void; onOpen: (id: string) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const c = useQuery({
    queryKey: qk.candidate(id),
    queryFn: () => recruitmentApi.candidate(id),
  });
  const jobs = useQuery({
    queryKey: qk.jobPosts,
    queryFn: recruitmentApi.jobs,
  });
  const [jobId, setJobId] = useState('');
  const [blacklisting, setBlacklisting] = useState(false);
  const [reason, setReason] = useState('');
  const add = useMutation({
    mutationFn: () => recruitmentApi.addExisting(jobId, id),
    onSuccess: (card) => {
      toast.success('Added to the job');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      navigate(`/recruitment/applications/${card.id}`);
    },
    onError: (e) => toast.error(e),
  });
  const blacklist = useMutation({
    mutationFn: (on: boolean) => recruitmentApi.blacklist(id, on, on ? reason.trim() : null),
    onSuccess: (_, on) => {
      toast.success(on ? 'Added to the do-not-hire list' : 'Removed from the do-not-hire list');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      setBlacklisting(false);
    },
    onError: (e) => toast.error(e),
  });
  const d = c.data;
  const already = new Set(d?.applications.map((a) => a.jobPostId));
  const openJobs = jobs.data?.filter((j) => (j.status === 'Open' || j.status === 'Closed') && !already.has(j.id)) ?? [];
  return (
    <Drawer open onClose={onClose} title={d ? `${d.firstName} ${d.lastName}`.trim() : 'Candidate'} description={d?.email}>
      {c.isLoading ? (
        <SkeletonRows rows={5} cols={2} />
      ) : c.isError ? (
        <ErrorState error={c.error} onRetry={() => c.refetch()} />
      ) : (
        d && (
          <div className="space-y-6">
            {d.isBlacklisted && (
              <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800 dark:bg-red-500/10 dark:text-red-200">
                <b>Do not hire.</b> {d.blacklistReason}
              </div>
            )}
            <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
              {(
                [
                  ['Phone', d.phone],
                  ['City', d.city],
                  ['CNIC', d.cnic],
                  ['Current job', [d.currentTitle, d.currentCompany].filter(Boolean).join(' at ') || null],
                  ['Experience', d.experienceYears != null ? `${d.experienceYears} years` : null],
                  ['Expected salary', d.expectedSalary ? formatCurrency(d.expectedSalary) : null],
                  ['Notice period', d.noticeDays != null ? `${d.noticeDays} days` : null],
                  ['Source', SOURCE_LABELS[d.source] + (d.referredBy ? ` (${d.referredBy})` : '')],
                ] as Array<[string, string | null]>
              ).map(([k, v]) => (
                <div key={k}>
                  <dt className="text-xs text-zinc-500">{k}</dt>
                  <dd className="text-zinc-900 dark:text-zinc-100">{v ?? '—'}</dd>
                </div>
              ))}
              {d.skills && (
                <div className="col-span-2">
                  <dt className="text-xs text-zinc-500">Skills</dt>
                  <dd>{d.skills}</dd>
                </div>
              )}
              {d.linkedInUrl && (
                <div className="col-span-2">
                  <dt className="text-xs text-zinc-500">LinkedIn</dt>
                  <dd>
                    <a href={d.linkedInUrl} target="_blank" rel="noreferrer noopener" className="text-accent-600 hover:underline">
                      {d.linkedInUrl}
                    </a>
                  </dd>
                </div>
              )}
            </dl>

            <section>
              <h3 className="mb-2 text-sm font-semibold">Jobs</h3>
              <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
                {d.applications.map((a) => (
                  <li key={a.id}>
                    <Link
                      to={`/recruitment/applications/${a.id}`}
                      className="flex items-center justify-between gap-2 px-3 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50"
                    >
                      <span>
                        {a.job}
                        <span className="block text-xs text-zinc-500">{formatDateTimeUtc(a.appliedAtUtc)}</span>
                      </span>
                      <StageBadge stage={a.stage} />
                    </Link>
                  </li>
                ))}
              </ul>
              {!d.employeeId && openJobs.length > 0 && (
                <div className="mt-2 flex gap-2">
                  <Select value={jobId} onChange={(e) => setJobId(e.target.value)}>
                    <option value="">Consider for another job…</option>
                    {openJobs.map((j) => (
                      <option key={j.id} value={j.id}>
                        {j.title}
                      </option>
                    ))}
                  </Select>
                  <Button variant="secondary" disabled={!jobId} loading={add.isPending} onClick={() => add.mutate()}>
                    Add
                  </Button>
                </div>
              )}
            </section>

            {d.possibleDuplicates.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-amber-700 dark:text-amber-300">Possible duplicates</h3>
                <ul className="space-y-1 text-sm">
                  {d.possibleDuplicates.map((x) => (
                    <li key={x.id}>
                      <button type="button" className="text-accent-600 hover:underline" onClick={() => onOpen(x.id)}>
                        {x.name}
                      </button>{' '}
                      <span className="text-xs text-zinc-500">{[x.email, x.phone].filter(Boolean).join(' · ')}</span>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <section>
              <h3 className="mb-2 text-sm font-semibold">CV and files</h3>
              <AttachmentList owner="Candidate" ownerId={d.id} emptyText="No CV yet." />
            </section>

            {isHr && (
              <section className="border-t border-zinc-100 pt-4 dark:border-zinc-800">
                {d.isBlacklisted ? (
                  <Button variant="secondary" loading={blacklist.isPending} onClick={() => blacklist.mutate(false)}>
                    Remove from do-not-hire list
                  </Button>
                ) : blacklisting ? (
                  <div className="space-y-2">
                    <Field label="Why should this person not be hired?" required>
                      <Textarea rows={2} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
                    </Field>
                    <div className="flex gap-2">
                      <Button variant="danger" disabled={!reason.trim()} loading={blacklist.isPending} onClick={() => blacklist.mutate(true)}>
                        Add to do-not-hire list
                      </Button>
                      <Button variant="ghost" onClick={() => setBlacklisting(false)}>
                        Cancel
                      </Button>
                    </div>
                  </div>
                ) : (
                  <Button variant="ghost" icon={<Ban className="size-4" />} onClick={() => setBlacklisting(true)}>
                    Do not hire…
                  </Button>
                )}
              </section>
            )}
          </div>
        )
      )}
    </Drawer>
  );
}

// ---------- My interviews ----------

function InterviewsTab() {
  const list = useQuery({
    queryKey: qk.myInterviews,
    queryFn: recruitmentApi.myInterviews,
  });
  const now = Date.now();
  const at = (u: string) => new Date(/[zZ]$/.test(u) ? u : `${u}Z`).getTime();
  const upcoming = list.data?.filter((i) => i.status === 'Scheduled' && at(i.startsAtUtc) > now) ?? [];
  const due = list.data?.filter((i) => !i.feedbackGiven && at(i.startsAtUtc) <= now && (i.status === 'Scheduled' || i.status === 'Completed')) ?? [];
  const done = list.data?.filter((i) => i.feedbackGiven || i.status === 'NoShow') ?? [];
  const row = (i: (typeof upcoming)[number], action: string) => (
    <li key={i.id}>
      <Link
        to={`/recruitment/applications/${i.applicationId}`}
        className="flex flex-wrap items-center gap-3 px-5 py-3 hover:bg-zinc-50 dark:hover:bg-zinc-800/40"
      >
        <CalendarClock className="size-4 text-zinc-400" />
        <span className="min-w-0 flex-1">
          <span className="font-medium text-zinc-900 dark:text-white">{i.candidate}</span>
          <span className="text-sm text-zinc-500"> · {i.job}</span>
          <span className="block text-xs text-zinc-500">
            {i.title} (round {i.round}) · {formatDateTimeUtc(i.startsAtUtc)} · {i.durationMinutes} min · {MODE_LABELS[i.mode]}
            {i.where ? ` · ${i.where}` : ''}
          </span>
        </span>
        <span className="text-sm font-medium text-accent-600">{action}</span>
      </Link>
    </li>
  );
  if (list.isLoading) return <SkeletonRows rows={4} cols={3} />;
  if (list.isError) return <ErrorState error={list.error} onRetry={() => list.refetch()} />;
  if (!list.data?.length)
    return (
      <Card>
        <EmptyState icon={CalendarClock} title="No interviews" description="When you are asked to interview a candidate, it shows up here." />
      </Card>
    );
  return (
    <div className="space-y-5">
      {due.length > 0 && (
        <Card>
          <CardHeader title="Feedback needed" description="Your verdict helps the team decide. Others' feedback shows after you give yours." />
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">{due.map((i) => row(i, 'Give feedback'))}</ul>
        </Card>
      )}
      <Card>
        <CardHeader title="Upcoming" />
        {upcoming.length ? (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">{upcoming.map((i) => row(i, 'Prepare'))}</ul>
        ) : (
          <p className="px-5 py-6 text-sm text-zinc-500">Nothing scheduled.</p>
        )}
      </Card>
      {done.length > 0 && (
        <Card>
          <CardHeader title="Done" />
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">{done.map((i) => row(i, 'View'))}</ul>
        </Card>
      )}
    </div>
  );
}

// ---------- Openings and referrals ----------

function OpeningsTab() {
  const openings = useQuery({
    queryKey: qk.openings,
    queryFn: recruitmentApi.openings,
  });
  const referrals = useQuery({
    queryKey: qk.referrals,
    queryFn: recruitmentApi.referrals,
  });
  const [referring, setReferring] = useState<Opening | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_20rem]">
      <div className="space-y-3">
        {openings.isLoading ? (
          <SkeletonRows rows={3} cols={2} />
        ) : openings.isError ? (
          <ErrorState error={openings.error} onRetry={() => openings.refetch()} />
        ) : !openings.data?.length ? (
          <Card>
            <EmptyState
              icon={Briefcase}
              title="No open jobs right now"
              description="When the company is hiring, the jobs show up here so you can refer people you know."
            />
          </Card>
        ) : (
          openings.data.map((o) => (
            <Card key={o.id} className="p-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="font-semibold text-zinc-900 dark:text-white">{o.title}</h3>
                  <p className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-zinc-500">
                    {o.department && <span>{o.department}</span>}
                    {o.location && (
                      <span className="inline-flex items-center gap-1">
                        <MapPin className="size-3" />
                        {o.location}
                      </span>
                    )}
                    <span>{EMPLOYMENT_LABELS[o.employmentType]}</span>
                    {o.closesOn && <span>Closes {formatDate(o.closesOn)}</span>}
                  </p>
                </div>
                <Button size="sm" icon={<Gift className="size-4" />} onClick={() => setReferring(o)}>
                  Refer someone
                </Button>
              </div>
              <p
                className={
                  expanded === o.id
                    ? 'mt-3 text-sm whitespace-pre-line text-zinc-700 dark:text-zinc-300'
                    : 'mt-3 line-clamp-2 text-sm text-zinc-700 dark:text-zinc-300'
                }
              >
                {o.description}
              </p>
              {expanded === o.id && o.requirements && (
                <ul className="mt-2 list-disc pl-5 text-sm text-zinc-700 dark:text-zinc-300">
                  {o.requirements
                    .split('\n')
                    .filter((l) => l.trim())
                    .map((l, i) => (
                      <li key={i}>{l}</li>
                    ))}
                </ul>
              )}
              <button
                type="button"
                className="mt-2 text-xs font-medium text-accent-600 hover:underline"
                onClick={() => setExpanded(expanded === o.id ? null : o.id)}
              >
                {expanded === o.id ? 'Show less' : 'Read more'}
              </button>
            </Card>
          ))
        )}
      </div>
      <Card className="self-start">
        <CardHeader title="My referrals" description="People you referred and how far they got." />
        {referrals.data?.length ? (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {referrals.data.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-2 px-5 py-2.5 text-sm">
                <span className="min-w-0 truncate">{r.job}</span>
                <StageBadge stage={r.stage} />
              </li>
            ))}
          </ul>
        ) : (
          <p className="px-5 py-6 text-sm text-zinc-500">No referrals yet.</p>
        )}
      </Card>
      {referring && <ReferModal opening={referring} onClose={() => setReferring(null)} />}
    </div>
  );
}

function ReferModal({ opening, onClose }: { opening: Opening; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [v, setV] = useState<CandidateFormValues>(EMPTY_CANDIDATE);
  const problem = cvProblem(v, false);
  const send = useMutation({
    mutationFn: () => recruitmentApi.refer(opening.id, v),
    onSuccess: () => {
      toast.success('Thanks for the referral', 'The hiring team has been told.');
      qc.invalidateQueries({ queryKey: qk.referrals });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={`Refer someone for ${opening.title}`}
      description="Only refer people who know you are sharing their details."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={send.isPending} disabled={!!problem} onClick={() => send.mutate()}>
            Refer
          </Button>
        </>
      }
    >
      <CandidateFields value={v} onChange={setV} compact />
      <FormError message={send.isError ? errorMessage(send.error) : null} />
    </Modal>
  );
}

// ---------- Reports ----------

function ReportTab() {
  const r = useQuery({
    queryKey: qk.recruitmentReport,
    queryFn: recruitmentApi.report,
  });
  if (r.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (r.isError) return <ErrorState error={r.error} onRetry={() => r.refetch()} />;
  const d = r.data!;
  const totalSource = Object.values(d.bySource).reduce((a, b) => a + (b ?? 0), 0);
  const tile = (label: string, value: string | number) => (
    <Card className="p-4">
      <p className="text-xl font-semibold text-zinc-900 tabular-nums dark:text-white">{value}</p>
      <p className="text-xs text-zinc-500">{label}</p>
    </Card>
  );
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {tile('Open jobs', d.openJobs)}
        {tile('Positions to fill', d.openPositions)}
        {tile('Candidates in play', d.activeCandidates)}
        {tile('Interviews this week', d.interviewsThisWeek)}
        {tile('Offer acceptance', d.offersMade ? `${Math.round((d.offersAccepted / d.offersMade) * 100)}%` : '—')}
        {tile('Average days to hire', d.averageDaysToHire ?? '—')}
      </div>
      <Card>
        <CardHeader title="Pipeline by job" />
        {!d.jobs.length ? (
          <EmptyState icon={BarChart3} title="No jobs yet" />
        ) : (
          <Table>
            <THead>
              <Tr>
                <Th>Job</Th>
                {PIPELINE.map((s) => (
                  <Th key={s} className="text-right">
                    {STAGE_LABELS[s]}
                  </Th>
                ))}
                <Th className="text-right">Rejected</Th>
                <Th className="text-right">Total</Th>
              </Tr>
            </THead>
            <TBody>
              {d.jobs.map((j) => (
                <Tr key={j.jobPostId}>
                  <Td>
                    <Link to={`/recruitment/jobs/${j.jobPostId}`} className="font-medium hover:text-accent-600">
                      {j.job}
                    </Link>
                    <p className="text-xs text-zinc-500">
                      {j.status} · {j.hired}/{j.positions} hired
                    </p>
                  </Td>
                  {PIPELINE.map((s: ApplicationStage) => (
                    <Td key={s} className="text-right tabular-nums">
                      {j.byStage[s] ?? 0}
                    </Td>
                  ))}
                  <Td className="text-right tabular-nums text-zinc-500">{j.byStage.Rejected ?? 0}</Td>
                  <Td className="text-right font-medium tabular-nums">{j.total}</Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
      <Card>
        <CardHeader title="Where candidates come from" description="Applications and hires by source" />
        <div className="space-y-3 px-5 py-4">
          {(Object.keys(SOURCE_LABELS) as CandidateSource[])
            .filter((s) => d.bySource[s])
            .map((s) => {
              const n = d.bySource[s] ?? 0;
              return (
                <div key={s}>
                  <div className="mb-1 flex justify-between text-sm">
                    <span>{SOURCE_LABELS[s]}</span>
                    <span className="text-zinc-500 tabular-nums">
                      {n} applied · {d.hiredBySource[s] ?? 0} hired
                    </span>
                  </div>
                  <div className="h-2 rounded-full bg-zinc-100 dark:bg-zinc-800">
                    <div
                      className="h-2 rounded-full bg-accent-500"
                      style={{
                        width: `${(n / Math.max(1, totalSource)) * 100}%`,
                      }}
                    />
                  </div>
                </div>
              );
            })}
          {!totalSource && <p className="text-sm text-zinc-500">No applications yet.</p>}
        </div>
      </Card>
    </div>
  );
}
