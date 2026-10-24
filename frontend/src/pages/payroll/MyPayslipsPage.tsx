import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { ChevronRight, FileText } from 'lucide-react';
import { payrollApi } from '../../api/payroll';
import { qk } from '../../api/keys';
import { Card, EmptyState, ErrorState, PageHeader, SkeletonRows, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { formatCurrency, formatNumber, monthLabel } from '../../lib/format';

export function MyPayslipsPage() {
  const navigate = useNavigate();
  const slips = useQuery({ queryKey: qk.myPayslips, queryFn: payrollApi.myPayslips });

  return (
    <>
      <PageHeader title="My payslips" description="Payslips appear here once HR finalizes the month's payroll." />
      <Card>
        {slips.isLoading ? (
          <SkeletonRows rows={4} cols={4} />
        ) : slips.isError ? (
          <ErrorState error={slips.error} onRetry={() => slips.refetch()} />
        ) : !slips.data?.length ? (
          <EmptyState icon={FileText} title="No payslips yet" description="Your first payslip will show up after payroll is finalized." />
        ) : (
          <Table>
            <THead>
              <tr>
                <Th>Month</Th>
                <Th className="text-end">Paid days</Th>
                <Th className="text-end">Gross</Th>
                <Th className="text-end">Income tax</Th>
                <Th className="text-end">Net pay</Th>
                <Th>
                  <span className="sr-only">Open</span>
                </Th>
              </tr>
            </THead>
            <TBody>
              {slips.data.map((p) => {
                return (
                  <Tr key={p.id} onClick={() => navigate(`/payslips/${p.id}`)}>
                    <Td className="font-medium text-zinc-900 dark:text-zinc-100">
                      <span className="inline-flex items-center gap-2">
                        <FileText className="size-4 text-zinc-500 dark:text-zinc-400" />
                        {monthLabel(p.year, p.month)}
                      </span>
                    </Td>
                    <Td className="text-end tabular-nums">{formatNumber(p.paidDays)}</Td>
                    <Td className="text-end tabular-nums">{formatCurrency(p.grossEarnings)}</Td>
                    <Td className="text-end tabular-nums">{formatCurrency(p.incomeTax)}</Td>
                    <Td className="text-end font-semibold text-zinc-900 tabular-nums dark:text-white">{formatCurrency(p.netPay)}</Td>
                    <Td className="text-end">
                      <ChevronRight className="ms-auto size-4 text-zinc-500 dark:text-zinc-400" />
                    </Td>
                  </Tr>
                );
              })}
            </TBody>
          </Table>
        )}
      </Card>
    </>
  );
}
