/** Columns every record carries for "Added … · Modified …"; audit diffs and history leave them out. */
export const STAMP_FIELDS = ['CreatedById', 'CreatedBy', 'ModifiedAtUtc', 'ModifiedById', 'ModifiedBy'];

const DATA_ACTIONS: Record<string, string> = { Created: 'Added', Updated: 'Modified', Deleted: 'Deleted' };

/** Data audit actions in the words screens use: Created -> Added, Updated -> Modified. Others are unchanged. */
export function auditActionLabel(action: string): string {
  return DATA_ACTIONS[action] ?? action;
}

/** Who did it: "Ali Raza (EMP-0042)" when the login has an employee record, else their email, else System. */
export function actorLabel(entry: { employeeName: string | null; employeeCode: string | null; userEmail: string | null }): string {
  if (entry.employeeName) return entry.employeeCode ? `${entry.employeeName} (${entry.employeeCode})` : entry.employeeName;
  return entry.userEmail ?? 'System';
}
