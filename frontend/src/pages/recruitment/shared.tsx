import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { FileUp, Star } from 'lucide-react';
import { useDepartments, useDesignations, useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import {
  CV_ACCEPT,
  EMPLOYMENT_LABELS,
  recruitmentApi,
  SOURCE_LABELS,
  STAGE_LABELS,
  type ApplicationStage,
  type CandidateFormValues,
  type CandidateSource,
  type EmploymentType,
  type JobPost,
  type JobPostStatus,
  type JobRequest,
  type SaveJobPost,
} from '../../api/recruitment';
import { Badge, Button, Checkbox, Field, FormError, Input, Modal, Select, Textarea, useToast } from '../../components/ui';
import type { Tone } from '../../components/ui/Badge';
import { cn } from '../../lib/cn';
import { errorMessage } from '../../api/client';

const STAGE_TONES: Record<ApplicationStage, Tone> = {
  Applied: 'gray',
  Shortlisted: 'blue',
  Test: 'violet',
  Interview: 'accent',
  Offer: 'amber',
  Hired: 'green',
  Rejected: 'red',
  Withdrawn: 'gray',
  OnHold: 'amber',
};

export function StageBadge({ stage }: { stage: ApplicationStage }) {
  return (
    <Badge tone={STAGE_TONES[stage]} dot>
      {STAGE_LABELS[stage]}
    </Badge>
  );
}

const POST_TONES: Record<JobPostStatus, Tone> = {
  Draft: 'gray',
  Open: 'green',
  Closed: 'amber',
  Filled: 'blue',
};

export function PostStatusBadge({ status }: { status: JobPostStatus }) {
  return <Badge tone={POST_TONES[status]}>{status}</Badge>;
}

export function Stars({ value, onChange, size = 'md' }: { value: number | null; onChange?: (v: number | null) => void; size?: 'sm' | 'md' }) {
  return (
    <span className="inline-flex items-center gap-0.5" aria-label={value ? `${value} of 5` : 'Not rated'}>
      {[1, 2, 3, 4, 5].map((n) => {
        const on = (value ?? 0) >= n;
        const icon = <Star className={cn(size === 'sm' ? 'size-3' : 'size-4', on ? 'fill-amber-400 text-amber-400' : 'text-zinc-300 dark:text-zinc-600')} />;
        return onChange ? (
          <button
            key={n}
            type="button"
            title={`${n} of 5`}
            onClick={() => onChange(value === n ? null : n)}
            className="rounded p-0.5 hover:bg-zinc-100 dark:hover:bg-zinc-800"
          >
            {icon}
          </button>
        ) : (
          <span key={n}>{icon}</span>
        );
      })}
    </span>
  );
}

/** Copies a link and says so. */
export function useCopy() {
  const toast = useToast();
  return (text: string, what = 'Link') =>
    navigator.clipboard.writeText(text).then(
      () => toast.success(`${what} copied`),
      () => toast.error('Could not copy. Select the link and copy it yourself.'),
    );
}

export function careersUrl(company: string, job?: string) {
  return `${window.location.origin}/careers/${company}${job ? `/${job}` : ''}`;
}

/** The candidate's details and CV, shared by the careers page, referrals and HR adding someone. */
export function CandidateFields({
  value,
  onChange,
  cvRequired,
  withSource,
  compact,
}: {
  value: CandidateFormValues;
  onChange: (v: CandidateFormValues) => void;
  cvRequired?: boolean;
  withSource?: boolean;
  compact?: boolean;
}) {
  const set = <K extends keyof CandidateFormValues>(k: K, v: CandidateFormValues[K]) => onChange({ ...value, [k]: v });
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <Field label="First name" required>
        <Input value={value.firstName} maxLength={100} onChange={(e) => set('firstName', e.target.value)} autoComplete="given-name" />
      </Field>
      <Field label="Last name">
        <Input value={value.lastName} maxLength={100} onChange={(e) => set('lastName', e.target.value)} autoComplete="family-name" />
      </Field>
      <Field label="Email" required>
        <Input type="email" value={value.email} maxLength={256} onChange={(e) => set('email', e.target.value)} autoComplete="email" />
      </Field>
      <Field label="Phone">
        <Input type="tel" value={value.phone} maxLength={30} onChange={(e) => set('phone', e.target.value)} autoComplete="tel" placeholder="0300-1234567" />
      </Field>
      <Field label="City">
        <Input value={value.city} maxLength={100} onChange={(e) => set('city', e.target.value)} autoComplete="address-level2" />
      </Field>
      <Field label="Experience (years)">
        <Input type="number" min={0} max={60} step={0.5} value={value.experienceYears} onChange={(e) => set('experienceYears', e.target.value)} />
      </Field>
      {!compact && (
        <>
          <Field label="Current job title">
            <Input value={value.currentTitle} maxLength={150} onChange={(e) => set('currentTitle', e.target.value)} />
          </Field>
          <Field label="Current company">
            <Input value={value.currentCompany} maxLength={150} onChange={(e) => set('currentCompany', e.target.value)} />
          </Field>
          <Field label="Expected salary (PKR a month)">
            <Input type="number" min={0} value={value.expectedSalary} onChange={(e) => set('expectedSalary', e.target.value)} />
          </Field>
          <Field label="Notice period (days)">
            <Input type="number" min={0} max={365} value={value.noticeDays} onChange={(e) => set('noticeDays', e.target.value)} />
          </Field>
          <Field label="LinkedIn profile" className="sm:col-span-2">
            <Input
              type="url"
              value={value.linkedInUrl}
              maxLength={300}
              onChange={(e) => set('linkedInUrl', e.target.value)}
              placeholder="https://linkedin.com/in/…"
            />
          </Field>
          <Field label="Key skills" className="sm:col-span-2">
            <Input value={value.skills} maxLength={1000} onChange={(e) => set('skills', e.target.value)} placeholder="e.g. C#, SQL, customer service" />
          </Field>
        </>
      )}
      {withSource && (
        <Field label="Source">
          <Select value={value.source ?? 'Manual'} onChange={(e) => set('source', e.target.value as CandidateSource)}>
            {(['Manual', 'WalkIn', 'Advertisement', 'Agency', 'Referral'] as CandidateSource[]).map((s) => (
              <option key={s} value={s}>
                {SOURCE_LABELS[s]}
              </option>
            ))}
          </Select>
        </Field>
      )}
      <Field label="CV" required={cvRequired} hint="PDF or Word, up to 5 MB" className={withSource ? '' : 'sm:col-span-2'}>
        <label className="flex cursor-pointer items-center gap-2 rounded-lg border border-dashed border-zinc-300 px-3 py-2.5 text-sm text-zinc-600 hover:border-accent-400 dark:border-zinc-700 dark:text-zinc-300">
          <FileUp className="size-4 shrink-0 text-zinc-500" />
          <span className="truncate">{value.cv ? value.cv.name : 'Choose a file'}</span>
          <input type="file" accept={CV_ACCEPT} className="sr-only" onChange={(e) => set('cv', e.target.files?.[0] ?? null)} />
        </label>
      </Field>
      <Field label={compact ? 'Why are they a good fit?' : 'Cover letter'} className="sm:col-span-2">
        <Textarea rows={compact ? 3 : 4} value={value.coverLetter} maxLength={4000} onChange={(e) => set('coverLetter', e.target.value)} />
      </Field>
    </div>
  );
}

export function cvProblem(v: CandidateFormValues, required: boolean): string | null {
  if (!v.firstName.trim()) return 'Enter the first name.';
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v.email.trim())) return 'Enter a valid email address.';
  if (!v.cv) return required ? 'Attach the CV.' : null;
  if (!/\.(pdf|docx?)$/i.test(v.cv.name)) return 'The CV must be a PDF or Word file.';
  if (v.cv.size > 5 * 1024 * 1024) return 'The CV must be smaller than 5 MB.';
  return null;
}

const num = (s: string) => (s.trim() === '' ? null : Number(s));

/** HR: create or edit a job post, optionally from an approved job request. */
export function JobPostModal({
  post,
  fromRequest,
  onClose,
  onSaved,
}: {
  post?: JobPost;
  fromRequest?: JobRequest;
  onClose: () => void;
  onSaved?: (p: JobPost) => void;
}) {
  const toast = useToast();
  const qc = useQueryClient();
  const departments = useDepartments();
  const designations = useDesignations();
  const people = useEmployeeLookup();
  const src = post ?? fromRequest;
  const [f, setF] = useState({
    title: src?.title ?? '',
    departmentId: src?.departmentId ?? '',
    designationId: src?.designationId ?? '',
    location: post?.location ?? '',
    employmentType: (post?.employmentType ?? 'FullTime') as EmploymentType,
    description: post?.description ?? (fromRequest ? fromRequest.reason : ''),
    requirements:
      post?.requirements ??
      (fromRequest
        ? [
            fromRequest.experienceMinYears ? `${fromRequest.experienceMinYears}+ years of experience` : '',
            fromRequest.qualification ?? '',
            fromRequest.skills ?? '',
          ]
            .filter(Boolean)
            .join('\n')
        : ''),
    salaryMin: src?.salaryMin?.toString() ?? '',
    salaryMax: src?.salaryMax?.toString() ?? '',
    showSalary: post?.showSalary ?? false,
    positions: (src?.positions ?? 1).toString(),
    closesOn: post?.closesOn ?? '',
    recruiterEmployeeId: post?.recruiterEmployeeId ?? '',
    hiringManagerEmployeeId: post?.hiringManagerEmployeeId ?? (fromRequest?.requestedByEmployeeId || ''),
  });
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const save = useMutation({
    mutationFn: () => {
      const b: SaveJobPost = {
        title: f.title.trim(),
        jobRequestId: post?.jobRequestId ?? fromRequest?.id ?? null,
        departmentId: f.departmentId || null,
        designationId: f.designationId || null,
        location: f.location.trim() || null,
        employmentType: f.employmentType,
        description: f.description.trim(),
        requirements: f.requirements.trim() || null,
        salaryMin: num(f.salaryMin),
        salaryMax: num(f.salaryMax),
        showSalary: f.showSalary,
        positions: Number(f.positions) || 1,
        closesOn: f.closesOn || null,
        recruiterEmployeeId: f.recruiterEmployeeId || null,
        hiringManagerEmployeeId: f.hiringManagerEmployeeId || null,
      };
      return post ? recruitmentApi.updateJob(post.id, b) : recruitmentApi.createJob(b);
    },
    onSuccess: (p) => {
      toast.success(post ? 'Job updated' : 'Job created', post ? undefined : 'It is a draft. Open it to start taking applications.');
      qc.invalidateQueries({ queryKey: qk.recruitment });
      onSaved?.(p);
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="xl"
      title={post ? 'Edit job' : 'New job'}
      description={fromRequest ? `From ${fromRequest.requestedBy}'s request` : undefined}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={save.isPending} disabled={!f.title.trim() || !f.description.trim()} onClick={() => save.mutate()}>
            {post ? 'Save' : 'Create job'}
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Job title" required className="sm:col-span-2">
          <Input value={f.title} maxLength={150} onChange={(e) => set('title', e.target.value)} placeholder="Senior accountant" />
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
        <Field label="Designation" hint="Given to the person when hired">
          <Select value={f.designationId} onChange={(e) => set('designationId', e.target.value)}>
            <option value="">—</option>
            {designations.data?.map((d) => (
              <option key={d.id} value={d.id}>
                {d.title}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Location">
          <Input value={f.location} maxLength={150} onChange={(e) => set('location', e.target.value)} placeholder="Lahore (on-site)" />
        </Field>
        <Field label="Type">
          <Select value={f.employmentType} onChange={(e) => set('employmentType', e.target.value as EmploymentType)}>
            {(Object.keys(EMPLOYMENT_LABELS) as EmploymentType[]).map((t) => (
              <option key={t} value={t}>
                {EMPLOYMENT_LABELS[t]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="About the job" required className="sm:col-span-2">
          <Textarea rows={5} value={f.description} maxLength={8000} onChange={(e) => set('description', e.target.value)} />
        </Field>
        <Field label="Requirements" hint="One per line" className="sm:col-span-2">
          <Textarea rows={4} value={f.requirements} maxLength={8000} onChange={(e) => set('requirements', e.target.value)} />
        </Field>
        <Field label="Salary from (PKR a month)">
          <Input type="number" min={0} value={f.salaryMin} onChange={(e) => set('salaryMin', e.target.value)} />
        </Field>
        <Field label="Salary to">
          <Input type="number" min={0} value={f.salaryMax} onChange={(e) => set('salaryMax', e.target.value)} />
        </Field>
        <Checkbox
          className="sm:col-span-2"
          label="Show the salary on the careers page"
          checked={f.showSalary}
          onChange={(e) => set('showSalary', e.target.checked)}
        />
        <Field label="Positions">
          <Input type="number" min={1} max={500} value={f.positions} onChange={(e) => set('positions', e.target.value)} />
        </Field>
        <Field label="Applications close on" hint="Leave empty to keep it open">
          <Input type="date" value={f.closesOn} onChange={(e) => set('closesOn', e.target.value)} />
        </Field>
        <Field label="Recruiter" hint="Screens, schedules and makes offers">
          <Select value={f.recruiterEmployeeId} onChange={(e) => set('recruiterEmployeeId', e.target.value)}>
            <option value="">HR</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Hiring manager" hint="Becomes the new hire's manager">
          <Select value={f.hiringManagerEmployeeId} onChange={(e) => set('hiringManagerEmployeeId', e.target.value)}>
            <option value="">—</option>
            {people.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <FormError message={save.isError ? errorMessage(save.error) : null} />
    </Modal>
  );
}
