import { useRef, type RefObject } from 'react';
import { useQuery } from '@tanstack/react-query';
import { lettersApi } from '../../api/letters';
import { qk } from '../../api/keys';
import { Textarea } from '../../components/ui';
import type { LetterCategory } from '../../types/workflows';

export const LETTER_CATEGORIES: LetterCategory[] = [
  'Offer',
  'Appointment',
  'Confirmation',
  'Increment',
  'SalaryCertificate',
  'Experience',
  'Relieving',
  'Warning',
  'Other',
];

const CATEGORY_LABELS: Record<LetterCategory, string> = {
  Offer: 'Offer letter',
  Appointment: 'Appointment',
  Confirmation: 'Confirmation',
  Increment: 'Increment',
  SalaryCertificate: 'Salary certificate',
  Experience: 'Experience certificate',
  Relieving: 'Relieving letter',
  Warning: 'Warning',
  Other: 'Other',
};

export const categoryLabel = (c: LetterCategory) => CATEGORY_LABELS[c];

export function usePlaceholders() {
  return useQuery({ queryKey: qk.letterPlaceholders, queryFn: lettersApi.placeholders, staleTime: Infinity });
}

/** Inserts "{{Key}}" at the caret of a textarea and keeps focus there. */
export function insertAtCaret(el: HTMLTextAreaElement | null, value: string, text: string, onChange: (v: string) => void) {
  const token = `{{${text}}}`;
  if (!el) return onChange(value + token);
  const start = el.selectionStart ?? value.length;
  const end = el.selectionEnd ?? value.length;
  onChange(value.slice(0, start) + token + value.slice(end));
  requestAnimationFrame(() => {
    el.focus();
    el.setSelectionRange(start + token.length, start + token.length);
  });
}

/** Letter body editor with one-click placeholder chips. */
export function BodyEditor({
  value,
  onChange,
  rows = 16,
  textareaRef,
}: {
  value: string;
  onChange: (v: string) => void;
  rows?: number;
  textareaRef?: RefObject<HTMLTextAreaElement | null>;
}) {
  const own = useRef<HTMLTextAreaElement>(null);
  const ref = textareaRef ?? own;
  const placeholders = usePlaceholders();
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-1.5" aria-label="Insert a field">
        {(placeholders.data ?? []).map((p) => (
          <button
            key={p.key}
            type="button"
            title={`${p.label}, e.g. ${p.example}`}
            onClick={() => insertAtCaret(ref.current, value, p.key, onChange)}
            className="rounded-md border border-zinc-200 bg-white px-2 py-0.5 font-mono text-[11px] text-zinc-600 hover:border-accent-400 hover:text-accent-700 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300"
          >
            {p.key}
          </button>
        ))}
      </div>
      <Textarea
        ref={ref}
        value={value}
        rows={rows}
        maxLength={20000}
        onChange={(e) => onChange(e.target.value)}
        className="font-serif text-[15px] leading-relaxed"
      />
    </div>
  );
}

/** Highlights {{placeholders}} in a template snippet. */
export function Highlighted({ text }: { text: string }) {
  const parts = text.split(/(\{\{\s*[A-Za-z]+\s*\}\})/g);
  return (
    <>
      {parts.map((p, i) =>
        /^\{\{/.test(p) ? (
          <mark key={i} className="rounded bg-accent-50 px-0.5 text-accent-700 dark:bg-accent-500/15 dark:text-accent-200">
            {p}
          </mark>
        ) : (
          <span key={i}>{p}</span>
        ),
      )}
    </>
  );
}
