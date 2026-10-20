import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useSearchParams } from 'react-router-dom';
import { KeyRound, UserRound, Wallet } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Avatar, Badge, Card, ErrorState, PageHeader, PageSpinner, StatusBadge, Tabs, useToast } from '../../components/ui';
import { formatDate, roleLabel } from '../../lib/format';
import type { Employee, SaveEmployeeRequest } from '../../types';
import { EmployeeForm, toSaveRequest } from './EmployeeForm';
import { LoginTab } from './LoginTab';
import { SalaryTab } from './SalaryTab';

type Tab = 'profile' | 'salary' | 'login';

export function EmployeeDetailPage() {
  const { id = '' } = useParams();
  const user = useUser();
  const hr = isHr(user);
  const [params, setParams] = useSearchParams();
  const requested = (params.get('tab') as Tab | null) ?? 'profile';
  const tab: Tab = hr ? requested : 'profile';
  const employee = useQuery({ queryKey: qk.employee(id), queryFn: () => employeesApi.get(id) });

  if (employee.isLoading) return <PageSpinner />;
  if (employee.isError || !employee.data) {
    return (
      <>
        <PageHeader title="Employee" back={{ to: '/employees', label: 'Employees' }} />
        <Card>
          <ErrorState error={employee.error} onRetry={() => employee.refetch()} />
        </Card>
      </>
    );
  }
  const e = employee.data;

  return (
    <>
      <PageHeader
        back={{ to: '/employees', label: 'Employees' }}
        title={
          <span className="flex items-center gap-3">
            <Avatar name={`${e.firstName} ${e.lastName}`} size="lg" />
            <span className="min-w-0">
              <span className="block truncate">
                {e.firstName} {e.lastName}
              </span>
              <span className="mt-1 flex flex-wrap items-center gap-2 text-sm font-normal tracking-normal">
                <span className="font-mono text-xs text-zinc-500">{e.employeeCode}</span>
                <StatusBadge status={e.status} />
                {e.designation && <span className="text-zinc-500 dark:text-zinc-400">{e.designation}</span>}
                {e.loginRole && <Badge tone="accent">{roleLabel(e.loginRole)}</Badge>}
              </span>
            </span>
          </span>
        }
        description={`Joined ${formatDate(e.joiningDate)}${e.department ? ` · ${e.department}` : ''}${e.manager ? ` · Reports to ${e.manager}` : ''}`}
      />

      {hr && (
        <Tabs<Tab>
          className="mb-6"
          value={tab}
          onChange={(t) => setParams(t === 'profile' ? {} : { tab: t }, { replace: true })}
          items={[
            { value: 'profile', label: 'Profile', icon: <UserRound className="size-4" /> },
            { value: 'salary', label: 'Salary', icon: <Wallet className="size-4" /> },
            { value: 'login', label: 'Login', icon: <KeyRound className="size-4" /> },
          ]}
        />
      )}

      {tab === 'profile' && <ProfileTab key={e.id} employee={e} readOnly={!hr} />}
      {tab === 'salary' && <SalaryTab employeeId={e.id} />}
      {tab === 'login' && <LoginTab employee={e} />}
    </>
  );
}

function ProfileTab({ employee, readOnly }: { employee: Employee; readOnly: boolean }) {
  const toast = useToast();
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [version, setVersion] = useState(0);

  const save = useMutation({
    mutationFn: (b: SaveEmployeeRequest) => employeesApi.update(employee.id, b),
    onSuccess: (updated) => {
      toast.success('Profile saved');
      qc.setQueryData(qk.employee(employee.id), updated);
      qc.invalidateQueries({ queryKey: qk.employees });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      setVersion((v) => v + 1);
    },
    onError: (err) => {
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
    },
  });

  return (
    <EmployeeForm
      key={version}
      initial={toSaveRequest(employee)}
      selfId={employee.id}
      readOnly={readOnly}
      submitLabel="Save changes"
      submitting={save.isPending}
      serverError={error}
      serverFieldErrors={fieldErrors}
      onSubmit={(b) => {
        setError(null);
        setFieldErrors({});
        save.mutate(b);
      }}
    />
  );
}
