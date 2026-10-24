import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, Plus, Trash2, Zap } from 'lucide-react';
import {
  approvalsApi,
  APPROVER_LABELS,
  REQUEST_LABELS,
  type ApprovalFlow,
  type ApproverKind,
  type FlowStep,
} from '../../api/approvals';
import { useEmployeeLookup } from '../../api/hooks';
import { qk } from '../../api/keys';
import { Badge, Button, Card, CardBody, CardHeader, ErrorState, IconButton, Select, Skeleton, useToast } from '../../components/ui';

const KINDS: ApproverKind[] = ['LineManager', 'SecondLevelManager', 'DepartmentHead', 'Hr', 'SpecificEmployee'];
const HINTS: Record<ApproverKind, string> = {
  LineManager: 'The employee’s reporting manager. HR when there is none.',
  SecondLevelManager: 'The line manager’s own manager. Skipped when there is none.',
  Hr: 'Any HR manager or company admin.',
  SpecificEmployee: 'One named person, e.g. the CFO for expenses.',
  DepartmentHead: 'The head of the employee’s department (set under Departments). Skipped when there is none.',
};

export function ApprovalFlowsTab() {
  const flows = useQuery({ queryKey: qk.approvalFlows, queryFn: approvalsApi.flows });

  if (flows.isError) {
    return (
      <Card>
        <ErrorState error={flows.error} onRetry={() => flows.refetch()} />
      </Card>
    );
  }
  return (
    <div className="space-y-6">
      <p className="max-w-3xl text-sm text-zinc-600 dark:text-zinc-400">
        Decide who approves each kind of request, in order. A request moves to the next person only after the previous
        one approves; any rejection ends it. Nobody ever approves their own request, and HR can always step in.
      </p>
      {flows.isLoading
        ? Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-48 w-full rounded-xl" />)
        : flows.data?.map((f) => <FlowCard key={f.requestType} flow={f} />)}
    </div>
  );
}

function FlowCard({ flow }: { flow: ApprovalFlow }) {
  const toast = useToast();
  const qc = useQueryClient();
  const employees = useEmployeeLookup();
  const [steps, setSteps] = useState<FlowStep[]>(flow.steps);
  useEffect(() => setSteps(flow.steps), [flow.steps]);

  const dirty = JSON.stringify(steps.map((s) => [s.kind, s.employeeId])) !== JSON.stringify(flow.steps.map((s) => [s.kind, s.employeeId]));
  const invalid = steps.some((s) => s.kind === 'SpecificEmployee' && !s.employeeId);

  const save = useMutation({
    mutationFn: () => approvalsApi.saveFlow(flow.requestType, steps.map((s) => ({ kind: s.kind, employeeId: s.kind === 'SpecificEmployee' ? s.employeeId : null }))),
    onSuccess: () => {
      toast.success('Approval chain saved', `${REQUEST_LABELS[flow.requestType]}: applies to new requests.`);
      qc.invalidateQueries({ queryKey: qk.approvalFlows });
    },
    onError: (err) => toast.error(err),
  });

  const update = (i: number, patch: Partial<FlowStep>) => setSteps((l) => l.map((s, j) => (j === i ? { ...s, ...patch } : s)));
  const move = (i: number, by: number) =>
    setSteps((l) => {
      const next = [...l];
      [next[i], next[i + by]] = [next[i + by], next[i]];
      return next;
    });

  return (
    <Card>
      <CardHeader
        title={REQUEST_LABELS[flow.requestType]}
        description={steps.length ? `${steps.length} step${steps.length === 1 ? '' : 's'}` : 'Approved automatically'}
        actions={flow.isDefault && !dirty ? <Badge>Default</Badge> : undefined}
      />
      <CardBody className="space-y-3">
        {steps.length === 0 && (
          <p className="flex items-center gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">
            <Zap className="size-4 shrink-0" aria-hidden="true" />
            No approvers: every request is approved the moment it is submitted.
          </p>
        )}
        <ol className="space-y-2">
          {steps.map((step, i) => (
            <li key={i} className="flex flex-wrap items-center gap-2 rounded-lg border border-zinc-200 p-2.5 dark:border-zinc-800">
              <span className="flex size-6 items-center justify-center rounded-full bg-accent-600 text-xs font-semibold text-white">{i + 1}</span>
              <Select
                aria-label={`Step ${i + 1} approver`}
                value={step.kind}
                onChange={(e) => update(i, { kind: e.target.value as ApproverKind, employeeId: null })}
                className="w-48"
              >
                {KINDS.map((k) => (
                  <option key={k} value={k}>
                    {APPROVER_LABELS[k]}
                  </option>
                ))}
              </Select>
              {step.kind === 'SpecificEmployee' ? (
                <Select
                  aria-label={`Step ${i + 1} person`}
                  value={step.employeeId ?? ''}
                  onChange={(e) => update(i, { employeeId: e.target.value || null })}
                  className="min-w-56 flex-1"
                >
                  <option value="">Choose a person…</option>
                  {employees.data?.map((e) => (
                    <option key={e.id} value={e.id}>
                      {e.name}
                    </option>
                  ))}
                </Select>
              ) : (
                <span className="min-w-40 flex-1 text-xs text-zinc-500 dark:text-zinc-400">{HINTS[step.kind]}</span>
              )}
              <div className="flex">
                <IconButton label="Move up" disabled={i === 0} onClick={() => move(i, -1)}>
                  <ArrowUp className="size-4" />
                </IconButton>
                <IconButton label="Move down" disabled={i === steps.length - 1} onClick={() => move(i, 1)}>
                  <ArrowDown className="size-4" />
                </IconButton>
                <IconButton label={`Remove step ${i + 1}`} onClick={() => setSteps((l) => l.filter((_, j) => j !== i))}>
                  <Trash2 className="size-4" />
                </IconButton>
              </div>
            </li>
          ))}
        </ol>
        <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
          <Button
            variant="secondary"
            size="sm"
            icon={<Plus className="size-4" />}
            disabled={steps.length >= 5}
            onClick={() => setSteps((l) => [...l, { kind: l.length ? 'Hr' : 'LineManager', employeeId: null }])}
          >
            Add step
          </Button>
          <div className="flex gap-2">
            {dirty && (
              <Button variant="ghost" size="sm" onClick={() => setSteps(flow.steps)}>
                Undo changes
              </Button>
            )}
            <Button size="sm" disabled={!dirty || invalid} loading={save.isPending} onClick={() => save.mutate()}>
              Save chain
            </Button>
          </div>
        </div>
      </CardBody>
    </Card>
  );
}
