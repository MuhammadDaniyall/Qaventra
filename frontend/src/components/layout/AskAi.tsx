import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Mic, MicOff, Send, Sparkles, Volume2, VolumeX, X } from 'lucide-react';
import { aiApi, type AiAction, type AiTurn } from '../../api/ai';
import { errorMessage } from '../../api/client';
import { cn } from '../../lib/cn';
import { useT } from '../../lib/i18n';
import { Button, Drawer, IconButton, useToast } from '../ui';

// The browser's speech recognition (Chrome, Edge, Safari). Not in TypeScript's DOM types yet.
interface SpeechResult {
  0: { transcript: string };
  isFinal: boolean;
}
interface SpeechRecognitionLike {
  lang: string;
  interimResults: boolean;
  continuous: boolean;
  onresult: ((e: { resultIndex: number; results: ArrayLike<SpeechResult> }) => void) | null;
  onend: (() => void) | null;
  onerror: ((e: { error: string }) => void) | null;
  start(): void;
  stop(): void;
}
type SpeechCtor = new () => SpeechRecognitionLike;
const Recognition: SpeechCtor | undefined =
  typeof window === 'undefined'
    ? undefined
    : ((window as unknown as { SpeechRecognition?: SpeechCtor }).SpeechRecognition ??
      (window as unknown as { webkitSpeechRecognition?: SpeechCtor }).webkitSpeechRecognition);

type VoiceLang = 'ur-PK' | 'en-US';

interface Entry extends AiTurn {
  actions?: (AiAction & { state?: 'done' | 'dismissed' })[];
}

const SUGGESTIONS = ['How many leaves do I have left?', 'Meri is mahine ki attendance kaisi hai?', 'When is the next holiday?', 'Kal ki casual leave laga do'];

function speak(text: string, lang: VoiceLang) {
  if (!('speechSynthesis' in window)) return;
  window.speechSynthesis.cancel();
  const u = new SpeechSynthesisUtterance(text.replace(/[*_#`|>]/g, ''));
  u.lang = lang;
  const voice = window.speechSynthesis.getVoices().find((v) => v.lang.toLowerCase().startsWith(lang.slice(0, 2)));
  if (voice) u.voice = voice;
  window.speechSynthesis.speak(u);
}

/** Header button and side panel for the AI HR assistant; hidden when the site has no AI key. */
export function AskAi() {
  const { t, lang } = useT();
  const status = useQuery({ queryKey: ['ai-status'], queryFn: aiApi.status, staleTime: 5 * 60_000, retry: false });
  const [open, setOpen] = useState(false);
  if (!status.data?.enabled) return null;
  return (
    <>
      <IconButton label={t('Ask AI')} onClick={() => setOpen(true)}>
        <Sparkles className="size-[18px] text-accent-500" />
      </IconButton>
      {open && <AiPanel onClose={() => setOpen(false)} defaultVoiceLang={lang === 'ur' ? 'ur-PK' : 'en-US'} />}
    </>
  );
}

function AiPanel({ onClose, defaultVoiceLang }: { onClose: () => void; defaultVoiceLang: VoiceLang }) {
  const { t } = useT();
  const toast = useToast();
  const qc = useQueryClient();
  const [entries, setEntries] = useState<Entry[]>([]);
  const [text, setText] = useState('');
  const [listening, setListening] = useState(false);
  const [voiceLang, setVoiceLang] = useState<VoiceLang>(defaultVoiceLang);
  const [readAloud, setReadAloud] = useState(false);
  const recognition = useRef<SpeechRecognitionLike | null>(null);
  const bottom = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottom.current?.scrollIntoView({ behavior: 'smooth' });
  }, [entries]);
  useEffect(
    () => () => {
      recognition.current?.stop();
      if ('speechSynthesis' in window) window.speechSynthesis.cancel();
    },
    [],
  );

  const ask = useMutation({
    mutationFn: ({ history, voice }: { history: Entry[]; voice: boolean }) =>
      aiApi.ask(
        history.map(({ role, text }) => ({ role, text })),
        voice,
      ),
    onSuccess: (r, { voice }) => {
      setEntries((e) => [...e, { role: 'assistant', text: r.reply, actions: r.actions }]);
      if (voice || readAloud) speak(r.reply, voiceLang);
      qc.setQueryData(['ai-status'], (s: { enabled: boolean; limit: number; used: number } | undefined) => (s ? { ...s, used: s.limit - r.remaining } : s));
    },
    onError: (err) => setEntries((e) => [...e, { role: 'assistant', text: errorMessage(err) }]),
  });

  const send = (question: string, voice = false) => {
    const q = question.trim();
    if (!q || ask.isPending) return;
    const history = [...entries, { role: 'user' as const, text: q }];
    setEntries(history);
    setText('');
    ask.mutate({ history, voice });
  };

  const listen = () => {
    if (!Recognition) return;
    if (listening) {
      recognition.current?.stop();
      return;
    }
    const r = new Recognition();
    r.lang = voiceLang;
    r.interimResults = true;
    r.continuous = false;
    let heard = '';
    r.onresult = (e) => {
      heard = Array.from(e.results)
        .map((x) => x[0].transcript)
        .join(' ');
      setText(heard);
    };
    r.onerror = (e) => {
      if (e.error === 'not-allowed') toast.error(t('Allow the microphone in your browser to speak to the assistant.'));
    };
    r.onend = () => {
      setListening(false);
      recognition.current = null;
      if (heard.trim()) send(heard, true);
    };
    recognition.current = r;
    setListening(true);
    r.start();
  };

  const confirm = useMutation({
    mutationFn: ({ action }: { entry: number; index: number; action: AiAction }) => aiApi.confirm(action),
    onSuccess: (_, { entry, index }) => {
      setEntries((list) =>
        list.map((e, i) => (i === entry ? { ...e, actions: e.actions?.map((a, j) => (j === index ? { ...a, state: 'done' as const } : a)) } : e)),
      );
      toast.success(t('Submitted'));
      qc.invalidateQueries();
    },
    onError: (err) => toast.error(err),
  });
  const dismiss = (entry: number, index: number) =>
    setEntries((list) =>
      list.map((e, i) => (i === entry ? { ...e, actions: e.actions?.map((a, j) => (j === index ? { ...a, state: 'dismissed' as const } : a)) } : e)),
    );

  return (
    <Drawer
      open
      onClose={onClose}
      title={t('Ask AI')}
      description={t('Ask about your leave, attendance, pay or company policies, or ask it to prepare a request. Type or speak in Urdu or English.')}
      footer={
        <form
          className="flex w-full items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            send(text);
          }}
        >
          {Recognition && (
            <div className="flex flex-col items-center gap-1">
              <IconButton
                label={listening ? t('Stop listening') : t('Speak')}
                onClick={listen}
                className={cn(listening && 'animate-pulse bg-red-50 text-red-600 dark:bg-red-500/10')}
              >
                {listening ? <MicOff className="size-5" /> : <Mic className="size-5" />}
              </IconButton>
              <button
                type="button"
                className="text-[11px] font-medium text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200"
                onClick={() => setVoiceLang((l) => (l === 'ur-PK' ? 'en-US' : 'ur-PK'))}
                aria-label={t('Speech language')}
              >
                {voiceLang === 'ur-PK' ? 'اردو' : 'EN'}
              </button>
            </div>
          )}
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                send(text);
              }
            }}
            rows={2}
            maxLength={4000}
            dir="auto"
            placeholder={listening ? t('Listening…') : t('Type your question')}
            aria-label={t('Your question')}
            className="min-h-11 flex-1 resize-none rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm focus:border-accent-500 focus:outline-none dark:border-zinc-700 dark:bg-zinc-900"
          />
          <IconButton label={readAloud ? t('Stop reading answers aloud') : t('Read answers aloud')} onClick={() => setReadAloud((v) => !v)}>
            {readAloud ? <Volume2 className="size-5" /> : <VolumeX className="size-5" />}
          </IconButton>
          <Button type="submit" icon={<Send className="size-4" />} loading={ask.isPending} disabled={!text.trim()}>
            {t('Ask')}
          </Button>
        </form>
      }
    >
      <div className="space-y-4">
        {entries.length === 0 && (
          <div className="space-y-2">
            <p className="text-sm text-zinc-500">{t('Try asking:')}</p>
            <div className="flex flex-wrap gap-2">
              {SUGGESTIONS.map((s) => (
                <button
                  key={s}
                  type="button"
                  dir="auto"
                  onClick={() => send(s)}
                  className="rounded-full border border-zinc-200 px-3 py-1.5 text-sm text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  {s}
                </button>
              ))}
            </div>
          </div>
        )}
        {entries.map((e, i) => (
          <div key={i} className={cn('flex', e.role === 'user' ? 'justify-end' : 'justify-start')}>
            <div className={cn('max-w-[88%] space-y-2', e.role === 'user' && 'items-end')}>
              <div
                dir="auto"
                className={cn(
                  'rounded-2xl px-3.5 py-2.5 text-sm leading-6 whitespace-pre-wrap',
                  e.role === 'user' ? 'bg-accent-600 text-white' : 'bg-zinc-100 text-zinc-800 dark:bg-zinc-800 dark:text-zinc-100',
                )}
              >
                {e.text}
              </div>
              {e.actions?.map((a, j) => (
                <div key={j} className="rounded-xl border border-accent-200 bg-accent-50/60 p-3 dark:border-accent-500/30 dark:bg-accent-500/10">
                  <p dir="auto" className="text-sm font-medium text-zinc-800 dark:text-zinc-100">
                    {a.summary}
                  </p>
                  {a.state === 'done' ? (
                    <p className="mt-2 flex items-center gap-1 text-sm text-emerald-600">
                      <Check className="size-4" /> {t('Submitted')}
                    </p>
                  ) : a.state === 'dismissed' ? (
                    <p className="mt-2 text-sm text-zinc-500">{t('Cancelled')}</p>
                  ) : (
                    <div className="mt-2 flex gap-2">
                      <Button
                        size="sm"
                        icon={<Check className="size-4" />}
                        loading={confirm.isPending}
                        onClick={() => confirm.mutate({ entry: i, index: j, action: a })}
                      >
                        {t('Confirm')}
                      </Button>
                      <Button size="sm" variant="ghost" icon={<X className="size-4" />} onClick={() => dismiss(i, j)}>
                        {t('Cancel')}
                      </Button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        ))}
        {ask.isPending && (
          <div className="flex justify-start">
            <div className="rounded-2xl bg-zinc-100 px-3.5 py-2.5 text-sm text-zinc-500 dark:bg-zinc-800">{t('Thinking…')}</div>
          </div>
        )}
        <div ref={bottom} />
      </div>
    </Drawer>
  );
}
