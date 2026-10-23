import { http } from './client';

export type RequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled';
export type EmploymentType = 'FullTime' | 'PartTime' | 'Contract' | 'Internship';
export type JobPostStatus = 'Draft' | 'Open' | 'Closed' | 'Filled';
export type CandidateSource = 'CareersPage' | 'Referral' | 'WalkIn' | 'Advertisement' | 'Agency' | 'Manual';
export type ApplicationStage = 'Applied' | 'Shortlisted' | 'Test' | 'Interview' | 'Offer' | 'Hired' | 'Rejected' | 'Withdrawn' | 'OnHold';
export type OfferStatus = 'Draft' | 'Sent' | 'Accepted' | 'Declined';
export type InterviewMode = 'Onsite' | 'Online' | 'Phone';
export type InterviewStatus = 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow';
export type HireRecommendation = 'StrongNo' | 'No' | 'Yes' | 'StrongYes';

/** The board's columns, in order. Closed stages sit apart. */
export const PIPELINE: ApplicationStage[] = ['Applied', 'Shortlisted', 'Test', 'Interview', 'Offer', 'Hired'];
export const CLOSED_STAGES: ApplicationStage[] = ['Rejected', 'Withdrawn', 'OnHold'];

export const STAGE_LABELS: Record<ApplicationStage, string> = {
  Applied: 'Applied',
  Shortlisted: 'Shortlisted',
  Test: 'Test',
  Interview: 'Interview',
  Offer: 'Offer',
  Hired: 'Hired',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrawn',
  OnHold: 'On hold',
};

export const EMPLOYMENT_LABELS: Record<EmploymentType, string> = {
  FullTime: 'Full-time',
  PartTime: 'Part-time',
  Contract: 'Contract',
  Internship: 'Internship',
};

export const SOURCE_LABELS: Record<CandidateSource, string> = {
  CareersPage: 'Careers page',
  Referral: 'Referral',
  WalkIn: 'Walk-in',
  Advertisement: 'Advertisement',
  Agency: 'Agency',
  Manual: 'Added by hand',
};

export const MODE_LABELS: Record<InterviewMode, string> = {
  Onsite: 'In person',
  Online: 'Online',
  Phone: 'Phone',
};

export const RECOMMENDATION_LABELS: Record<HireRecommendation, string> = {
  StrongNo: 'Strong no',
  No: 'No',
  Yes: 'Yes',
  StrongYes: 'Strong yes',
};

export interface RecruitmentMe {
  isHr: boolean;
  runsJobs: boolean;
  canRequest: boolean;
  myInterviews: number;
  careersSlug: string | null;
}

export interface SaveJobRequest {
  title: string;
  departmentId: string | null;
  designationId: string | null;
  positions: number;
  replacesEmployeeId: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  experienceMinYears: number | null;
  qualification: string | null;
  skills: string | null;
  neededBy: string | null;
  reason: string;
}

export interface JobRequest extends SaveJobRequest {
  id: string;
  department: string | null;
  designation: string | null;
  replaces: string | null;
  status: RequestStatus;
  requestedBy: string;
  requestedByEmployeeId: string;
  createdAtUtc: string;
  decisionNote: string | null;
  jobPostId: string | null;
}

export interface SaveJobPost {
  title: string;
  jobRequestId: string | null;
  departmentId: string | null;
  designationId: string | null;
  location: string | null;
  employmentType: EmploymentType;
  description: string;
  requirements: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  showSalary: boolean;
  positions: number;
  closesOn: string | null;
  recruiterEmployeeId: string | null;
  hiringManagerEmployeeId: string | null;
}

export interface JobPost extends SaveJobPost {
  id: string;
  slug: string;
  department: string | null;
  designation: string | null;
  status: JobPostStatus;
  recruiter: string | null;
  hiringManager: string | null;
  publishedAtUtc: string | null;
  createdAtUtc: string;
  byStage: Partial<Record<ApplicationStage, number>>;
  total: number;
  canEdit: boolean;
}

export interface PipelineCard {
  id: string;
  candidateId: string;
  name: string;
  email: string;
  currentTitle: string | null;
  experienceYears: number | null;
  source: CandidateSource;
  stage: ApplicationStage;
  rating: number | null;
  isBlacklisted: boolean;
  interviews: number;
  averageFeedback: number | null;
  offerStatus: OfferStatus | null;
  appliedAtUtc: string;
  stageChangedAtUtc: string | null;
}

export interface Opening {
  id: string;
  title: string;
  department: string | null;
  location: string | null;
  employmentType: EmploymentType;
  description: string;
  requirements: string | null;
  closesOn: string | null;
  slug: string;
}

/** Sent as a form so a CV can go with it. */
export interface CandidateFormValues {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  cnic?: string;
  city: string;
  currentTitle: string;
  currentCompany: string;
  experienceYears: string;
  expectedSalary: string;
  noticeDays: string;
  linkedInUrl: string;
  skills: string;
  coverLetter: string;
  source?: CandidateSource;
  cv: File | null;
}

export interface CandidateListItem {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  city: string | null;
  currentTitle: string | null;
  experienceYears: number | null;
  source: CandidateSource;
  isBlacklisted: boolean;
  applications: number;
  latestJob: string | null;
  latestStage: ApplicationStage | null;
  createdAtUtc: string;
  hired: boolean;
}

export interface CandidateApplication {
  id: string;
  jobPostId: string;
  job: string;
  stage: ApplicationStage;
  appliedAtUtc: string;
}

export interface Candidate {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  cnic: string | null;
  city: string | null;
  currentTitle: string | null;
  currentCompany: string | null;
  experienceYears: number | null;
  expectedSalary: number | null;
  noticeDays: number | null;
  linkedInUrl: string | null;
  skills: string | null;
  source: CandidateSource;
  referredBy: string | null;
  isBlacklisted: boolean;
  blacklistReason: string | null;
  employeeId: string | null;
  createdAtUtc: string;
  applications: CandidateApplication[];
  possibleDuplicates: CandidateListItem[];
}

export interface ApplicationEvent {
  id: string;
  text: string;
  byName: string | null;
  isNote: boolean;
  createdAtUtc: string;
}

export interface Feedback {
  interviewerEmployeeId: string;
  interviewer: string;
  rating: number;
  recommendation: HireRecommendation;
  strengths: string | null;
  concerns: string | null;
  createdAtUtc: string;
}

export interface Interview {
  id: string;
  applicationId: string;
  round: number;
  title: string;
  startsAtUtc: string;
  durationMinutes: number;
  mode: InterviewMode;
  where: string | null;
  interviewerIds: string[];
  interviewers: string[];
  status: InterviewStatus;
  notes: string | null;
  feedback: Feedback[];
  feedbackHidden: number;
  iAmInterviewer: boolean;
  iGaveFeedback: boolean;
}

export interface Application {
  id: string;
  candidate: Candidate;
  jobPostId: string;
  job: string;
  hiringManagerEmployeeId: string | null;
  stage: ApplicationStage;
  coverLetter: string | null;
  rating: number | null;
  rejectionReason: string | null;
  offeredSalary: number | null;
  offeredJoiningDate: string | null;
  offerNote: string | null;
  offerStatus: OfferStatus | null;
  appliedAtUtc: string;
  events: ApplicationEvent[];
  interviews: Interview[];
  canRun: boolean;
  canOffer: boolean;
  canHire: boolean;
}

export interface MyInterview {
  id: string;
  applicationId: string;
  candidate: string;
  job: string;
  title: string;
  round: number;
  startsAtUtc: string;
  durationMinutes: number;
  mode: InterviewMode;
  where: string | null;
  status: InterviewStatus;
  feedbackGiven: boolean;
}

export interface ScheduleInterview {
  title: string;
  startsAtUtc: string;
  durationMinutes: number;
  mode: InterviewMode;
  where: string | null;
  interviewerIds: string[];
  notes: string | null;
  emailCandidate: boolean;
}

export interface SaveCandidate {
  firstName: string;
  lastName: string | null;
  email: string;
  phone: string | null;
  cnic: string | null;
  city: string | null;
  currentTitle: string | null;
  currentCompany: string | null;
  experienceYears: number | null;
  expectedSalary: number | null;
  noticeDays: number | null;
  linkedInUrl: string | null;
  skills: string | null;
  source: CandidateSource;
  referredBy: string | null;
}

export interface FunnelRow {
  jobPostId: string;
  job: string;
  status: JobPostStatus;
  positions: number;
  byStage: Partial<Record<ApplicationStage, number>>;
  total: number;
  hired: number;
}

export interface RecruitmentReport {
  jobs: FunnelRow[];
  bySource: Partial<Record<CandidateSource, number>>;
  hiredBySource: Partial<Record<CandidateSource, number>>;
  averageDaysToHire: number | null;
  offersMade: number;
  offersAccepted: number;
  offersDeclined: number;
  openJobs: number;
  openPositions: number;
  activeCandidates: number;
  interviewsThisWeek: number;
}

export interface CareersJob {
  slug: string;
  title: string;
  department: string | null;
  location: string | null;
  employmentType: EmploymentType;
  description: string;
  requirements: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  closesOn: string | null;
  publishedAtUtc: string | null;
}

export interface CareersPage {
  company: string;
  city: string | null;
  jobs: CareersJob[];
}

export function candidateForm(v: CandidateFormValues): FormData {
  const form = new FormData();
  const add = (k: string, value: string | undefined) => {
    if (value?.trim()) form.append(k, value.trim());
  };
  add('firstName', v.firstName);
  add('lastName', v.lastName);
  add('email', v.email);
  add('phone', v.phone);
  add('cnic', v.cnic);
  add('city', v.city);
  add('currentTitle', v.currentTitle);
  add('currentCompany', v.currentCompany);
  add('experienceYears', v.experienceYears);
  add('expectedSalary', v.expectedSalary);
  add('noticeDays', v.noticeDays);
  add('linkedInUrl', v.linkedInUrl);
  add('skills', v.skills);
  add('coverLetter', v.coverLetter);
  add('source', v.source);
  if (v.cv) form.append('cv', v.cv);
  return form;
}

export const EMPTY_CANDIDATE: CandidateFormValues = {
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  city: '',
  currentTitle: '',
  currentCompany: '',
  experienceYears: '',
  expectedSalary: '',
  noticeDays: '',
  linkedInUrl: '',
  skills: '',
  coverLetter: '',
  cv: null,
};

export const CV_ACCEPT = '.pdf,.doc,.docx';

export const recruitmentApi = {
  me: () => http.get<RecruitmentMe>('/recruitment/me'),
  setCareersSlug: (slug: string) => http.put('/recruitment/careers-slug', { slug }),

  requests: () => http.get<JobRequest[]>('/recruitment/requests'),
  createRequest: (b: SaveJobRequest) => http.post<JobRequest>('/recruitment/requests', b),
  approveRequest: (id: string, note: string | null) => http.post(`/recruitment/requests/${id}/approve`, { note }),
  rejectRequest: (id: string, note: string | null) => http.post(`/recruitment/requests/${id}/reject`, { note }),
  cancelRequest: (id: string) => http.post(`/recruitment/requests/${id}/cancel`),

  jobs: () => http.get<JobPost[]>('/recruitment/jobs'),
  job: (id: string) => http.get<JobPost>(`/recruitment/jobs/${id}`),
  createJob: (b: SaveJobPost) => http.post<JobPost>('/recruitment/jobs', b),
  updateJob: (id: string, b: SaveJobPost) => http.put<JobPost>(`/recruitment/jobs/${id}`, b),
  setJobStatus: (id: string, status: JobPostStatus) => http.post(`/recruitment/jobs/${id}/status`, { status }),
  deleteJob: (id: string) => http.del(`/recruitment/jobs/${id}`),
  pipeline: (id: string) => http.get<PipelineCard[]>(`/recruitment/jobs/${id}/pipeline`),
  addCandidate: (jobId: string, v: CandidateFormValues) => http.upload<PipelineCard>(`/recruitment/jobs/${jobId}/candidates`, candidateForm(v)),
  addExisting: (jobId: string, candidateId: string) => http.post<PipelineCard>(`/recruitment/jobs/${jobId}/candidates/${candidateId}`),

  openings: () => http.get<Opening[]>('/recruitment/openings'),
  refer: (id: string, v: CandidateFormValues) => http.upload<void>(`/recruitment/openings/${id}/refer`, candidateForm(v)),
  referrals: () => http.get<CandidateApplication[]>('/recruitment/referrals'),

  candidates: (search: string, blacklisted = false) =>
    http.get<CandidateListItem[]>('/recruitment/candidates', {
      search: search || undefined,
      blacklisted: blacklisted || undefined,
    }),
  candidate: (id: string) => http.get<Candidate>(`/recruitment/candidates/${id}`),
  updateCandidate: (id: string, b: SaveCandidate) => http.put<Candidate>(`/recruitment/candidates/${id}`, b),
  blacklist: (id: string, on: boolean, reason: string | null) => http.post(`/recruitment/candidates/${id}/blacklist`, { on, reason }),

  application: (id: string) => http.get<Application>(`/recruitment/applications/${id}`),
  move: (id: string, stage: ApplicationStage, reason: string | null, emailCandidate = false) =>
    http.post(`/recruitment/applications/${id}/move`, {
      stage,
      reason,
      emailCandidate,
    }),
  note: (id: string, text: string) => http.post(`/recruitment/applications/${id}/notes`, { text }),
  rate: (id: string, rating: number | null) => http.post(`/recruitment/applications/${id}/rating`, { rating }),
  schedule: (id: string, b: ScheduleInterview) => http.post(`/recruitment/applications/${id}/interviews`, b),
  offer: (
    id: string,
    b: {
      salary: number;
      joiningDate: string;
      note: string | null;
      emailCandidate: boolean;
    },
  ) => http.post(`/recruitment/applications/${id}/offer`, b),
  offerResponse: (id: string, accepted: boolean, note: string | null) =>
    http.post(`/recruitment/applications/${id}/offer-response`, {
      accepted,
      note,
    }),
  hire: (id: string, employeeCode: string | null) =>
    http.post<{ employeeId: string; employeeCode: string }>(`/recruitment/applications/${id}/hire`, { employeeCode }),

  myInterviews: () => http.get<MyInterview[]>('/recruitment/interviews/mine'),
  setInterviewStatus: (id: string, status: InterviewStatus) => http.post(`/recruitment/interviews/${id}/status`, { status }),
  feedback: (
    id: string,
    b: {
      rating: number;
      recommendation: HireRecommendation;
      strengths: string | null;
      concerns: string | null;
    },
  ) => http.post(`/recruitment/interviews/${id}/feedback`, b),

  report: () => http.get<RecruitmentReport>('/recruitment/report'),
  /** Current employees, for picking interviewers. */
  people: () => http.get<Array<{ id: string; name: string }>>('/recruitment/people'),

  careers: (company: string) => http.get<CareersPage>(`/careers/${company}`),
  careersJob: (company: string, job: string) => http.get<CareersJob>(`/careers/${company}/${job}`),
  apply: (company: string, job: string, v: CandidateFormValues) => http.upload<void>(`/careers/${company}/${job}/apply`, candidateForm(v)),
};
