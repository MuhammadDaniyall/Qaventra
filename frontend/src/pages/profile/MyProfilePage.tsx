import { useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { KeyRound } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { ChangePasswordDialog } from '../../components/layout/ChangePasswordDialog';
import { Avatar, Badge, Button, Card, CardHeader, ErrorState, PageHeader, PageSpinner, StatusBadge } from '../../components/ui';
import { formatDate, humanize, roleLabel } from '../../lib/format';

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="grid grid-cols-1 gap-1 px-5 py-3 sm:grid-cols-[180px_1fr] sm:gap-4">
      <dt className="text-sm text-zinc-500 dark:text-zinc-400">{label}</dt>
      <dd className="text-sm text-zinc-900 dark:text-zinc-100">{value || <span className="text-zinc-400">—</span>}</dd>
    </div>
  );
}

export function MyProfilePage() {
  const user = useUser();
  const [pwOpen, setPwOpen] = useState(false);
  const profile = useQuery({ queryKey: qk.myProfile, queryFn: employeesApi.me });

  if (profile.isLoading) return <PageSpinner />;
  if (profile.isError || !profile.data) {
    return (
      <>
        <PageHeader title="My profile" />
        <Card>
          <ErrorState error={profile.error} onRetry={() => profile.refetch()} />
        </Card>
      </>
    );
  }
  const e = profile.data;

  return (
    <>
      <PageHeader
        title="My profile"
        description="Contact HR to correct any of these details."
        actions={
          <Button variant="secondary" icon={<KeyRound className="size-4" />} onClick={() => setPwOpen(true)}>
            Change password
          </Button>
        }
      />
      <Card className="mb-6 p-5">
        <div className="flex flex-wrap items-center gap-4">
          <Avatar name={`${e.firstName} ${e.lastName}`} size="lg" />
          <div>
            <p className="text-lg font-semibold text-zinc-900 dark:text-white">
              {e.firstName} {e.lastName}
            </p>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-zinc-500 dark:text-zinc-400">
              <span className="font-mono text-xs">{e.employeeCode}</span>
              <StatusBadge status={e.status} />
              <Badge tone="accent">{roleLabel(user.role)}</Badge>
              <span>{user.email}</span>
            </div>
          </div>
        </div>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader title="Personal" />
          <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
            <Row label="Email" value={e.email} />
            <Row label="Phone" value={e.phone} />
            <Row label="CNIC" value={e.cnic} />
            <Row label="Date of birth" value={e.dateOfBirth ? formatDate(e.dateOfBirth) : null} />
            <Row label="Gender" value={e.gender ? humanize(e.gender) : null} />
            <Row label="Address" value={e.address} />
          </dl>
        </Card>
        <div className="space-y-6">
          <Card>
            <CardHeader title="Job" />
            <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
              <Row label="Department" value={e.department} />
              <Row label="Designation" value={e.designation} />
              <Row label="Reports to" value={e.manager} />
              <Row label="Shift" value={e.shift} />
              <Row label="Joined" value={formatDate(e.joiningDate)} />
              {e.leavingDate && <Row label="Leaving date" value={formatDate(e.leavingDate)} />}
            </dl>
          </Card>
          <Card>
            <CardHeader title="Bank" />
            <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
              <Row label="Bank" value={e.bankName} />
              <Row label="Account" value={e.bankAccount} />
            </dl>
          </Card>
        </div>
      </div>
      <ChangePasswordDialog open={pwOpen} onClose={() => setPwOpen(false)} />
    </>
  );
}
