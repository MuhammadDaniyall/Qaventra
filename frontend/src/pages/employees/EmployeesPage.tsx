import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { KeyRound, Plus, Search, Users } from 'lucide-react';
import { employeesApi } from '../../api/employees';
import { useDepartments } from '../../api/hooks';
import { qk } from '../../api/keys';
import { useUser } from '../../auth/AuthContext';
import { isHr } from '../../auth/roles';
import {
  Avatar,
  Button,
  Card,
  EmptyState,
  ErrorState,
  Input,
  PageHeader,
  Pagination,
  Select,
  SkeletonRows,
  StatusBadge,
  Table,
  TBody,
  Td,
  Th,
  THead,
  Tr,
} from '../../components/ui';
import { formatDate } from '../../lib/format';
import { useDebounced } from '../../lib/hooks';
import { EMPLOYMENT_STATUSES, type EmploymentStatus } from '../../types';

const PAGE_SIZE = 25;

export function EmployeesPage() {
  const user = useUser();
  const hr = isHr(user);
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [status, setStatus] = useState<EmploymentStatus | ''>('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebounced(search.trim(), 300);
  const departments = useDepartments();

  const query = { search: debouncedSearch, departmentId: departmentId || undefined, status, page, pageSize: PAGE_SIZE };
  const list = useQuery({
    queryKey: [...qk.employees, 'list', query],
    queryFn: () => employeesApi.list(query),
    placeholderData: keepPreviousData,
  });
  const filtered = !!(debouncedSearch || departmentId || status);

  return (
    <>
      <PageHeader
        title="Employees"
        description={hr ? 'Everyone in your company.' : 'Your direct reports.'}
        actions={
          hr && (
            <Button icon={<Plus className="size-4" />} onClick={() => navigate('/employees/new')}>
              Add employee
            </Button>
          )
        }
      />
      <Card>
        <div className="flex flex-col gap-3 border-b border-zinc-100 p-4 sm:flex-row sm:items-center dark:border-zinc-800">
          <div className="relative flex-1 sm:max-w-sm">
            <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-zinc-400" />
            <Input
              aria-label="Search employees"
              placeholder="Search name, code, email or CNIC"
              className="pl-9"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="grid grid-cols-2 gap-3 sm:flex">
            <Select
              aria-label="Filter by department"
              value={departmentId}
              onChange={(e) => {
                setDepartmentId(e.target.value);
                setPage(1);
              }}
              className="sm:w-48"
            >
              <option value="">All departments</option>
              {(departments.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
            <Select
              aria-label="Filter by status"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value as EmploymentStatus | '');
                setPage(1);
              }}
              className="sm:w-40"
            >
              <option value="">All statuses</option>
              {EMPLOYMENT_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </Select>
          </div>
        </div>

        {list.isLoading ? (
          <SkeletonRows rows={8} cols={6} />
        ) : list.isError ? (
          <ErrorState error={list.error} onRetry={() => list.refetch()} />
        ) : !list.data || list.data.items.length === 0 ? (
          <EmptyState
            icon={Users}
            title={filtered ? 'No employees match' : hr ? 'No employees yet' : 'No direct reports'}
            description={
              filtered
                ? 'Try a different search or clear the filters.'
                : hr
                  ? 'Add your first employee to start tracking attendance, leave and payroll.'
                  : 'People who report to you will appear here.'
            }
            action={
              hr && !filtered ? (
                <Button icon={<Plus className="size-4" />} onClick={() => navigate('/employees/new')}>
                  Add employee
                </Button>
              ) : undefined
            }
          />
        ) : (
          <>
            <Table className={list.isFetching ? 'opacity-70 transition-opacity' : undefined}>
              <THead>
                <tr>
                  <Th>Employee</Th>
                  <Th>Code</Th>
                  <Th>Department</Th>
                  <Th>Designation</Th>
                  <Th>Status</Th>
                  <Th>Joined</Th>
                  <Th>Login</Th>
                </tr>
              </THead>
              <TBody>
                {list.data.items.map((e) => (
                  <Tr key={e.id} onClick={() => navigate(`/employees/${e.id}`)}>
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar name={e.fullName} size="sm" />
                        <div className="min-w-0">
                          <p className="font-medium text-zinc-900 dark:text-zinc-100">{e.fullName}</p>
                          <p className="text-xs text-zinc-500 dark:text-zinc-400">{e.email ?? e.phone ?? '—'}</p>
                        </div>
                      </div>
                    </Td>
                    <Td className="font-mono text-xs">{e.employeeCode}</Td>
                    <Td>{e.department ?? '—'}</Td>
                    <Td>{e.designation ?? '—'}</Td>
                    <Td>
                      <StatusBadge status={e.status} />
                    </Td>
                    <Td>{formatDate(e.joiningDate)}</Td>
                    <Td>
                      {e.hasLogin ? (
                        <KeyRound className="size-4 text-emerald-500" aria-label="Has login" />
                      ) : (
                        <span className="text-zinc-400">—</span>
                      )}
                    </Td>
                  </Tr>
                ))}
              </TBody>
            </Table>
            <Pagination page={page} pageSize={PAGE_SIZE} total={list.data.total} onPage={setPage} />
          </>
        )}
      </Card>
    </>
  );
}
