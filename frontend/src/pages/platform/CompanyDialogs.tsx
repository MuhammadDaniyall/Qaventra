import { useState, type ReactNode } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Check, Copy, KeyRound } from 'lucide-react';
import { platformApi } from '../../api/platform';
import { ApiError, errorMessage } from '../../api/client';
import { Button, Field, FormError, Input, Modal, Switch, useToast } from '../../components/ui';
import { blank, CompanyInfoFields, emptyCompanyFields, fieldsFromCompany, PlanFields, planFromFields, validateCompany, type CompanyFields } from './CompanyForm';
import type { Company, CompanyAdminCredentials, CreateCompanyRequest } from '../../types';

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="space-y-4">
      <legend className="mb-3 text-xs font-semibold tracking-wider text-zinc-500 uppercase dark:text-zinc-400">{title}</legend>
      {children}
    </fieldset>
  );
}

/** "Demo Company (Pvt) Ltd" -> "superadmin@democompany.com"; empty when the name has no usable letters. */
export function suggestAdminEmail(companyName: string): string {
  const slug = companyName
    .toLowerCase()
    .replace(/\(\s*pvt\.?\s*\)/g, ' ')
    .replace(/\b(pvt|private|ltd|limited)\b\.?/g, ' ')
    .replace(/[^a-z0-9]/g, '');
  return slug ? `superadmin@${slug}.com` : '';
}

/** Create a company together with its Company Super Admin. */
export function CreateCompanyModal({ onClose, onCreated }: { onClose: () => void; onCreated: (c: CompanyAdminCredentials) => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [company, setCompany] = useState<CompanyFields>(emptyCompanyFields);
  const [first, setFirst] = useState('');
  const [last, setLast] = useState('');
  const [usePersonName, setUsePersonName] = useState(false);
  const [emailInput, setEmailInput] = useState('');
  // The email follows the company name until the user edits it; after that it is never overwritten.
  const [emailEdited, setEmailEdited] = useState(false);
  const email = emailEdited ? emailInput : suggestAdminEmail(company.name);
  const personFirst = usePersonName ? first.trim() : '';
  const personLast = usePersonName ? last.trim() : '';
  const defaultName = `${company.name.trim() || '<Company name>'} Super Admin`;
  const [autoPassword, setAutoPassword] = useState(true);
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const create = useMutation({
    mutationFn: (b: CreateCompanyRequest) => platformApi.createCompany(b),
    onSuccess: (res) => {
      toast.success(`${res.company.name} created`, `Company Super Admin: ${res.adminEmail}`);
      qc.invalidateQueries({ queryKey: ['platform'] });
      onCreated(res);
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) {
        const fe = err.fieldErrors();
        // Map server names to the fields in this form.
        setFieldErrors({ ...fe, first: fe.adminFirstName, last: fe.adminLastName, email: fe.adminEmail, password: fe.adminPassword });
      }
    },
  });

  const submit = () => {
    setError(null);
    const fe = validateCompany(company);
    if (personFirst && !personLast) fe.last = 'Enter the last name too, or clear both.';
    if (personLast && !personFirst) fe.first = 'Enter the first name too, or clear both.';
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) fe.email = 'Enter a valid email.';
    if (!autoPassword && password.length < 8) fe.password = 'Use at least 8 characters.';
    setFieldErrors(fe);
    if (Object.keys(fe).length) return;
    create.mutate({
      name: company.name.trim(),
      phone: blank(company.phone),
      city: blank(company.city),
      address: blank(company.address),
      ...planFromFields(company),
      // Omitted when empty: the server names the admin "<Company name> Super Admin".
      ...(personFirst && personLast ? { adminFirstName: personFirst, adminLastName: personLast } : {}),
      adminEmail: email.trim(),
      adminPassword: autoPassword ? null : password,
    });
  };

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Create company"
      description="The company and its Company Super Admin login are created together."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="create-company-form" loading={create.isPending}>
            Create company
          </Button>
        </>
      }
    >
      <form
        id="create-company-form"
        noValidate
        className="space-y-6"
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
      >
        <FormError message={error} />
        <Section title="Company">
          <CompanyInfoFields v={company} set={(p) => setCompany((c) => ({ ...c, ...p }))} errors={fieldErrors} />
        </Section>
        <div className="h-px bg-zinc-100 dark:bg-zinc-800" />
        <Section title="Plan & billing">
          <PlanFields v={company} set={(p) => setCompany((c) => ({ ...c, ...p }))} errors={fieldErrors} headcount={0} showStartHelper />
        </Section>
        <div className="h-px bg-zinc-100 dark:bg-zinc-800" />
        <Section title="Company Super Admin">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-zinc-50 px-3 py-2.5 text-sm dark:bg-zinc-800/50">
            <p className="text-zinc-600 dark:text-zinc-300" aria-live="polite">
              Name:{' '}
              <span className="font-medium text-zinc-900 dark:text-zinc-100">
                {personFirst && personLast ? `${personFirst} ${personLast}` : defaultName}
              </span>
            </p>
            <button
              type="button"
              onClick={() => setUsePersonName((u) => !u)}
              className="text-xs font-medium text-accent-600 hover:underline focus-visible:outline-2 focus-visible:outline-accent-500 dark:text-accent-400"
            >
              {usePersonName ? 'Use the company name instead' : "Use a person's name instead"}
            </button>
          </div>
          {usePersonName && (
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="First name" hint="Optional" error={fieldErrors.first}>
                <Input value={first} maxLength={100} autoFocus onChange={(e) => setFirst(e.target.value)} />
              </Field>
              <Field label="Last name" hint="Optional" error={fieldErrors.last}>
                <Input value={last} maxLength={100} onChange={(e) => setLast(e.target.value)} />
              </Field>
            </div>
          )}
          <Field
            label="Login email"
            required
            hint={emailEdited ? 'They sign in with this email.' : 'Suggested from the company name — change it to a real address you control.'}
            error={fieldErrors.email}
          >
            <Input
              type="email"
              autoComplete="off"
              value={email}
              placeholder="superadmin@company.com"
              onChange={(e) => {
                setEmailEdited(true);
                setEmailInput(e.target.value);
              }}
            />
          </Field>
          <Switch
            checked={autoPassword}
            onChange={setAutoPassword}
            label="Generate a secure password automatically"
            description="You'll see it once after the company is created."
          />
          {!autoPassword && (
            <Field label="Password" required hint="At least 8 characters." error={fieldErrors.password}>
              <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </Field>
          )}
        </Section>
      </form>
    </Modal>
  );
}

/** Edit company details (not the admin). */
export function EditCompanyModal({ company, onClose }: { company: Company; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [v, setV] = useState<CompanyFields>(() => fieldsFromCompany(company));
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const save = useMutation({
    mutationFn: () =>
      platformApi.updateCompany(company.id, {
        name: v.name.trim(),
        phone: blank(v.phone),
        city: blank(v.city),
        address: blank(v.address),
        // PUT replaces every field, so the full plan is always sent.
        ...planFromFields(v),
      }),
    onSuccess: (c) => {
      toast.success('Company updated', c.name);
      qc.invalidateQueries({ queryKey: ['platform'] });
      onClose();
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title="Edit company"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="edit-company-form" loading={save.isPending}>
            Save
          </Button>
        </>
      }
    >
      <form
        id="edit-company-form"
        noValidate
        className="space-y-6"
        onSubmit={(e) => {
          e.preventDefault();
          setError(null);
          const fe = validateCompany(v);
          setFieldErrors(fe);
          if (!Object.keys(fe).length) save.mutate();
        }}
      >
        <FormError message={error} />
        <Section title="Company">
          <CompanyInfoFields v={v} set={(p) => setV((s) => ({ ...s, ...p }))} errors={fieldErrors} />
        </Section>
        <div className="h-px bg-zinc-100 dark:bg-zinc-800" />
        <Section title="Plan & billing">
          <PlanFields v={v} set={(p) => setV((s) => ({ ...s, ...p }))} errors={fieldErrors} headcount={company.employees} />
        </Section>
      </form>
    </Modal>
  );
}

/** Reset the Company Super Admin's password (generated or chosen). */
export function ResetAdminPasswordModal({
  company,
  onClose,
  onReset,
}: {
  company: Company;
  onClose: () => void;
  onReset: (c: CompanyAdminCredentials) => void;
}) {
  const toast = useToast();
  const [auto, setAuto] = useState(true);
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const reset = useMutation({
    mutationFn: () => platformApi.resetAdminPassword(company.id, auto ? null : password),
    onSuccess: (res) => {
      toast.success('Password reset', res.adminEmail);
      onReset(res);
    },
    onError: (err) => setError(errorMessage(err)),
  });

  return (
    <Modal
      open
      onClose={onClose}
      size="sm"
      title="Reset admin password"
      description={`${company.adminName ?? 'Company Super Admin'} · ${company.adminEmail ?? '—'}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            variant="danger"
            loading={reset.isPending}
            icon={<KeyRound className="size-4" />}
            onClick={() => {
              setError(null);
              if (!auto && password.length < 8) return setError('Use at least 8 characters.');
              reset.mutate();
            }}
          >
            Reset password
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormError message={error} />
        <p className="text-sm text-zinc-600 dark:text-zinc-300">Their current password stops working immediately.</p>
        <Switch checked={auto} onChange={setAuto} label="Generate a secure password automatically" />
        {!auto && (
          <Field label="New password" required hint="At least 8 characters.">
            <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
        )}
      </div>
    </Modal>
  );
}

function CopyField({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      // Clipboard blocked: the value is selectable in the field.
    }
  };
  return (
    <Field label={label}>
      <div className="flex gap-2">
        <Input readOnly value={value} onFocus={(e) => e.currentTarget.select()} className={mono ? 'font-mono' : undefined} />
        <Button variant="secondary" onClick={copy} icon={copied ? <Check className="size-4 text-emerald-500" /> : <Copy className="size-4" />}>
          {copied ? 'Copied' : 'Copy'}
        </Button>
      </div>
    </Field>
  );
}

/**
 * Shows sign-in details once. The password lives only in this component's props and is never stored;
 * closing the dialog discards it.
 */
export function CredentialsDialog({
  credentials,
  title,
  onClose,
}: {
  credentials: CompanyAdminCredentials;
  title: string;
  onClose: () => void;
}) {
  return (
    <Modal
      open
      onClose={onClose}
      title={title}
      description={`Company Super Admin for ${credentials.company.name}`}
      footer={<Button onClick={onClose}>I've saved it</Button>}
    >
      <div className="space-y-4">
        <CopyField label="Email" value={credentials.adminEmail} />
        {credentials.generatedPassword && <CopyField label="Password" value={credentials.generatedPassword} mono />}
        {credentials.generatedPassword && (
          <div
            role="alert"
            className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm font-medium text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100"
          >
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
            This password is shown only once. Copy it now and share it securely.
          </div>
        )}
      </div>
    </Modal>
  );
}
