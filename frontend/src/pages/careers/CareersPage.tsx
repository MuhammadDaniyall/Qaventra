import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Briefcase, Building2, CheckCircle2, MapPin } from 'lucide-react';
import { ApiError, errorMessage } from '../../api/client';
import { EMPLOYMENT_LABELS, EMPTY_CANDIDATE, recruitmentApi, type CandidateFormValues, type CareersJob } from '../../api/recruitment';
import { Button, EmptyState, FormError, PageSpinner } from '../../components/ui';
import { formatCurrency, formatDate } from '../../lib/format';
import { CandidateFields, cvProblem } from '../recruitment/shared';

/** The public careers page: a company's open jobs, each with an application form. No sign-in. */
export function CareersPage() {
  const { company = '', job } = useParams();
  const page = useQuery({
    queryKey: ['careers', company],
    queryFn: () => recruitmentApi.careers(company),
    retry: false,
  });
  useEffect(() => {
    if (page.data) document.title = `Careers at ${page.data.company}`;
  }, [page.data]);

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950">
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-4xl items-center gap-3 px-4 py-5 sm:px-6">
          <span className="flex size-10 items-center justify-center rounded-xl bg-accent-600 text-white">
            <Building2 className="size-5" />
          </span>
          <div>
            <p className="text-lg font-semibold text-zinc-900 dark:text-white">{page.data?.company ?? 'Careers'}</p>
            <p className="text-sm text-zinc-500">Careers{page.data?.city ? ` · ${page.data.city}` : ''}</p>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
        {page.isLoading ? (
          <PageSpinner />
        ) : page.isError ? (
          <EmptyState
            icon={Building2}
            title={page.error instanceof ApiError && page.error.status === 404 ? 'This careers page does not exist' : 'Could not load the careers page'}
            description={page.error instanceof ApiError && page.error.status === 404 ? 'Check the link you were given.' : errorMessage(page.error)}
          />
        ) : job ? (
          <JobDetail company={company} job={page.data!.jobs.find((j) => j.slug === job)} companyName={page.data!.company} />
        ) : (
          <JobList company={company} jobs={page.data!.jobs} />
        )}
      </main>
      <footer className="pb-8 text-center text-xs text-zinc-400">Powered by Qaventra HRMS</footer>
    </div>
  );
}

function JobList({ company, jobs }: { company: string; jobs: CareersJob[] }) {
  if (!jobs.length) return <EmptyState icon={Briefcase} title="No open positions right now" description="Please check back later." />;
  return (
    <>
      <h1 className="mb-1 text-2xl font-semibold text-zinc-900 dark:text-white">Open positions</h1>
      <p className="mb-6 text-sm text-zinc-500">
        {jobs.length} job{jobs.length === 1 ? '' : 's'} open. Pick one to read more and apply.
      </p>
      <ul className="space-y-3">
        {jobs.map((j) => (
          <li key={j.slug}>
            <Link
              to={`/careers/${company}/${j.slug}`}
              className="block rounded-xl border border-zinc-200 bg-white p-5 transition hover:border-accent-400 hover:shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
            >
              <p className="font-semibold text-zinc-900 dark:text-white">{j.title}</p>
              <Meta j={j} />
              <p className="mt-2 line-clamp-2 text-sm text-zinc-600 dark:text-zinc-300">{j.description}</p>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}

function Meta({ j }: { j: CareersJob }) {
  return (
    <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-zinc-500">
      {j.department && <span>{j.department}</span>}
      {j.location && (
        <span className="inline-flex items-center gap-1">
          <MapPin className="size-3.5" />
          {j.location}
        </span>
      )}
      <span>{EMPLOYMENT_LABELS[j.employmentType]}</span>
      {(j.salaryMin || j.salaryMax) && (
        <span>
          {j.salaryMin && j.salaryMax ? `${formatCurrency(j.salaryMin)} – ${formatCurrency(j.salaryMax)}` : formatCurrency(j.salaryMin ?? j.salaryMax)} a month
        </span>
      )}
      {j.closesOn && <span>Apply by {formatDate(j.closesOn)}</span>}
    </p>
  );
}

function JobDetail({ company, companyName, job }: { company: string; companyName: string; job: CareersJob | undefined }) {
  const [v, setV] = useState<CandidateFormValues>(EMPTY_CANDIDATE);
  const [problem, setProblem] = useState<string | null>(null);
  const apply = useMutation({
    mutationFn: () => recruitmentApi.apply(company, job!.slug, v),
  });
  const back = (
    <Link to={`/careers/${company}`} className="mb-4 inline-flex items-center gap-1 text-sm text-zinc-500 hover:text-zinc-900 dark:hover:text-white">
      <ArrowLeft className="size-4" /> All jobs
    </Link>
  );
  if (!job)
    return (
      <>
        {back}
        <EmptyState icon={Briefcase} title="This job is no longer open" description="It may have been filled or closed." />
      </>
    );
  if (apply.isSuccess)
    return (
      <div className="rounded-2xl border border-zinc-200 bg-white px-6 py-12 text-center dark:border-zinc-800 dark:bg-zinc-900">
        <CheckCircle2 className="mx-auto mb-3 size-10 text-emerald-500" />
        <h1 className="text-xl font-semibold text-zinc-900 dark:text-white">Application sent</h1>
        <p className="mx-auto mt-2 max-w-md text-sm text-zinc-500">
          Thank you for applying for {job.title} at {companyName}. We have emailed you a confirmation and will be in touch about the next steps.
        </p>
        <Link to={`/careers/${company}`} className="mt-6 inline-block text-sm font-medium text-accent-600 hover:underline">
          See other jobs
        </Link>
      </div>
    );
  const submit = (e: FormEvent) => {
    e.preventDefault();
    const p = cvProblem(v, true);
    setProblem(p);
    if (!p) apply.mutate();
  };
  return (
    <>
      {back}
      <article className="rounded-2xl border border-zinc-200 bg-white p-6 dark:border-zinc-800 dark:bg-zinc-900">
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-white">{job.title}</h1>
        <Meta j={job} />
        <div className="mt-5 text-sm leading-6 whitespace-pre-line text-zinc-700 dark:text-zinc-300">{job.description}</div>
        {job.requirements && (
          <>
            <h2 className="mt-5 mb-2 font-semibold text-zinc-900 dark:text-white">What we are looking for</h2>
            <ul className="list-disc space-y-1 pl-5 text-sm text-zinc-700 dark:text-zinc-300">
              {job.requirements
                .split('\n')
                .filter((l) => l.trim())
                .map((l, i) => (
                  <li key={i}>{l.replace(/^[-•*]\s*/, '')}</li>
                ))}
            </ul>
          </>
        )}
      </article>
      <form onSubmit={submit} className="mt-6 rounded-2xl border border-zinc-200 bg-white p-6 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="mb-4 text-lg font-semibold text-zinc-900 dark:text-white">Apply for this job</h2>
        <CandidateFields value={v} onChange={setV} cvRequired />
        <div className="mt-4">
          <FormError message={problem ?? (apply.isError ? errorMessage(apply.error) : null)} />
        </div>
        <p className="mt-4 text-xs text-zinc-500">By applying you agree that {companyName} may keep your details and CV to consider you for jobs.</p>
        <Button type="submit" className="mt-4" loading={apply.isPending}>
          Send application
        </Button>
      </form>
    </>
  );
}
