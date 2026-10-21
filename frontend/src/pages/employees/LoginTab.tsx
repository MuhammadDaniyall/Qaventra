import { useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { IssuedLoginsView } from './records';
import { KeyRound, RotateCcw, ShieldCheck } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isCompanyAdmin } from '../../auth/roles';
import { Badge, Button, Card, CardBody, CardHeader, ConfirmDialog, Field, FormError, Input, Modal, Select, Switch, useToast } from '../../components/ui';
import { roleLabel } from '../../lib/format';
import type { Employee, IssuedLogin, Me, UserRole } from '../../types';

function assignableRoles(me: Me): UserRole[] {
  return isCompanyAdmin(me)
    ? ['Employee', 'Manager', 'HrManager', 'CompanyAdmin']
    : ['Employee', 'Manager', 'HrManager'];
}

const ROLE_HELP: Record<string, string> = {
  Employee: 'Self-service: attendance, leave, payslips.',
  Manager: 'Plus team attendance and leave approvals for direct reports.',
  HrManager: 'Full HR: employees, payroll, organization settings.',
  CompanyAdmin: 'Everything, including audit logs and other admins.',
};

export function LoginTab({ employee }: { employee: Employee }) {
  return employee.userId ? <ManageLogin employee={employee} /> : <CreateLogin employee={employee} />;
}

function CreateLogin({ employee }: { employee: Employee }) {
  const me = useUser();
  const toast = useToast();
  const qc = useQueryClient();
  const [email, setEmail] = useState(employee.email ?? '');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<UserRole>('Employee');
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const create = useMutation({
    mutationFn: () => employeesApi.createLogin(employee.id, { email: email.trim(), password, role }),
    onSuccess: () => {
      toast.success('Login created', `${employee.firstName} can now sign in with ${email.trim()}.`);
      qc.invalidateQueries({ queryKey: qk.employees });
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    const fe: Record<string, string> = {};
    if (!email.trim()) fe.email = 'Email is required.';
    if (password.length < 8) fe.password = 'Use at least 8 characters.';
    setFieldErrors(fe);
    if (!Object.keys(fe).length) create.mutate();
  };

  return (
    <Card className="max-w-2xl">
      <CardHeader title="Create login" description={`${employee.firstName} has no login yet. Share the password with them securely.`} />
      <CardBody>
        <form onSubmit={submit} className="space-y-4" noValidate>
          <FormError message={error} />
          <Field label="Email" required error={fieldErrors.email}>
            <Input type="email" autoComplete="off" value={email} onChange={(e) => setEmail(e.target.value)} />
          </Field>
          <Field label="Temporary password" required hint="At least 8 characters. They can change it after signing in." error={fieldErrors.password}>
            <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <Field label="Role" required hint={ROLE_HELP[role]} error={fieldErrors.role}>
            <Select value={role} onChange={(e) => setRole(e.target.value as UserRole)}>
              {assignableRoles(me).map((r) => (
                <option key={r} value={r}>
                  {roleLabel(r)}
                </option>
              ))}
            </Select>
          </Field>
          <div className="flex justify-end">
            <Button type="submit" loading={create.isPending} icon={<KeyRound className="size-4" />}>
              Create login
            </Button>
          </div>
        </form>
      </CardBody>
    </Card>
  );
}

function ManageLogin({ employee }: { employee: Employee }) {
  const me = useUser();
  const toast = useToast();
  const qc = useQueryClient();
  const isSelf = me.employeeId === employee.id;
  const [role, setRole] = useState<UserRole>(employee.loginRole ?? 'Employee');
  const [active, setActive] = useState(employee.loginActive ?? true);
  const [error, setError] = useState<string | null>(null);
  const roles = assignableRoles(me);
  const locked = isSelf || (employee.loginRole === 'CompanyAdmin' && !isCompanyAdmin(me));

  const update = useMutation({
    mutationFn: () => employeesApi.updateLogin(employee.id, { role, isActive: active }),
    onSuccess: () => {
      toast.success('Login updated');
      qc.invalidateQueries({ queryKey: qk.employee(employee.id) });
      qc.invalidateQueries({ queryKey: qk.employees });
    },
    onError: (err) => setError(errorMessage(err)),
  });
  const [confirmReset, setConfirmReset] = useState(false);
  const [issued, setIssued] = useState<IssuedLogin | null>(null);
  const reset = useMutation({
    mutationFn: () => employeesApi.resetPassword(employee.id),
    onSuccess: (r) => {
      setConfirmReset(false);
      setIssued(r);
    },
    onError: (err) => {
      setConfirmReset(false);
      toast.error(err);
    },
  });

  return (
    <Card className="max-w-2xl">
      <CardHeader
        title="Login access"
        description="Change what this person can do, or disable their sign-in."
        actions={employee.loginRole && <Badge tone="accent">{roleLabel(employee.loginRole)}</Badge>}
      />
      <CardBody className="space-y-5">
        <FormError message={error} />
        {locked && (
          <div className="flex items-start gap-2 rounded-lg bg-zinc-50 p-3 text-sm text-zinc-600 dark:bg-zinc-800/50 dark:text-zinc-300">
            <ShieldCheck className="mt-0.5 size-4 shrink-0" />
            {isSelf
              ? 'You cannot change your own role or disable your own login.'
              : "Only a company admin can change another admin's login."}
          </div>
        )}
        <Field label="Role" hint={ROLE_HELP[role]}>
          <Select value={role} disabled={locked} onChange={(e) => setRole(e.target.value as UserRole)}>
            {(roles.includes(role) ? roles : [role, ...roles]).map((r) => (
              <option key={r} value={r}>
                {roleLabel(r)}
              </option>
            ))}
          </Select>
        </Field>
        <Switch
          checked={active}
          onChange={setActive}
          disabled={locked}
          label="Login enabled"
          description="Disabled users cannot sign in. Their records are kept."
        />
        <div className="flex flex-wrap justify-between gap-2">
          <Button variant="secondary" icon={<RotateCcw className="size-4" />} disabled={locked} onClick={() => setConfirmReset(true)}>
            Reset password
          </Button>
          <Button loading={update.isPending} disabled={locked} onClick={() => update.mutate()}>
            Save access
          </Button>
        </div>
      </CardBody>
      <ConfirmDialog
        open={confirmReset}
        title="Reset this password?"
        message={`${employee.firstName} gets a new temporary password and must choose their own at the next sign-in. The old password stops working.`}
        confirmLabel="Reset password"
        loading={reset.isPending}
        onConfirm={() => reset.mutate()}
        onClose={() => setConfirmReset(false)}
      />
      {issued && (
        <Modal open onClose={() => setIssued(null)} title="Password reset" footer={<Button onClick={() => setIssued(null)}>Done</Button>}>
          <IssuedLoginsView result={{ emailed: !issued.password, created: [issued], skipped: [] }} />
        </Modal>
      )}
    </Card>
  );
}
