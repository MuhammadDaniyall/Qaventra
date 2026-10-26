import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { IdCard, Printer } from 'lucide-react';
import qrcode from 'qrcode-generator';
import { http } from '../../api/client';
import { qk } from '../../api/keys';
import { orgApi } from '../../api/organization';
import { useUser } from '../../auth/AuthContext';
import { Button, Card, CardBody, CardHeader, Checkbox, EmptyState, ErrorState, Field, Input, PageHeader, Select, SkeletonRows } from '../../components/ui';
import { LogoMark } from '../../components/layout/Logo';
import { formatDate, initials } from '../../lib/format';
import { tx } from '../../lib/i18n';

interface CardData {
  id: string;
  employeeCode: string;
  name: string;
  designation: string | null;
  department: string | null;
  bloodGroup: string | null;
  joiningDate: string;
  phone: string | null;
}

interface Template {
  color: string;
  showBloodGroup: boolean;
  showJoining: boolean;
  showPhone: boolean;
  showQr: boolean;
  footer: string;
}

const KEY = 'hrms.id-card-template';
const DEFAULT: Template = { color: '#4F46E5', showBloodGroup: true, showJoining: true, showPhone: false, showQr: true, footer: 'If found, please return to the HR office.' };

function readTemplate(): Template {
  try {
    return { ...DEFAULT, ...(JSON.parse(localStorage.getItem(KEY) ?? '{}') as Partial<Template>) };
  } catch {
    return DEFAULT;
  }
}

/** QR as an SVG data string: who the card belongs to, readable by any phone camera. */
function qrSvg(text: string): string {
  const qr = qrcode(0, 'M');
  qr.addData(text);
  qr.make();
  return qr.createSvgTag({ cellSize: 2, margin: 0, scalable: true });
}

/** Employee ID cards (85.6 × 54 mm, the bank-card size) with a simple template, printed on A4 sheets. */
export function IdCardsPage() {
  const user = useUser();
  const company = user.companyName ?? '';
  const [department, setDepartment] = useState('');
  const [tpl, setTpl] = useState<Template>(readTemplate);
  const [picked, setPicked] = useState<Set<string> | null>(null);
  const departments = useQuery({ queryKey: qk.departments, queryFn: orgApi.departments });
  const cards = useQuery({
    queryKey: ['employees', 'id-cards', department],
    queryFn: () => http.get<CardData[]>('/employees/id-cards', { departmentId: department || undefined }),
  });
  const set = (patch: Partial<Template>) =>
    setTpl((t) => {
      const next = { ...t, ...patch };
      try {
        localStorage.setItem(KEY, JSON.stringify(next));
      } catch {
        // ignore
      }
      return next;
    });

  const all = cards.data ?? [];
  const selected = picked ? all.filter((c) => picked.has(c.id)) : all;
  const toggle = (id: string) =>
    setPicked((p) => {
      const next = new Set(p ?? all.map((c) => c.id));
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return (
    <>
      <div className="no-print">
        <PageHeader
          title="ID cards"
          description="Pick people, set the look once, and print on A4. Each card is the size of a bank card."
          back={{ to: '/employees', label: 'Employees' }}
          actions={
            <Button icon={<Printer className="size-4" />} disabled={!selected.length} onClick={() => window.print()}>
              {`${tx('Print')} ${selected.length}`}
            </Button>
          }
        />
        <div className="mb-6 grid gap-6 lg:grid-cols-[20rem_1fr]">
          <Card>
            <CardHeader title="Card design" />
            <CardBody className="space-y-4">
              <Field label="Colour">
                <div className="flex items-center gap-3">
                  <input type="color" value={tpl.color} onChange={(e) => set({ color: e.target.value })} className="h-10 w-14 cursor-pointer rounded-lg border border-zinc-200 dark:border-zinc-700" aria-label="Card colour" />
                  <span className="font-mono text-sm text-zinc-500 dark:text-zinc-400">{tpl.color}</span>
                </div>
              </Field>
              <Checkbox label="Blood group" checked={tpl.showBloodGroup} onChange={(e) => set({ showBloodGroup: e.target.checked })} />
              <Checkbox label="Joining date" checked={tpl.showJoining} onChange={(e) => set({ showJoining: e.target.checked })} />
              <Checkbox label="Phone number" checked={tpl.showPhone} onChange={(e) => set({ showPhone: e.target.checked })} />
              <Checkbox label="QR code" description="Scans to the company, employee code and name" checked={tpl.showQr} onChange={(e) => set({ showQr: e.target.checked })} />
              <Field label="Back line">
                <Input value={tpl.footer} maxLength={80} onChange={(e) => set({ footer: e.target.value })} />
              </Field>
            </CardBody>
          </Card>
          <Card>
            <CardHeader
              title="People"
              description={`${selected.length} / ${all.length}`}
              actions={
                <Select className="w-48" value={department} onChange={(e) => (setDepartment(e.target.value), setPicked(null))} aria-label="Department">
                  <option value="">{tx('All departments')}</option>
                  {(departments.data ?? []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </Select>
              }
            />
            {cards.isLoading ? (
              <SkeletonRows rows={4} cols={2} />
            ) : cards.isError ? (
              <ErrorState error={cards.error} onRetry={() => cards.refetch()} />
            ) : !all.length ? (
              <EmptyState icon={IdCard} title="No current employees here" className="py-8" />
            ) : (
              <ul className="grid max-h-80 gap-x-4 overflow-y-auto px-5 py-3 sm:grid-cols-2">
                {all.map((c) => (
                  <li key={c.id}>
                    <Checkbox label={`${c.name} · ${c.employeeCode}`} checked={!picked || picked.has(c.id)} onChange={() => toggle(c.id)} />
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
        {selected[0] && (
          <div className="mb-6">
            <p className="mb-3 text-sm font-semibold text-zinc-900 dark:text-white">{tx('Preview')}</p>
            <div className="flex flex-wrap gap-4">
              <IdCardFront card={selected[0]} tpl={tpl} company={company} />
              <IdCardBack tpl={tpl} company={company} />
            </div>
          </div>
        )}
      </div>
      {/* Only this part prints: fronts in a grid, ten to an A4 sheet. */}
      <div className="print-area hidden print:grid print:grid-cols-2 print:gap-[6mm]">
        {selected.map((c) => (
          <IdCardFront key={c.id} card={c} tpl={tpl} company={company} />
        ))}
      </div>
    </>
  );
}

function IdCardFront({ card, tpl, company }: { card: CardData; tpl: Template; company: string }) {
  const qr = useMemo(() => (tpl.showQr ? qrSvg(`${company} | ${card.employeeCode} | ${card.name}`) : ''), [tpl.showQr, company, card]);
  return (
    <div className="relative flex h-[54mm] w-[85.6mm] shrink-0 flex-col overflow-hidden rounded-[3mm] border border-zinc-200 bg-white text-zinc-900 shadow-sm print:break-inside-avoid print:shadow-none" dir="ltr">
      <div className="flex items-center gap-[2mm] px-[3mm] py-[2mm] text-white" style={{ background: tpl.color }}>
        <LogoMark className="size-[6mm] rounded-[1.2mm] ring-1 ring-white/40" />
        <span className="truncate text-[3mm] font-semibold tracking-wide">{company}</span>
      </div>
      <div className="flex flex-1 gap-[3mm] px-[3mm] py-[2.5mm]">
        <div
          className="flex h-[24mm] w-[19mm] shrink-0 items-center justify-center rounded-[1.5mm] text-[6mm] font-semibold"
          style={{ background: `${tpl.color}1a`, color: tpl.color }}
        >
          {initials(card.name)}
        </div>
        <div className="min-w-0 flex-1 text-[2.4mm] leading-snug">
          <p className="line-clamp-2 text-[3.4mm] leading-tight font-bold">{card.name}</p>
          <p className="truncate text-zinc-600">{card.designation ?? ''}</p>
          <p className="truncate text-zinc-600">{card.department ?? ''}</p>
          <dl className="mt-[1.5mm] grid grid-cols-[auto_1fr] gap-x-[2mm]">
            <dt className="text-zinc-500">ID</dt>
            <dd className="font-mono font-semibold">{card.employeeCode}</dd>
            {tpl.showBloodGroup && card.bloodGroup && (
              <>
                <dt className="text-zinc-500">Blood</dt>
                <dd className="font-semibold text-red-700">{card.bloodGroup}</dd>
              </>
            )}
            {tpl.showJoining && (
              <>
                <dt className="text-zinc-500">Since</dt>
                <dd>{formatDate(card.joiningDate)}</dd>
              </>
            )}
            {tpl.showPhone && card.phone && (
              <>
                <dt className="text-zinc-500">Phone</dt>
                <dd>{card.phone}</dd>
              </>
            )}
          </dl>
        </div>
        {qr && <div className="size-[16mm] shrink-0 self-end [&>svg]:size-full" dangerouslySetInnerHTML={{ __html: qr }} />}
      </div>
      <div className="h-[1.5mm]" style={{ background: tpl.color }} />
    </div>
  );
}

function IdCardBack({ tpl, company }: { tpl: Template; company: string }) {
  return (
    <div className="flex h-[54mm] w-[85.6mm] shrink-0 flex-col items-center justify-center gap-[2mm] rounded-[3mm] border border-zinc-200 bg-white p-[4mm] text-center text-zinc-700 shadow-sm" dir="ltr">
      <p className="text-[3.2mm] font-semibold" style={{ color: tpl.color }}>
        {company}
      </p>
      <p className="text-[2.6mm]">{tpl.footer}</p>
      <div className="mt-[3mm] h-px w-[50mm] bg-zinc-300" />
      <p className="text-[2.2mm] text-zinc-500">Authorised signature</p>
    </div>
  );
}
