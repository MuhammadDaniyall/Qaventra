import { useState, type FormEvent, type ReactNode } from 'react';
import { useDepartments, useDesignations, useEmployeeLookup, useShifts } from '../../api/hooks';
import { qk } from '../../api/keys';
import { orgApi } from '../../api/organization';
import { QuickCreateSelect } from './QuickCreateSelect';
import { Button, Card, Field, FormError, Input, Select, Textarea } from '../../components/ui';
import { todayIso } from '../../lib/format';
import { EMPLOYMENT_STATUSES, type Employee, type EmploymentStatus, type Gender, type SaveEmployeeRequest } from '../../types';

export function emptyEmployee(): SaveEmployeeRequest {
  return {
    firstName: '',
    lastName: '',
    joiningDate: todayIso(),
    employeeCode: null,
    email: null,
    phone: null,
    cnic: null,
    dateOfBirth: null,
    gender: null,
    status: 'Probation',
    leavingDate: null,
    address: null,
    bankName: null,
    bankAccount: null,
    departmentId: null,
    designationId: null,
    managerId: null,
    shiftId: null,
  };
}

export function toSaveRequest(e: Employee): SaveEmployeeRequest {
  return {
    firstName: e.firstName,
    lastName: e.lastName,
    joiningDate: e.joiningDate,
    employeeCode: e.employeeCode,
    email: e.email,
    phone: e.phone,
    cnic: e.cnic,
    dateOfBirth: e.dateOfBirth,
    gender: e.gender,
    status: e.status,
    leavingDate: e.leavingDate,
    address: e.address,
    bankName: e.bankName,
    bankAccount: e.bankAccount,
    departmentId: e.departmentId,
    designationId: e.designationId,
    managerId: e.managerId,
    shiftId: e.shiftId,
  };
}

const CNIC_RE = /^\d{5}-\d{7}-\d$/;

/** Formats digits as 12345-1234567-1 while typing. */
function formatCnic(raw: string): string {
  const d = raw.replace(/\D/g, '').slice(0, 13);
  if (d.length <= 5) return d;
  if (d.length <= 12) return `${d.slice(0, 5)}-${d.slice(5)}`;
  return `${d.slice(0, 5)}-${d.slice(5, 12)}-${d.slice(12)}`;
}

function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <Card>
      <div className="grid gap-6 p-5 md:grid-cols-[220px_1fr] md:p-6">
        <div>
          <h2 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">{title}</h2>
          <p className="mt-1 text-xs text-zinc-500 dark:text-zinc-400">{description}</p>
        </div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">{children}</div>
      </div>
    </Card>
  );
}

interface EmployeeFormProps {
  initial: SaveEmployeeRequest;
  /** The employee being edited (excluded from the manager picker). */
  selfId?: string;
  submitLabel: string;
  submitting: boolean;
  serverError?: string | null;
  serverFieldErrors?: Record<string, string>;
  readOnly?: boolean;
  onSubmit: (value: SaveEmployeeRequest) => void;
  onCancel?: () => void;
}

export function EmployeeForm({
  initial,
  selfId,
  submitLabel,
  submitting,
  serverError,
  serverFieldErrors,
  readOnly,
  onSubmit,
  onCancel,
}: EmployeeFormProps) {
  const [v, setV] = useState<SaveEmployeeRequest>(initial);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const departments = useDepartments();
  const designations = useDesignations();
  const shifts = useShifts();
  const managers = useEmployeeLookup();

  const allErrors = { ...serverFieldErrors, ...errors };
  const text = (key: keyof SaveEmployeeRequest) => ({
    value: (v[key] as string | null) ?? '',
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
      setV((s) => ({ ...s, [key]: e.target.value === '' ? null : e.target.value })),
    disabled: readOnly,
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const fe: Record<string, string> = {};
    if (!v.firstName.trim()) fe.firstName = 'First name is required.';
    if (!v.lastName.trim()) fe.lastName = 'Last name is required.';
    if (!v.joiningDate) fe.joiningDate = 'Joining date is required.';
    if (v.cnic && !CNIC_RE.test(v.cnic)) fe.cnic = 'CNIC must look like 12345-1234567-1.';
    if (v.leavingDate && v.joiningDate && v.leavingDate < v.joiningDate) fe.leavingDate = 'Cannot be before the joining date.';
    setErrors(fe);
    if (Object.keys(fe).length) return;
    onSubmit({ ...v, firstName: v.firstName.trim(), lastName: v.lastName.trim(), email: v.email?.trim() || null });
  };

  const leaving = v.status === 'Resigned' || v.status === 'Terminated';

  return (
    <form onSubmit={submit} className="space-y-5" noValidate>
      <FormError message={serverError} />
      <Section title="Personal" description="Identity and contact details.">
        <Field label="First name" required error={allErrors.firstName}>
          <Input {...text('firstName')} value={v.firstName} autoComplete="off" />
        </Field>
        <Field label="Last name" required error={allErrors.lastName}>
          <Input {...text('lastName')} value={v.lastName} autoComplete="off" />
        </Field>
        <Field label="Email" error={allErrors.email}>
          <Input type="email" {...text('email')} />
        </Field>
        <Field label="Phone" error={allErrors.phone}>
          <Input type="tel" placeholder="0300-1234567" {...text('phone')} />
        </Field>
        <Field label="CNIC" hint="Format: 12345-1234567-1" error={allErrors.cnic}>
          <Input
            inputMode="numeric"
            placeholder="12345-1234567-1"
            value={v.cnic ?? ''}
            disabled={readOnly}
            onChange={(e) => setV((s) => ({ ...s, cnic: e.target.value ? formatCnic(e.target.value) : null }))}
          />
        </Field>
        <Field label="Date of birth" error={allErrors.dateOfBirth}>
          <Input type="date" {...text('dateOfBirth')} />
        </Field>
        <Field label="Gender" error={allErrors.gender}>
          <Select
            value={v.gender ?? ''}
            disabled={readOnly}
            onChange={(e) => setV((s) => ({ ...s, gender: (e.target.value || null) as Gender | null }))}
          >
            <option value="">Not specified</option>
            <option value="Male">Male</option>
            <option value="Female">Female</option>
            <option value="Other">Other</option>
          </Select>
        </Field>
        <Field label="Address" className="sm:col-span-2" error={allErrors.address}>
          <Textarea rows={2} {...text('address')} />
        </Field>
      </Section>

      <Section title="Job" description="Designation, department, reporting line and working hours.">
        <div className="grid gap-4 rounded-xl border border-accent-200 bg-accent-50/50 p-4 sm:col-span-2 sm:grid-cols-2 dark:border-accent-500/25 dark:bg-accent-500/5">
          <QuickCreateSelect
            label="Designation"
            noun="designation"
            value={v.designationId}
            options={(designations.data ?? []).map((d) => ({ id: d.id, label: d.title }))}
            onChange={(id) => setV((s) => ({ ...s, designationId: id }))}
            create={(title) => orgApi.createDesignation({ title })}
            invalidate={qk.designations}
            placeholder="Choose a designation"
            hint={!readOnly && !designations.data?.length ? 'No designations yet — create the first one here.' : undefined}
            error={allErrors.designationId}
            disabled={readOnly}
          />
          <QuickCreateSelect
            label="Department"
            noun="department"
            value={v.departmentId}
            options={(departments.data ?? []).map((d) => ({ id: d.id, label: d.name }))}
            onChange={(id) => setV((s) => ({ ...s, departmentId: id }))}
            create={(name) => orgApi.createDepartment({ name, code: null })}
            invalidate={qk.departments}
            placeholder="Choose a department"
            hint={!readOnly && !departments.data?.length ? 'No departments yet — create the first one here.' : undefined}
            error={allErrors.departmentId}
            disabled={readOnly}
          />
        </div>
        <Field label="Employee code" hint={selfId ? undefined : 'Leave blank to auto-generate.'} error={allErrors.employeeCode}>
          <Input {...text('employeeCode')} placeholder="EMP-0001" />
        </Field>
        <Field label="Status" required error={allErrors.status}>
          <Select
            value={v.status}
            disabled={readOnly}
            onChange={(e) => setV((s) => ({ ...s, status: e.target.value as EmploymentStatus }))}
          >
            {EMPLOYMENT_STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Joining date" required error={allErrors.joiningDate}>
          <Input type="date" {...text('joiningDate')} value={v.joiningDate} />
        </Field>
        <Field
          label="Leaving date"
          hint={leaving ? 'Their login is disabled once they leave.' : undefined}
          error={allErrors.leavingDate}
        >
          <Input type="date" min={v.joiningDate || undefined} {...text('leavingDate')} />
        </Field>
        <Field label="Reports to" error={allErrors.managerId}>
          <Select {...text('managerId')}>
            <option value="">— No manager —</option>
            {(managers.data ?? [])
              .filter((m) => m.id !== selfId)
              .map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
          </Select>
        </Field>
        <Field label="Shift" hint="Blank uses the company's default shift." error={allErrors.shiftId}>
          <Select {...text('shiftId')}>
            <option value="">Default shift</option>
            {(shifts.data ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
                {s.isDefault ? ' (default)' : ''}
              </option>
            ))}
          </Select>
        </Field>
      </Section>

      <Section title="Bank" description="Where salary is paid.">
        <Field label="Bank name" error={allErrors.bankName}>
          <Input placeholder="Meezan Bank" {...text('bankName')} />
        </Field>
        <Field label="Account number / IBAN" error={allErrors.bankAccount}>
          <Input placeholder="PK36MEZN0000000000000000" {...text('bankAccount')} />
        </Field>
      </Section>

      {!readOnly && (
        <div className="flex justify-end gap-2">
          {onCancel && (
            <Button variant="secondary" onClick={onCancel}>
              Cancel
            </Button>
          )}
          <Button type="submit" loading={submitting}>
            {submitLabel}
          </Button>
        </div>
      )}
    </form>
  );
}
