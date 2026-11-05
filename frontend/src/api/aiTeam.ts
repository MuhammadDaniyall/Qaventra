import { http } from './client';

export type RobotKind =
  | 'PayrollChecker'
  | 'AttendanceWatcher'
  | 'CvScreener'
  | 'HelpdeskResponder'
  | 'PeopleRisk'
  | 'OnboardingCoach'
  | 'LeavePlanner'
  | 'DocumentWatcher';
export type RobotAutonomy = 'Advisory' | 'Supervised' | 'Routine';
export type RobotProposalStatus = 'Pending' | 'Approved' | 'Rejected' | 'Failed' | 'AutoDone' | 'Expired';

export interface Robot {
  id: string;
  kind: RobotKind;
  name: string;
  about: string;
  does: string[];
  canPropose: boolean;
  enabled: boolean;
  autonomy: RobotAutonomy;
  ownerUserId: string | null;
  ownerName: string | null;
  runHour: number;
  runsPerMonth: number;
  runsThisMonth: number;
  lastRunAtUtc: string | null;
  lastSummary: string | null;
  pending: number;
}

export interface AiTeam {
  stopped: boolean;
  stoppedBy: string | null;
  aiOn: boolean;
  robots: Robot[];
  owners: { id: string; name: string }[];
  pending: number;
}

export interface RobotFinding {
  title: string;
  detail: string;
  level: 'good' | 'watch' | 'act';
  link: string | null;
}

export interface RobotRun {
  id: string;
  robotId: string;
  robotName: string;
  manual: boolean;
  startedByName: string | null;
  status: 'Running' | 'Done' | 'Failed';
  startedAtUtc: string;
  finishedAtUtc: string | null;
  summary: string | null;
  steps: string[];
  findings: RobotFinding[];
  proposals: number;
  usedAi: boolean;
  error: string | null;
}

export interface RobotProposal {
  id: string;
  robotId: string;
  robotName: string;
  action: 'notify_employee' | 'helpdesk_reply' | 'shortlist';
  title: string;
  detail: string;
  link: string | null;
  status: RobotProposalStatus;
  createdAtUtc: string;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  note: string | null;
  canEdit: boolean;
}

export interface RobotSettings {
  enabled: boolean;
  autonomy: RobotAutonomy;
  ownerUserId: string | null;
  runHour: number;
  runsPerMonth: number;
}

export const aiTeamApi = {
  get: () => http.get<AiTeam>('/ai-team'),
  update: (id: string, s: RobotSettings) => http.put(`/ai-team/${id}`, s),
  run: (id: string) => http.post<RobotRun>(`/ai-team/${id}/run`),
  stop: (stopped: boolean) => http.post('/ai-team/stop', { stopped }),
  runs: (robotId?: string) => http.get<RobotRun[]>('/ai-team/runs', robotId ? { robotId } : undefined),
  proposals: (pending = true) => http.get<RobotProposal[]>('/ai-team/proposals', { pending }),
  decide: (id: string, approve: boolean, note?: string, body?: string) =>
    http.post(`/ai-team/proposals/${id}/decide`, { approve, note: note || null, body: body || null }),
};
