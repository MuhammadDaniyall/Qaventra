import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Boxes, CalendarClock, CreditCard, FileCheck2, Package, Pencil, Plus, Receipt, Trash2, Users } from 'lucide-react';
import { errorMessage, openProtectedFile } from '../../api/client';
import {
  billingApi,
  billingKeys,
  INVOICE_LABELS,
  INVOICE_TONES,
  platformBillingApi,
  type Invoice,
  type InvoiceStatus,
  type ModuleInfo,
  type PaymentStart,
  type Plan,
  type SavePlan,
} from '../../api/billing';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  EmptyState,
  ErrorState,
  Field,
  FormError,
  IconButton,
  Input,
  Modal,
  PageHeader,
  Select,
  SkeletonRows,
  StatCard,
  Table,
  TBody,
  Tabs,
  Td,
  Textarea,
  Th,
  THead,
  Tr,
  useToast,
} from '../../components/ui';
import { CYCLE_LABELS } from '../../lib/billing';
import { formatCurrency, formatDate, todayIso } from '../../lib/format';
import { addDays } from '../../lib/billing';

// ---------- Company admin: billing ----------

/** The company admin's billing page: package, what it costs, invoices and paying them. */
export function BillingPage() {
  const qc = useQueryClient();
  const toast = useToast();
  const billing = useQuery({ queryKey: billingKeys.billing, queryFn: billingApi.get });
  const [periods, setPeriods] = useState(1);
  const [paying, setPaying] = useState<Invoice | null>(null);
  const create = useMutation({
    mutationFn: () => billingApi.createInvoice(periods),
    onSuccess: (inv) => {
      qc.invalidateQueries({ queryKey: billingKeys.billing });
      setPaying(inv);
    },
    onError: (e) => toast.error(e),
  });
  const cancel = useMutation({
    mutationFn: (id: string) => billingApi.cancel(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: billingKeys.billing }),
    onError: (e) => toast.error(e),
  });

  if (billing.isLoading) return <SkeletonRows rows={5} cols={4} />;
  if (billing.isError) return <ErrorState error={billing.error} onRetry={() => billing.refetch()} />;
  const b = billing.data!;
  const enabled = b.enabledModules;
  return (
    <>
      <PageHeader title="Billing" description="Your package, invoices and payments." />
      <div className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Package" value={b.planName ?? 'Custom'} icon={Package} />
        <StatCard
          label="Per billing cycle"
          value={b.amountPerCycle === null ? '—' : formatCurrency(b.amountPerCycle)}
          hint={b.billingCycle ? CYCLE_LABELS[b.billingCycle] : undefined}
          icon={CreditCard}
          tone="green"
        />
        <StatCard label="Employees / limit" value={`${b.employees} / ${b.maxEmployees ?? '∞'}`} icon={Users} tone="violet" />
        <StatCard
          label={b.trialEndsOn && b.subscriptionEndsOn === b.trialEndsOn ? 'Free trial ends' : 'Paid until'}
          value={b.subscriptionEndsOn ? formatDate(b.subscriptionEndsOn) : 'No end date'}
          hint={b.daysLeft !== null ? `${b.daysLeft} days left` : undefined}
          icon={CalendarClock}
          tone={b.daysLeft !== null && b.daysLeft <= 7 ? 'red' : 'amber'}
        />
      </div>

      <Card className="mb-6">
        <CardHeader
          title="Pay"
          description="Pay ahead for one or more billing cycles. Your subscription is extended as soon as the payment is confirmed."
          actions={
            <div className="flex items-center gap-2">
              <Select value={periods} onChange={(e) => setPeriods(Number(e.target.value))} className="w-36" aria-label="Billing cycles">
                {[1, 2, 3, 6, 12].map((n) => (
                  <option key={n} value={n}>
                    {n} × {b.billingCycle ? CYCLE_LABELS[b.billingCycle] : 'cycle'}
                  </option>
                ))}
              </Select>
              <Button loading={create.isPending} disabled={!b.amountPerCycle} icon={<CreditCard className="size-4" />} onClick={() => create.mutate()}>
                Pay {b.amountPerCycle ? formatCurrency(b.amountPerCycle * periods) : ''}
              </Button>
            </div>
          }
        />
      </Card>

      <Card className="mb-6">
        <CardHeader title="Invoices" />
        {!b.invoices.length ? (
          <EmptyState icon={Receipt} title="No invoices yet" />
        ) : (
          <Table>
            <THead>
              <Tr>
                <Th>Invoice</Th>
                <Th>Date</Th>
                <Th>Amount</Th>
                <Th>Status</Th>
                <Th />
              </Tr>
            </THead>
            <TBody>
              {b.invoices.map((i) => (
                <Tr key={i.id}>
                  <Td>
                    <span className="font-mono text-xs">{i.number}</span>
                    <p className="text-xs text-zinc-500">{i.description}</p>
                    {i.reviewNote && i.status === 'Unpaid' && <p className="text-xs text-red-600">{i.reviewNote}</p>}
                  </Td>
                  <Td>{formatDate(i.issuedOn)}</Td>
                  <Td>{formatCurrency(i.amount)}</Td>
                  <Td>
                    <Badge tone={INVOICE_TONES[i.status]}>{INVOICE_LABELS[i.status]}</Badge>
                  </Td>
                  <Td>
                    {i.status === 'Unpaid' && (
                      <div className="flex justify-end gap-2">
                        <Button size="sm" onClick={() => setPaying(i)}>
                          Pay
                        </Button>
                        <Button size="sm" variant="ghost" loading={cancel.isPending} onClick={() => cancel.mutate(i.id)}>
                          Cancel
                        </Button>
                      </div>
                    )}
                  </Td>
                </Tr>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      <Card>
        <CardHeader title="Modules in your package" description="Modules outside your package are hidden. Ask us to add any you need." />
        <ul className="grid gap-2 p-5 sm:grid-cols-2 lg:grid-cols-3">
          {b.modules.map((m) => {
            const on = !enabled || enabled.includes(m.key);
            return (
              <li key={m.key} className="flex items-center gap-2 text-sm">
                <Badge tone={on ? 'green' : 'gray'}>{on ? 'On' : 'Off'}</Badge>
                <span className={on ? 'text-zinc-900 dark:text-white' : 'text-zinc-400'}>{m.label}</span>
              </li>
            );
          })}
        </ul>
      </Card>

      {paying && <PayModal invoice={paying} providers={b.providers} onClose={() => setPaying(null)} />}
    </>
  );
}

function PayModal({ invoice, providers, onClose }: { invoice: Invoice; providers: { key: string; label: string; online: boolean }[]; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [provider, setProvider] = useState(providers.find((p) => p.online)?.key ?? 'manual');
  const [start, setStart] = useState<PaymentStart | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [reference, setReference] = useState('');
  const pay = useMutation({
    mutationFn: () => billingApi.pay(invoice.id, provider),
    onSuccess: (s) => {
      if (s.redirectUrl) window.location.assign(s.redirectUrl);
      else setStart(s);
    },
  });
  const upload = useMutation({
    mutationFn: () => billingApi.uploadProof(invoice.id, file!, reference),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: billingKeys.billing });
      toast.success('Receipt sent', 'We will extend your subscription once the transfer is checked.');
      onClose();
    },
  });
  return (
    <Modal open onClose={onClose} title={`Pay ${formatCurrency(invoice.amount)}`} description={`Invoice ${invoice.number}`}>
      {!start ? (
        <div className="space-y-3">
          {providers.map((p) => (
            <label key={p.key} className="flex cursor-pointer items-center gap-3 rounded-lg border border-zinc-200 px-3 py-2 text-sm dark:border-zinc-700">
              <input type="radio" name="provider" checked={provider === p.key} onChange={() => setProvider(p.key)} />
              {p.label}
            </label>
          ))}
          <FormError message={pay.error ? errorMessage(pay.error) : null} />
          <Button className="w-full" loading={pay.isPending} onClick={() => pay.mutate()}>
            Continue
          </Button>
        </div>
      ) : (
        <div className="space-y-4">
          <pre className="whitespace-pre-wrap rounded-lg bg-zinc-50 p-3 font-sans text-sm text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200">{start.instructions}</pre>
          <Field label="Bank reference / transaction ID">
            <Input value={reference} maxLength={200} onChange={(e) => setReference(e.target.value)} />
          </Field>
          <Field label="Receipt (PDF or image, up to 5 MB)" required>
            <input type="file" accept="application/pdf,image/png,image/jpeg,image/webp" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </Field>
          <FormError message={upload.error ? errorMessage(upload.error) : null} />
          <Button className="w-full" disabled={!file} loading={upload.isPending} onClick={() => upload.mutate()}>
            Send receipt
          </Button>
        </div>
      )}
    </Modal>
  );
}

// ---------- Platform owner: packages and payments ----------

type PlatformTab = 'plans' | 'payments';

export function PlatformBillingPage() {
  const [tab, setTab] = useState<PlatformTab>('payments');
  return (
    <>
      <PageHeader title="Packages and payments" description="Packages companies can be put on, and bank transfers waiting for approval." />
      <Tabs<PlatformTab>
        className="mb-5"
        value={tab}
        onChange={setTab}
        items={[
          { value: 'payments', label: 'Payments to check' },
          { value: 'plans', label: 'Packages' },
        ]}
      />
      {tab === 'plans' ? <PlansTab /> : <PaymentsTab />}
    </>
  );
}

function PaymentsTab() {
  const qc = useQueryClient();
  const toast = useToast();
  const [status, setStatus] = useState<InvoiceStatus | 'all'>('AwaitingApproval');
  const invoices = useQuery({ queryKey: billingKeys.invoices(status), queryFn: () => platformBillingApi.invoices(status) });
  const [rejecting, setRejecting] = useState<Invoice | null>(null);
  const [note, setNote] = useState('');
  const done = () => qc.invalidateQueries({ queryKey: ['platform'] });
  const approve = useMutation({
    mutationFn: (id: string) => platformBillingApi.approve(id, null),
    onSuccess: () => {
      done();
      toast.success('Payment approved', 'The subscription has been extended.');
    },
    onError: (e) => toast.error(e),
  });
  const reject = useMutation({
    mutationFn: () => platformBillingApi.reject(rejecting!.id, note),
    onSuccess: () => {
      done();
      setRejecting(null);
      setNote('');
    },
  });
  return (
    <Card>
      <CardHeader
        title="Invoices"
        actions={
          <Select value={status} onChange={(e) => setStatus(e.target.value as InvoiceStatus | 'all')} className="w-44" aria-label="Status">
            <option value="AwaitingApproval">Being checked</option>
            <option value="Unpaid">Unpaid</option>
            <option value="Paid">Paid</option>
            <option value="all">All</option>
          </Select>
        }
      />
      {invoices.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : !invoices.data?.length ? (
        <EmptyState icon={FileCheck2} title="Nothing here" />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Company</Th>
              <Th>Invoice</Th>
              <Th>Amount</Th>
              <Th>Reference</Th>
              <Th>Status</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {invoices.data.map((i) => (
              <Tr key={i.id}>
                <Td>{i.company}</Td>
                <Td>
                  <span className="font-mono text-xs">{i.number}</span>
                  <p className="text-xs text-zinc-500">{formatDate(i.issuedOn)}</p>
                </Td>
                <Td>{formatCurrency(i.amount)}</Td>
                <Td className="text-xs">
                  {i.providerReference ?? '—'}
                  {i.hasProof && (
                    <button className="ms-2 text-accent-600 hover:underline" onClick={() => openProtectedFile(platformBillingApi.proofPath(i.id), `${i.number}-receipt`)}>
                      Receipt
                    </button>
                  )}
                </Td>
                <Td>
                  <Badge tone={INVOICE_TONES[i.status]}>{INVOICE_LABELS[i.status]}</Badge>
                </Td>
                <Td>
                  {(i.status === 'AwaitingApproval' || i.status === 'Unpaid') && (
                    <div className="flex justify-end gap-2">
                      <Button size="sm" variant="success" loading={approve.isPending} onClick={() => approve.mutate(i.id)}>
                        Approve
                      </Button>
                      {i.status === 'AwaitingApproval' && (
                        <Button size="sm" variant="secondary" onClick={() => setRejecting(i)}>
                          Turn down
                        </Button>
                      )}
                    </div>
                  )}
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      <Modal
        open={!!rejecting}
        onClose={() => setRejecting(null)}
        title="Turn down this receipt"
        footer={
          <Button variant="danger" disabled={!note.trim()} loading={reject.isPending} onClick={() => reject.mutate()}>
            Turn down
          </Button>
        }
      >
        <Field label="Reason (the company sees it)" required>
          <Textarea rows={3} value={note} onChange={(e) => setNote(e.target.value)} />
        </Field>
        <FormError message={reject.error ? errorMessage(reject.error) : null} />
      </Modal>
    </Card>
  );
}

function PlansTab() {
  const qc = useQueryClient();
  const toast = useToast();
  const plans = useQuery({ queryKey: billingKeys.plans, queryFn: platformBillingApi.plans });
  const modules = useQuery({ queryKey: billingKeys.modules, queryFn: platformBillingApi.modules });
  const [editing, setEditing] = useState<Plan | 'new' | null>(null);
  const remove = useMutation({
    mutationFn: (id: string) => platformBillingApi.deletePlan(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: billingKeys.plans }),
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <CardHeader
        title="Packages"
        actions={
          <Button size="sm" icon={<Plus className="size-4" />} onClick={() => setEditing('new')}>
            New package
          </Button>
        }
      />
      {plans.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : !plans.data?.length ? (
        <EmptyState icon={Boxes} title="No packages yet" description="Companies without a package keep every module." />
      ) : (
        <Table>
          <THead>
            <Tr>
              <Th>Package</Th>
              <Th>Price / month</Th>
              <Th>Employees</Th>
              <Th>Modules</Th>
              <Th>Trial</Th>
              <Th>Companies</Th>
              <Th />
            </Tr>
          </THead>
          <TBody>
            {plans.data.map((p) => (
              <Tr key={p.id}>
                <Td>
                  <span className="font-medium">{p.name}</span>
                  {!p.isActive && (
                    <span className="ms-2">
                      <Badge tone="gray">Off</Badge>
                    </span>
                  )}
                </Td>
                <Td>
                  {p.flatPrice > 0 && formatCurrency(p.flatPrice)}
                  {p.flatPrice > 0 && p.pricePerEmployee > 0 && ' + '}
                  {p.pricePerEmployee > 0 && `${formatCurrency(p.pricePerEmployee)} / employee`}
                </Td>
                <Td>{p.maxEmployees ?? '∞'}</Td>
                <Td>
                  {p.modules.length} / {modules.data?.length ?? '…'}
                </Td>
                <Td>{p.trialDays ? `${p.trialDays} days` : '—'}</Td>
                <Td>{p.companies}</Td>
                <Td>
                  <div className="flex justify-end gap-1">
                    <IconButton label="Edit" onClick={() => setEditing(p)}>
                      <Pencil className="size-4" />
                    </IconButton>
                    <IconButton label="Delete" disabled={p.companies > 0} onClick={() => remove.mutate(p.id)}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </div>
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && modules.data && <PlanModal plan={editing === 'new' ? null : editing} modules={modules.data} onClose={() => setEditing(null)} />}
    </Card>
  );
}

function ModulePicker({ modules, value, onChange }: { modules: ModuleInfo[]; value: string[]; onChange: (v: string[]) => void }) {
  return (
    <div className="grid gap-2 sm:grid-cols-2">
      {modules.map((m) => (
        <Checkbox
          key={m.key}
          label={m.label}
          disabled={m.core}
          checked={m.core || value.includes(m.key)}
          onChange={(e) => onChange(e.target.checked ? [...value, m.key] : value.filter((k) => k !== m.key))}
        />
      ))}
    </div>
  );
}

function PlanModal({ plan, modules, onClose }: { plan: Plan | null; modules: ModuleInfo[]; onClose: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState<SavePlan>(
    plan ?? {
      name: '',
      description: null,
      pricePerEmployee: 0,
      flatPrice: 0,
      maxEmployees: null,
      modules: modules.map((m) => m.key),
      trialDays: 14,
      isActive: true,
      sortOrder: 0,
    },
  );
  const set = (p: Partial<SavePlan>) => setForm((f) => ({ ...f, ...p }));
  const save = useMutation({
    mutationFn: () => (plan ? platformBillingApi.updatePlan(plan.id, form) : platformBillingApi.createPlan(form)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: billingKeys.plans });
      onClose();
    },
  });
  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={plan ? 'Edit package' : 'New package'}
      footer={
        <Button loading={save.isPending} disabled={!form.name.trim()} onClick={() => save.mutate()}>
          Save
        </Button>
      }
    >
      <div className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Name" required>
            <Input value={form.name} maxLength={100} onChange={(e) => set({ name: e.target.value })} />
          </Field>
          <Field label="Free trial (days)">
            <Input type="number" min={0} max={365} value={form.trialDays} onChange={(e) => set({ trialDays: Number(e.target.value) })} />
          </Field>
          <Field label="Price per employee / month (PKR)">
            <Input type="number" min={0} value={form.pricePerEmployee} onChange={(e) => set({ pricePerEmployee: Number(e.target.value) })} />
          </Field>
          <Field label="Flat price / month (PKR)">
            <Input type="number" min={0} value={form.flatPrice} onChange={(e) => set({ flatPrice: Number(e.target.value) })} />
          </Field>
          <Field label="Employee limit" hint="Empty = unlimited.">
            <Input
              type="number"
              min={1}
              value={form.maxEmployees ?? ''}
              onChange={(e) => set({ maxEmployees: e.target.value ? Number(e.target.value) : null })}
            />
          </Field>
          <div className="flex items-end pb-2">
            <Checkbox label="Can be offered" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
          </div>
        </div>
        <Field label="Description">
          <Textarea rows={2} value={form.description ?? ''} onChange={(e) => set({ description: e.target.value || null })} />
        </Field>
        <div>
          <p className="mb-2 text-sm font-medium text-zinc-900 dark:text-white">Modules</p>
          <ModulePicker modules={modules} value={form.modules} onChange={(m) => set({ modules: m })} />
        </div>
        <FormError message={save.error ? errorMessage(save.error) : null} />
      </div>
    </Modal>
  );
}

/** On the company page: which package the company is on, its trial, and its modules. */
export function CompanyPlanCard({ companyId }: { companyId: string }) {
  const qc = useQueryClient();
  const toast = useToast();
  const current = useQuery({ queryKey: billingKeys.companyPlan(companyId), queryFn: () => platformBillingApi.companyPlan(companyId) });
  const plans = useQuery({ queryKey: billingKeys.plans, queryFn: platformBillingApi.plans });
  const [open, setOpen] = useState(false);
  const [planId, setPlanId] = useState<string>('');
  const [trial, setTrial] = useState('');
  const [custom, setCustom] = useState<string[] | null>(null);
  const save = useMutation({
    mutationFn: () => platformBillingApi.setCompanyPlan(companyId, { planId: planId || null, trialEndsOn: trial || null, modules: custom }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['platform'] });
      toast.success('Package saved');
      setOpen(false);
    },
  });
  const c = current.data;
  const edit = () => {
    setPlanId(c?.planId ?? '');
    setTrial(c?.trialEndsOn ?? '');
    setCustom(null);
    setOpen(true);
  };
  const choose = (id: string) => {
    setPlanId(id);
    const p = plans.data?.find((x) => x.id === id);
    if (p?.trialDays && !trial) setTrial(addDays(todayIso(), p.trialDays));
  };
  return (
    <Card className="mt-6">
      <CardHeader
        title="Package and modules"
        description={
          c
            ? `${c.planName ?? 'No package'} · ${c.enabledModules ? `${c.enabledModules.length} of ${c.modules.length} modules` : 'every module'}${
                c.trialEndsOn ? ` · trial until ${formatDate(c.trialEndsOn)}` : ''
              }`
            : undefined
        }
        actions={
          <Button size="sm" variant="secondary" onClick={edit} disabled={!c}>
            Change
          </Button>
        }
      />
      {c && (
        <Modal
          open={open}
          onClose={() => setOpen(false)}
          size="lg"
          title="Package and modules"
          description="Prices and the employee limit are copied from the package. Turned-off modules are hidden and blocked."
          footer={
            <Button loading={save.isPending} onClick={() => save.mutate()}>
              Save
            </Button>
          }
        >
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Package">
                <Select value={planId} onChange={(e) => choose(e.target.value)}>
                  <option value="">No package (every module)</option>
                  {plans.data?.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Free trial until">
                <Input type="date" value={trial} onChange={(e) => setTrial(e.target.value)} />
              </Field>
            </div>
            <Checkbox
              label="Choose modules for this company only"
              checked={custom !== null}
              onChange={(e) => setCustom(e.target.checked ? c.enabledModules ?? c.modules.map((m) => m.key) : null)}
            />
            {custom !== null && <ModulePicker modules={c.modules} value={custom} onChange={setCustom} />}
            <FormError message={save.error ? errorMessage(save.error) : null} />
          </div>
        </Modal>
      )}
    </Card>
  );
}
