import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, useSearchParams } from 'react-router-dom';
import { ArrowUpRight, Building2, CalendarDays, FolderOpen, GraduationCap, History, IdCard, KeyRound, Laptop, Palmtree, Phone, UserRound, UserRoundCheck, UsersRound, Wallet } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import { RecordStamp } from '../../components/RecordStamp';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import { Avatar, Badge, Button, Card, CardBody, CardHeader, ErrorState, PageHeader, PageSpinner, StatusBadge, Tabs, useToast } from '../../components/ui';
import { formatDate, roleLabel } from '../../lib/format';
import { tx } from '../../lib/i18n';
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
        {/* Brand cover band, as on the home dashboard header. */}
        <div className="relative h-24 overflow-hidden bg-brand sm:h-28" aria-hidden="true">
          <div className="absolute -top-16 -end-10 size-56 rounded-full bg-white/20 blur-3xl" />
          <div className="absolute -bottom-20 start-1/4 size-56 rounded-full bg-fuchsia-300/30 blur-3xl" />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_1px_1px,rgb(255_255_255/0.22)_1px,transparent_0)] [background-size:18px_18px] [mask-image:linear-gradient(to_bottom,black,transparent)]" />
        </div>
        <div className="relative px-5 pb-5 sm:px-6 sm:pb-6">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <Avatar
              name={`${e.firstName} ${e.lastName}`}
              size="xl"
              className="-mt-10 shadow-lg shadow-indigo-500/20 ring-4 ring-white sm:-mt-12 dark:ring-zinc-900"
            />
            {hr && (
              <div className="flex flex-wrap gap-2 pt-3">
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
          <div className="mt-3 min-w-0">
            <h2 className="truncate text-xl font-semibold tracking-tight text-zinc-900 sm:text-2xl dark:text-white">
              {e.firstName} {e.lastName}
            </h2>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-sm">
              {e.designation && <span className="text-zinc-600 dark:text-zinc-300">{e.designation}</span>}
              <span className="rounded-md bg-zinc-100 px-1.5 py-0.5 font-mono text-[11px] text-zinc-600 dark:bg-white/[0.06] dark:text-zinc-300">
                {e.employeeCode}
              </span>
              <StatusBadge status={e.status} />
              {e.loginRole && <Badge tone="accent">{roleLabel(e.loginRole)}</Badge>}
            </div>
          </div>
          <dl className="mt-5 grid grid-cols-1 gap-2.5 text-sm min-[420px]:grid-cols-2 lg:grid-cols-4">
            {(
              [
                ['Department', e.department ?? '—', null, Building2],
                ['Reports to', e.manager ?? '—', null, UsersRound],
                ['Joined', formatDate(e.joiningDate), tenure(e.joiningDate), CalendarDays],
                ['Contact', e.phone ?? e.email ?? '—', e.phone ? e.email : null, Phone],
              ] as [string, string, string | null, typeof Phone][]
            ).map(([k, v, sub, Icon]) => (
              <div
                key={k}
                className="flex min-w-0 items-start gap-3 rounded-xl border border-zinc-200/70 bg-zinc-50/70 p-3 dark:border-white/[0.06] dark:bg-white/[0.03]"
              >
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white text-indigo-600 shadow-xs ring-1 ring-zinc-200/80 dark:bg-white/[0.06] dark:text-indigo-300 dark:ring-white/10">
                  <Icon className="size-4" aria-hidden="true" />
                </span>
                <div className="min-w-0">
                  <dt className="text-xs text-zinc-500 dark:text-zinc-400">{tx(k)}</dt>
                  <dd className="truncate font-medium text-zinc-900 dark:text-zinc-100" title={v}>{v}</dd>
                  {sub && <dd className="truncate text-xs text-zinc-500 dark:text-zinc-400" title={sub}>{sub}</dd>}
                </div>
              </div>
            ))}
          </dl>
          <RecordStamp stamps={e.stamps} className="mt-4" />
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
          {/* Remount when the record changes elsewhere (job change, re-hire) so the form never saves stale values. */}
          <ProfileTab key={`${e.id}-${employee.dataUpdatedAt}`} employee={e} readOnly={!hr} />
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
