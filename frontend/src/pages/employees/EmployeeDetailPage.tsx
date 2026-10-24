import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useSearchParams } from 'react-router-dom';
import { ArrowUpRight, FolderOpen, GraduationCap, History, IdCard, KeyRound, Laptop, Palmtree, Phone, UserRound, UserRoundCheck, Wallet } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Avatar, Badge, Button, Card, CardBody, CardHeader, ErrorState, PageHeader, PageSpinner, StatusBadge, Tabs, useToast } from '../../components/ui';
import { formatDate, roleLabel } from '../../lib/format';
import type { Employee, SaveEmployeeRequest } from '../../types';
import { EmployeeForm, toSaveRequest } from './EmployeeForm';
import { LeaveTab } from './LeaveTab';
import { EmployeeFileCard, EmployeeRecordsCard } from './EmployeeFile';
import { HistoryButton } from '../../components/engine';
import { CostSplitCard } from './CostSplit';
import { LoginTab } from './LoginTab';
import { SalaryTab } from './SalaryTab';
import { EmergencyContactsCard, JobChangeModal, JobHistory, RehireModal } from './records';
import { CustomValuesCard } from '../settings/CustomFieldsTab';
import { EmployeeAssetsTab } from '../assets/EmployeeAssets';

type Tab = 'profile' | 'file' | 'family' | 'job' | 'emergency' | 'assets' | 'documents' | 'leave' | 'salary' | 'login';

export function EmployeeDetailPage() {
  const { id = '' } = useParams();
  const user = useUser();
  const hr = isHr(user);
  const [params, setParams] = useSearchParams();
  const requested = (params.get('tab') as Tab | null) ?? 'profile';
  const tab: Tab = hr || ['job', 'emergency', 'assets', 'file', 'family'].includes(requested) ? requested : 'profile';
  const employee = useQuery({ queryKey: qk.employee(id), queryFn: () => employeesApi.get(id) });
  const [dialog, setDialog] = useState<'change' | 'rehire' | null>(() => (params.get('rehire') ? 'rehire' : null));

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
  const current = e.status === 'Active' || e.status === 'Probation';

  return (
    <>
      <PageHeader back={{ to: '/employees', label: 'Employees' }} title={<span className="sr-only">{e.firstName} {e.lastName}</span>} />

      <Card className="-mt-4 mb-6 overflow-hidden">
        <div className="flex flex-col gap-5 p-5 sm:flex-row sm:items-start sm:p-6">
          <Avatar name={`${e.firstName} ${e.lastName}`} size="lg" />
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-xl font-semibold tracking-tight text-zinc-900 sm:text-2xl dark:text-white">
              {e.firstName} {e.lastName}
            </h2>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-sm">
              {e.designation && <span className="text-zinc-600 dark:text-zinc-300">{e.designation}</span>}
              <span className="font-mono text-xs text-zinc-500">{e.employeeCode}</span>
              <StatusBadge status={e.status} />
              {e.loginRole && <Badge tone="accent">{roleLabel(e.loginRole)}</Badge>}
            </div>
            <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3 text-sm lg:grid-cols-4">
              {(
                [
                  ['Department', e.department ?? '—', null],
                  ['Reports to', e.manager ?? '—', null],
                  ['Joined', formatDate(e.joiningDate), tenure(e.joiningDate)],
                  ['Contact', e.phone ?? e.email ?? '—', e.phone ? e.email : null],
                ] as [string, string, string | null][]
              ).map(([k, v, sub]) => (
                <div key={k} className="min-w-0">
                  <dt className="text-xs text-zinc-500 dark:text-zinc-400">{k}</dt>
                  <dd className="font-medium break-words text-zinc-900 dark:text-zinc-100">{v}</dd>
                  {sub && <dd className="truncate text-xs text-zinc-500 dark:text-zinc-400" title={sub}>{sub}</dd>}
                </div>
              ))}
            </dl>
          </div>
          {hr && (
            <div className="flex flex-wrap gap-2 sm:justify-end">
              <HistoryButton entityType="Employee" entityId={e.id} label="Profile change history" />
              {current ? (
                <Button variant="secondary" icon={<ArrowUpRight className="size-4" />} onClick={() => setDialog('change')}>
                  Promote / transfer
                </Button>
              ) : (
                <Button icon={<UserRoundCheck className="size-4" />} onClick={() => setDialog('rehire')}>
                  Re-hire
                </Button>
              )}
            </div>
          )}
        </div>
      </Card>

      <Tabs<Tab>
        className="mb-6"
        value={tab}
        onChange={(t) => setParams(t === 'profile' ? {} : { tab: t }, { replace: true })}
        items={[
          { value: 'profile', label: 'Profile', icon: <UserRound className="size-4" /> },
          { value: 'file', label: 'File', icon: <IdCard className="size-4" /> },
          { value: 'family', label: 'Family & education', icon: <GraduationCap className="size-4" /> },
          { value: 'job', label: 'Job history', icon: <History className="size-4" /> },
          { value: 'emergency', label: 'Emergency', icon: <Phone className="size-4" /> },
          { value: 'assets', label: 'Assets', icon: <Laptop className="size-4" /> },
          ...(hr
            ? ([
                { value: 'documents', label: 'Documents', icon: <FolderOpen className="size-4" /> },
                { value: 'leave', label: 'Leave', icon: <Palmtree className="size-4" /> },
                { value: 'salary', label: 'Salary', icon: <Wallet className="size-4" /> },
                { value: 'login', label: 'Login', icon: <KeyRound className="size-4" /> },
              ] as const)
            : []),
        ]}
      />

      {tab === 'profile' && (
        <div className="space-y-5">
          <ProfileTab key={e.id} employee={e} readOnly={!hr} />
          <CustomValuesCard employeeId={e.id} editable={hr} />
        </div>
      )}
      {tab === 'documents' && (
        <Card>
          <CardHeader title="Documents" description="CNIC, degrees, contracts and other records. Add an expiry date to get reminded before it lapses." />
          <CardBody>
            <AttachmentList owner="EmployeeDocument" ownerId={e.id} emptyText="No documents yet." />
          </CardBody>
        </Card>
      )}
      {tab === 'leave' && <LeaveTab employeeId={e.id} />}
      {tab === 'salary' && (
        <>
          <SalaryTab employeeId={e.id} />
          <CostSplitCard employeeId={e.id} />
        </>
      )}
      {tab === 'login' && <LoginTab employee={e} />}
      {tab === 'file' && <EmployeeFileCard employeeId={e.id} departmentId={e.departmentId} editable={hr} />}
      {tab === 'family' && <EmployeeRecordsCard employeeId={e.id} editable={hr} />}
      {tab === 'job' && <JobHistory employeeId={e.id} joiningDate={e.joiningDate} />}
      {tab === 'emergency' && <EmergencyContactsCard employeeId={e.id} editable={hr} />}
      {tab === 'assets' && <EmployeeAssetsTab employeeId={e.id} hr={hr} />}
      {dialog === 'change' && current && <JobChangeModal employee={e} onClose={() => setDialog(null)} />}
      {dialog === 'rehire' && !current && (
        <RehireModal
          employee={e}
          onClose={() => {
            setDialog(null);
            if (params.get('rehire')) setParams({}, { replace: true });
          }}
        />
      )}
    </>
  );
}

/** "2 yrs 3 mos" since the joining date. */
function tenure(joining: string): string {
  const [y, m] = joining.split('-').map(Number);
  const now = new Date();
  const months = Math.max(0, (now.getFullYear() - y) * 12 + now.getMonth() + 1 - m);
  const yrs = Math.floor(months / 12);
  const mos = months % 12;
  if (!yrs) return `${mos} ${mos === 1 ? 'mo' : 'mos'}`;
  return mos ? `${yrs} ${yrs === 1 ? 'yr' : 'yrs'} ${mos} ${mos === 1 ? 'mo' : 'mos'}` : `${yrs} ${yrs === 1 ? 'yr' : 'yrs'}`;
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
