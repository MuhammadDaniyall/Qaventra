import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { BookOpenCheck, Send } from 'lucide-react';
import { aiApi } from '../../api/ai';
import { errorMessage } from '../../api/client';
import { Badge, Button, Card, FormError, Input } from '../ui';
import { useT } from '../../lib/i18n';

/** "Ask about policies": answers only from the company's own policies, leave settings and holidays, and shows the source. */
export function PolicyAsk() {
  const { t, lang } = useT();
  const [q, setQ] = useState('');
  const ask = useMutation({ mutationFn: (question: string) => aiApi.policy(question, lang) });
  const submit = () => {
    const question = q.trim();
    if (question.length >= 3) ask.mutate(question);
  };
  const a = ask.data;
  return (
    <Card className="mb-5 p-5">
      <div className="mb-3 flex items-center gap-2">
        <BookOpenCheck className="size-5 text-accent-600" />
        <h2 className="font-semibold">{t('Ask about policies')}</h2>
      </div>
      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
      >
        <Input
          value={q}
          maxLength={500}
          onChange={(e) => setQ(e.target.value)}
          placeholder={t('e.g. How many days of maternity leave do we get?')}
          aria-label={t('Ask about policies')}
        />
        <Button type="submit" loading={ask.isPending} disabled={q.trim().length < 3} icon={<Send className="size-4" />}>
          {t('Ask')}
        </Button>
      </form>
      {ask.isError && (
        <div className="mt-3">
          <FormError message={errorMessage(ask.error)} />
        </div>
      )}
      {a && (
        <div className="mt-4 rounded-lg bg-zinc-50 p-4 text-sm dark:bg-zinc-800/60">
          <p className="whitespace-pre-line">{a.answer}</p>
          {a.sources.length > 0 && (
            <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-zinc-500">
              <span>{t('Source')}:</span>
              {a.sources.map((s) => (
                <Badge key={s.title} tone="accent">
                  {s.title}
                </Badge>
              ))}
            </div>
          )}
          <p className="mt-2 text-xs text-zinc-500">
            {a.usedAi
              ? t('Answered by AI from company policy only. HR has the final word.')
              : t('Matching lines from company policy. HR has the final word.')}
          </p>
        </div>
      )}
    </Card>
  );
}
