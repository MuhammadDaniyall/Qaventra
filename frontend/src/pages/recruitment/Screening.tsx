import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { http } from '../../api/client';
import { Badge, Button, Card, CardHeader, Field, Input, useToast } from '../../components/ui';

interface Criteria {
  minExperienceYears: number | null;
  maxExpectedSalary: number | null;
  cities: string | null;
  keywords: string | null;
}
interface Score {
  applicationId: string;
  candidate: string;
  stage: string;
  score: number;
  reasons: string[];
}

/** Shortlisting criteria for a job and every applicant's score against them. */
export function ScreeningCard({ jobId }: { jobId: string }) {
  const toast = useToast();
  const qc = useQueryClient();
  const key = ['recruitment', 'screening', jobId];
  const criteria = useQuery({ queryKey: [...key, 'criteria'], queryFn: () => http.get<Criteria>(`/recruitment-extras/posts/${jobId}/criteria`) });
  const scores = useQuery({ queryKey: [...key, 'scores'], queryFn: () => http.get<Score[]>(`/recruitment-extras/posts/${jobId}/scores`) });
  const [f, setF] = useState<Criteria | null>(null);
  const save = useMutation({
    mutationFn: () => http.put(`/recruitment-extras/posts/${jobId}/criteria`, f),
    onSuccess: () => {
      setF(null);
      qc.invalidateQueries({ queryKey: key });
    },
    onError: (e) => toast.error(e),
  });
  if (!criteria.data) return null;
  const v = f ?? criteria.data;
  const num = (s: string) => (s === '' ? null : Number(s));
  return (
    <Card className="mt-6">
      <CardHeader
        title="Shortlisting score"
        description="Experience 30, expected salary 20, city 10, keywords 40. Criteria left empty count for everyone."
        actions={
          <Button size="sm" variant={f ? 'primary' : 'secondary'} loading={save.isPending} onClick={() => (f ? save.mutate() : setF(v))}>
            {f ? 'Save criteria' : 'Edit criteria'}
          </Button>
        }
      />
      {f && (
        <div className="grid gap-3 px-5 pb-4 sm:grid-cols-4">
          <Field label="Min. experience (years)">
            <Input type="number" min={0} value={v.minExperienceYears ?? ''} onChange={(e) => setF({ ...v, minExperienceYears: num(e.target.value) })} />
          </Field>
          <Field label="Max. expected salary">
            <Input type="number" min={0} value={v.maxExpectedSalary ?? ''} onChange={(e) => setF({ ...v, maxExpectedSalary: num(e.target.value) })} />
          </Field>
          <Field label="Cities">
            <Input value={v.cities ?? ''} placeholder="Lahore, Karachi" onChange={(e) => setF({ ...v, cities: e.target.value || null })} />
          </Field>
          <Field label="Keywords">
            <Input value={v.keywords ?? ''} placeholder="React, SQL" onChange={(e) => setF({ ...v, keywords: e.target.value || null })} />
          </Field>
        </div>
      )}
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {(scores.data ?? []).map((s) => (
          <li key={s.applicationId} className="flex items-center gap-3 px-5 py-2 text-sm">
            <Badge tone={s.score >= 70 ? 'green' : s.score >= 40 ? 'amber' : 'gray'}>{s.score}</Badge>
            <Link to={`/recruitment/applications/${s.applicationId}`} className="font-medium hover:underline">
              {s.candidate}
            </Link>
            <span className="flex-1 truncate text-xs text-zinc-500 dark:text-zinc-400">{s.reasons.join(' · ')}</span>
            <span className="text-xs text-zinc-500 dark:text-zinc-400">{s.stage}</span>
          </li>
        ))}
      </ul>
    </Card>
  );
}
