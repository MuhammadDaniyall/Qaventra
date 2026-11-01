import type { ReactNode } from 'react';
import { AttachmentList } from '../../components/attachments/AttachmentList';
import { RecordStamp } from '../../components/RecordStamp';
import { Drawer, StatusBadge } from '../../components/ui';
import { formatDate, formatDateTimeUtc, formatNumber } from '../../lib/format';
import type { LeaveRequest } from '../../types';
import { durationLabel } from './MyLeavePage';

/** One leave request in full, with who added and last changed it. Opened from a row on My leave or Leave approvals. */
export function LeaveDetailDrawer({ request, onClose }: { request: LeaveRequest | null; onClose: () => void }) {
  const r = request;
  const rows: Array<[string, ReactNode]> = r
    ? [
        ['Employee', `${r.employeeName} (${r.employeeCode})`],
        ['Type', r.leaveType],
        ['Dates', r.toDate !== r.fromDate ? `${formatDate(r.fromDate)} – ${formatDate(r.toDate)}` : formatDate(r.fromDate)],
        ['Duration', durationLabel(r)],
        ['Days', formatNumber(r.days)],
        ['Status', <StatusBadge key="s" status={r.status} />],
        ['Reason', r.reason],
        ['Decided', r.decidedAtUtc ? formatDateTimeUtc(r.decidedAtUtc) : null],
        ['Decision note', r.decisionNote],
      ]
    : [];

  return (
    <Drawer open={!!r} onClose={onClose} title="Leave request" description={r ? `${r.employeeName} · ${r.leaveType}` : undefined}>
      {r && (
        <div className="space-y-6">
          <RecordStamp stamps={r.stamps} />
          <dl className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {rows.map(([k, v]) => (
              <div key={k} className="grid grid-cols-[140px_1fr] gap-3 px-3 py-2 text-sm">
                <dt className="text-zinc-500 dark:text-zinc-400">{k}</dt>
                <dd className="min-w-0 break-words text-zinc-900 dark:text-zinc-100">
                  {v ?? <span className="text-zinc-500 dark:text-zinc-400">—</span>}
                </dd>
              </div>
            ))}
          </dl>
          {r.attachmentCount > 0 && <AttachmentList owner="Leave" ownerId={r.id} />}
        </div>
      )}
    </Drawer>
  );
}
