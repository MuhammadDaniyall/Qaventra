import { useId, useState } from 'react';
import { useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query';
import { Plus } from 'lucide-react';
import { errorMessage } from '../../api/client';
import { Button, Input, Select, useToast } from '../../components/ui';
import { cn } from '../../lib/cn';

interface Option {
  id: string;
  label: string;
}

interface QuickCreateSelectProps {
  label: string;
  /** e.g. "designation" — used in "+ New designation". */
  noun: string;
  value: string | null;
  options: Option[];
  onChange: (id: string | null) => void;
  create: (name: string) => Promise<{ id: string }>;
  invalidate: QueryKey;
  placeholder: string;
  hint?: string;
  error?: string;
  disabled?: boolean;
  canCreate?: boolean;
  className?: string;
}

/** A picker with an inline "+ New …" quick-create, so HR can add one without leaving the form. */
export function QuickCreateSelect({
  label,
  noun,
  value,
  options,
  onChange,
  create,
  invalidate,
  placeholder,
  hint,
  error,
  disabled,
  canCreate = true,
  className,
}: QuickCreateSelectProps) {
  const id = useId();
  const toast = useToast();
  const qc = useQueryClient();
  const [adding, setAdding] = useState(false);
  const [name, setName] = useState('');
  const [createError, setCreateError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (n: string) => create(n),
    onSuccess: async (created) => {
      await qc.invalidateQueries({ queryKey: invalidate });
      onChange(created.id);
      toast.success(`${noun.charAt(0).toUpperCase()}${noun.slice(1)} "${name.trim()}" created`);
      setName('');
      setAdding(false);
    },
    onError: (err) => setCreateError(errorMessage(err)),
  });

  const submit = () => {
    setCreateError(null);
    if (!name.trim()) return setCreateError(`Enter a ${noun} name.`);
    mutation.mutate(name.trim());
  };

  const desc = error || createError || hint ? `${id}-desc` : undefined;

  return (
    <div className={cn('space-y-1.5', className)}>
      <div className="flex items-center justify-between gap-2">
        <label htmlFor={id} className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
          {label}
        </label>
        {canCreate && !disabled && !adding && (
          <button
            type="button"
            onClick={() => setAdding(true)}
            className="inline-flex items-center gap-1 rounded text-xs font-medium text-accent-600 hover:underline focus-visible:outline-2 focus-visible:outline-accent-500 dark:text-accent-400"
          >
            <Plus className="size-3.5" /> New {noun}
          </button>
        )}
      </div>
      {adding ? (
        <div className="flex gap-2">
          <Input
            id={id}
            autoFocus
            value={name}
            maxLength={150}
            placeholder={`New ${noun} name`}
            aria-describedby={desc}
            aria-invalid={createError ? true : undefined}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                submit();
              } else if (e.key === 'Escape') {
                e.preventDefault();
                setAdding(false);
              }
            }}
          />
          <Button size="md" loading={mutation.isPending} onClick={submit}>
            Add
          </Button>
          <Button size="md" variant="ghost" onClick={() => (setAdding(false), setCreateError(null))}>
            Cancel
          </Button>
        </div>
      ) : (
        <Select
          id={id}
          value={value ?? ''}
          disabled={disabled}
          aria-describedby={desc}
          aria-invalid={error ? true : undefined}
          onChange={(e) => onChange(e.target.value || null)}
        >
          <option value="">{placeholder}</option>
          {options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.label}
            </option>
          ))}
        </Select>
      )}
      {(error || createError || hint) && (
        <p
          id={desc}
          className={cn('text-xs', error || createError ? 'text-red-600 dark:text-red-400' : 'text-zinc-500 dark:text-zinc-400')}
        >
          {error || createError || hint}
        </p>
      )}
    </div>
  );
}
