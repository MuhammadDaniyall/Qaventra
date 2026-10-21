import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, CheckCircle2, Download, FileSpreadsheet, Info, Upload } from 'lucide-react';
import { downloadFile, excelApi, type ImportKind, type ImportResult } from '../../api/excel';
import { errorMessage } from '../../api/client';
import { cn } from '../../lib/cn';
import { toIsoDate, todayIso } from '../../lib/format';
import { Button, Checkbox, Field, FormError, Input, Modal, useToast } from '../ui';

/** Downloads an Excel export. */
export function ExportButton({ path, fileName, label = 'Export' }: { path: string; fileName: string; label?: string }) {
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  return (
    <Button
      variant="secondary"
      icon={<Download className="size-4" />}
      loading={busy}
      onClick={async () => {
        setBusy(true);
        try {
          await downloadFile(path, fileName);
        } catch (err) {
          toast.error(err);
        } finally {
          setBusy(false);
        }
      }}
    >
      {label}
    </Button>
  );
}

/** Export for a date range: asks for the dates (default: this month so far), then downloads. */
export function ExportRangeButton({ path, name, label = 'Export' }: { path: string; name: string; label?: string }) {
  const toast = useToast();
  const [open, setOpen] = useState(false);
  const now = new Date();
  const [from, setFrom] = useState(toIsoDate(new Date(now.getFullYear(), now.getMonth(), 1)));
  const [to, setTo] = useState(todayIso());
  const [busy, setBusy] = useState(false);
  const presets: Array<[string, () => [string, string]]> = [
    ['This month', () => [toIsoDate(new Date(now.getFullYear(), now.getMonth(), 1)), todayIso()]],
    ['Last month', () => [toIsoDate(new Date(now.getFullYear(), now.getMonth() - 1, 1)), toIsoDate(new Date(now.getFullYear(), now.getMonth(), 0))]],
    ['Last 3 months', () => [toIsoDate(new Date(now.getFullYear(), now.getMonth() - 2, 1)), todayIso()]],
    ['This year', () => [toIsoDate(new Date(now.getFullYear(), 0, 1)), todayIso()]],
  ];

  const go = async () => {
    setBusy(true);
    try {
      await downloadFile(`${path}?from=${from}&to=${to}`, `${name}-${from}-to-${to}.xlsx`);
      setOpen(false);
    } catch (err) {
      toast.error(err);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Button variant="secondary" icon={<Download className="size-4" />} onClick={() => setOpen(true)}>
        {label}
      </Button>
      {open && (
        <Modal
          open
          size="sm"
          onClose={() => setOpen(false)}
          title="Export to Excel"
          footer={
            <>
              <Button variant="secondary" onClick={() => setOpen(false)}>
                Cancel
              </Button>
              <Button icon={<Download className="size-4" />} loading={busy} disabled={!from || !to || to < from} onClick={go}>
                Download
              </Button>
            </>
          }
        >
          <div className="space-y-4">
            <div className="flex flex-wrap gap-1.5">
              {presets.map(([text, range]) => (
                <button
                  key={text}
                  type="button"
                  onClick={() => {
                    const [f, t] = range();
                    setFrom(f);
                    setTo(t);
                  }}
                  className="rounded-full border border-zinc-200 px-2.5 py-1 text-xs font-medium text-zinc-600 hover:border-accent-400 hover:text-accent-700 dark:border-zinc-700 dark:text-zinc-300"
                >
                  {text}
                </button>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-3">
              <Field label="From">
                <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </Field>
              <Field label="To">
                <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} />
              </Field>
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}

const TITLES: Record<ImportKind, { title: string; what: string }> = {
  employees: { title: 'Import employees', what: 'employees' },
  attendance: { title: 'Import attendance', what: 'attendance rows' },
};

/** Template → upload → row-by-row check → import (all or nothing). */
export function ImportButton({ kind, invalidate }: { kind: ImportKind; invalidate: ReadonlyArray<readonly unknown[]> }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button variant="secondary" icon={<Upload className="size-4" />} onClick={() => setOpen(true)}>
        Import
      </Button>
      {open && <ImportDialog kind={kind} invalidate={invalidate} onClose={() => setOpen(false)} />}
    </>
  );
}

function ImportDialog({ kind, invalidate, onClose }: { kind: ImportKind; invalidate: ReadonlyArray<readonly unknown[]>; onClose: () => void }) {
  const toast = useToast();
  const qc = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [createMissing, setCreateMissing] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const t = TITLES[kind];

  const check = useMutation({
    mutationFn: ({ f, missing }: { f: File; missing: boolean }) => excelApi.upload(kind, f, false, missing),
    onSuccess: setResult,
    onError: (err) => {
      setResult(null);
      setError(errorMessage(err));
    },
  });
  const commit = useMutation({
    mutationFn: () => excelApi.upload(kind, file!, true, createMissing),
    onSuccess: (r) => {
      toast.success(`Imported ${r.created + r.updated} ${t.what}`, r.updated ? `${r.created} new, ${r.updated} replaced` : undefined);
      invalidate.forEach((key) => qc.invalidateQueries({ queryKey: key }));
      onClose();
    },
    onError: (err) => setError(errorMessage(err)),
  });

  const choose = (f: File | undefined) => {
    if (!f) return;
    setError(null);
    if (!/\.xlsx$/i.test(f.name)) return setError('Choose an .xlsx file (Excel). Download the template if you need one.');
    if (f.size > 5 * 1024 * 1024) return setError('The file must be smaller than 5 MB.');
    setFile(f);
    check.mutate({ f, missing: createMissing });
  };

  const problems = result?.rows.filter((r) => r.errors.length) ?? [];
  const notes = result?.rows.filter((r) => r.notes.length && !r.errors.length) ?? [];
  const ready = !!result && result.total > 0 && result.invalid === 0;

  return (
    <Modal
      open
      onClose={onClose}
      size="lg"
      title={t.title}
      description="Nothing is saved until every row is correct."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button icon={<Upload className="size-4" />} disabled={!ready} loading={commit.isPending} onClick={() => commit.mutate()}>
            {result ? `Import ${result.valid} ${t.what}` : 'Import'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <ol className="space-y-3 text-sm">
          <li className="flex items-start gap-3">
            <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-accent-600 text-xs font-semibold text-white">1</span>
            <div className="flex-1">
              <p className="text-zinc-700 dark:text-zinc-300">Download the template and fill it in.</p>
              <Button variant="ghost" size="sm" className="mt-1 -ml-2" icon={<FileSpreadsheet className="size-4" />} onClick={() => excelApi.template(kind).catch((e) => toast.error(e))}>
                Download template
              </Button>
            </div>
          </li>
          <li className="flex items-start gap-3">
            <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-accent-600 text-xs font-semibold text-white">2</span>
            <div className="flex-1 space-y-2">
              <p className="text-zinc-700 dark:text-zinc-300">Upload it. We check every row first.</p>
              {kind === 'employees' && (
                <Checkbox
                  checked={createMissing}
                  onChange={(e) => {
                    setCreateMissing(e.target.checked);
                    if (file) {
                      setResult(null);
                      check.mutate({ f: file, missing: e.target.checked });
                    }
                  }}
                  label="Create missing departments and designations"
                  description="Otherwise names must match ones that already exist."
                />
              )}
              <button
                type="button"
                onClick={() => input.current?.click()}
                onDragOver={(e) => {
                  e.preventDefault();
                  setDragging(true);
                }}
                onDragLeave={() => setDragging(false)}
                onDrop={(e) => {
                  e.preventDefault();
                  setDragging(false);
                  choose(e.dataTransfer.files[0]);
                }}
                className={cn(
                  'flex w-full items-center justify-center gap-2 rounded-xl border border-dashed px-3 py-5 text-sm transition-colors',
                  dragging
                    ? 'border-accent-500 bg-accent-50 text-accent-700 dark:bg-accent-500/10'
                    : 'border-zinc-300 text-zinc-500 hover:border-accent-400 hover:text-accent-700 dark:border-zinc-700 dark:text-zinc-400',
                )}
              >
                <FileSpreadsheet className="size-5" aria-hidden="true" />
                {check.isPending ? 'Checking…' : file ? `${file.name} · choose another` : 'Choose the Excel file or drop it here'}
              </button>
              <input
                ref={input}
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                className="sr-only"
                tabIndex={-1}
                onChange={(e) => {
                  choose(e.target.files?.[0]);
                  e.target.value = '';
                }}
              />
            </div>
          </li>
        </ol>

        <FormError message={error} />

        {result && (
          <div className="space-y-3">
            <div className="grid grid-cols-3 gap-2 text-center">
              <Stat label="Rows" value={result.total} />
              <Stat label="Ready" value={result.valid} tone="green" />
              <Stat label="With problems" value={result.invalid} tone={result.invalid ? 'red' : undefined} />
            </div>
            {result.total === 0 && <p className="text-sm text-zinc-500">The sheet has no data rows.</p>}
            {ready && (
              <p className="flex items-center gap-2 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-200">
                <CheckCircle2 className="size-4 shrink-0" aria-hidden="true" />
                All {result.total} rows look right.{result.updated ? ` ${result.updated} will replace existing records.` : ''}
              </p>
            )}
            {problems.length > 0 && (
              <div className="max-h-64 overflow-y-auto rounded-xl border border-red-200 dark:border-red-500/30">
                <p className="sticky top-0 flex items-center gap-2 bg-red-50 px-3 py-2 text-sm font-medium text-red-800 dark:bg-red-950 dark:text-red-200">
                  <AlertCircle className="size-4" aria-hidden="true" /> Fix these rows in Excel and upload again
                </p>
                <ul className="divide-y divide-red-100 dark:divide-red-500/20">
                  {problems.map((r) => (
                    <li key={r.row} className="px-3 py-2 text-sm">
                      <p className="font-medium text-zinc-900 dark:text-zinc-100">
                        Row {r.row} <span className="font-normal text-zinc-500">· {r.label}</span>
                      </p>
                      <ul className="mt-0.5 list-disc pl-5 text-red-700 dark:text-red-300">
                        {r.errors.map((e) => (
                          <li key={e}>{e}</li>
                        ))}
                      </ul>
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {notes.length > 0 && problems.length === 0 && (
              <ul className="space-y-1 text-xs text-zinc-500">
                {Array.from(new Set(notes.flatMap((r) => r.notes))).slice(0, 8).map((n) => (
                  <li key={n} className="flex items-start gap-1.5">
                    <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden="true" />
                    {n}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: 'green' | 'red' }) {
  return (
    <div className="rounded-lg bg-zinc-50 p-2.5 dark:bg-zinc-800/50">
      <p
        className={cn(
          'text-xl font-semibold tabular-nums',
          tone === 'green' ? 'text-emerald-600' : tone === 'red' ? 'text-red-600' : 'text-zinc-900 dark:text-white',
        )}
      >
        {value}
      </p>
      <p className="text-xs text-zinc-500">{label}</p>
    </div>
  );
}
