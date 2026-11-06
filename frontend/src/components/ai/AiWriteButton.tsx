import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Sparkles, Wand2 } from 'lucide-react';
import { aiApi, type AiWriteKind } from '../../api/ai';
import { errorMessage } from '../../api/client';
import { Button, Field, FormError, Modal, Select, Textarea } from '../ui';
import { tx, useT } from '../../lib/i18n';

const HINTS: Record<AiWriteKind, string> = {
  announcement: 'e.g. Office closed on Friday for Eid, reopens Monday 9 am',
  job_ad: 'e.g. Senior accountant in Lahore, 5 years, handles month-end close and tax',
  requirements: 'e.g. Senior accountant: ACCA or CA, 5 years, QuickBooks, FBR filing',
  letter: 'e.g. Experience letter for an employee leaving after 3 years in sales',
  policy: 'e.g. Work from home policy: 2 days a week, manager approval, core hours 11-4',
  review: 'e.g. Strong on deadlines, helps teammates, needs to speak up more in meetings',
  email: 'e.g. Remind everyone to submit expense claims by the 25th',
  improve: 'What should change? e.g. make it shorter and more formal',
};

const TONES = ['Professional and friendly', 'Formal', 'Warm and celebratory', 'Short and direct', 'Firm but polite'];

/**
 * "Write with AI": a small sparkle button that opens a brief box, asks the free models for a draft, and hands the text
 * back to the form. When the field already has text it can also improve it. The person edits and saves as usual.
 */
export function AiWriteButton({
  kind,
  draft,
  context,
  onResult,
  label = 'Write with AI',
}: {
  kind: AiWriteKind;
  /** What is in the field now; sent so the AI can improve or continue it. */
  draft?: string;
  /** Extra facts the form already knows (job title, location...), added to the brief. */
  context?: string;
  onResult: (text: string, title: string | null) => void;
  label?: string;
}) {
  const { lang } = useT();
  const [open, setOpen] = useState(false);
  const [brief, setBrief] = useState('');
  const [tone, setTone] = useState(TONES[0]);
  const [language, setLanguage] = useState<string>(lang === 'ur' ? 'ur' : 'en');
  const hasDraft = !!draft?.trim();

  const write = useMutation({
    mutationFn: (improve: boolean) =>
      aiApi.write(improve ? 'improve' : kind, [context, brief].filter(Boolean).join('\n'), draft ?? '', tone, language),
    onSuccess: (r) => {
      onResult(r.text, r.title);
      setOpen(false);
      setBrief('');
    },
  });

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-indigo-50 to-fuchsia-50 px-2.5 py-0.5 text-xs font-medium text-indigo-700 ring-1 ring-indigo-200/70 transition hover:from-indigo-100 hover:to-fuchsia-100 dark:from-indigo-500/10 dark:to-fuchsia-500/10 dark:text-indigo-200 dark:ring-indigo-400/20"
      >
        <Sparkles className="size-3.5" /> {tx(label)}
      </button>
      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Write with AI"
        description="Say in a line or two what you need. You can edit the draft before saving."
        footer={
          <>
            <Button variant="ghost" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            {hasDraft && (
              <Button variant="secondary" icon={<Wand2 className="size-4" />} loading={write.isPending && write.variables} onClick={() => write.mutate(true)}>
                Improve my text
              </Button>
            )}
            <Button
              icon={<Sparkles className="size-4" />}
              loading={write.isPending && !write.variables}
              disabled={!brief.trim() && !context}
              onClick={() => write.mutate(false)}
            >
              {hasDraft ? 'Write new draft' : 'Write draft'}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          <FormError message={write.error ? errorMessage(write.error) : null} />
          <Field label="What do you need?">
            <Textarea rows={3} maxLength={2000} autoFocus value={brief} onChange={(e) => setBrief(e.target.value)} placeholder={tx(HINTS[kind])} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Tone">
              <Select value={tone} onChange={(e) => setTone(e.target.value)}>
                {TONES.map((t) => (
                  <option key={t} value={t}>
                    {tx(t)}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Language">
              <Select value={language} onChange={(e) => setLanguage(e.target.value)}>
                <option value="en">English</option>
                <option value="ur">اردو</option>
              </Select>
            </Field>
          </div>
          {hasDraft && <p className="text-xs text-zinc-500 dark:text-zinc-400">{tx('“Improve my text” keeps your meaning and polishes the wording.')}</p>}
        </div>
      </Modal>
    </>
  );
}
