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
        actions={
          hr && (
            <>
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
            </>
          )
        }
      />

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
      {tab === 'salary' && <SalaryTab employeeId={e.id} />}
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
