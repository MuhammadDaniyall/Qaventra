import { http } from './client';

export type AnnouncementKind = 'News' | 'Policy' | 'Event';
export type PollResultsVisibility = 'AfterVoting' | 'AfterClose' | 'HrOnly';
export type CustomFieldType = 'Text' | 'Number' | 'Date' | 'Dropdown' | 'YesNo';

export interface Announcement {
  id: string;
  title: string;
  body: string;
  kind: AnnouncementKind;
  publishFrom: string;
  publishTo: string | null;
  departmentIds: string[];
  departments: string[];
  isPinned: boolean;
  linkUrl: string | null;
  createdByName: string | null;
  createdAtUtc: string;
  isRead: boolean;
  attachments: number;
  /** HR only. */
  readCount: number | null;
  audienceCount: number | null;
}

export interface SaveAnnouncement {
  title: string;
  body: string;
  kind: AnnouncementKind;
  publishFrom: string;
  publishTo: string | null;
  departmentIds: string[];
  isPinned: boolean;
  linkUrl: string | null;
  notify: boolean;
}

export interface PollOption {
  id: string;
  text: string;
  /** Null while results are hidden from the viewer. */
  votes: number | null;
  /** HR only, and never for anonymous polls. */
  voters: string[] | null;
}

export interface Poll {
  id: string;
  question: string;
  description: string | null;
  opensOn: string;
  closesOn: string;
  isOpen: boolean;
  isAnonymous: boolean;
  resultsVisibility: PollResultsVisibility;
  departments: string[];
  createdByName: string | null;
  options: PollOption[];
  myOptionId: string | null;
  resultsShown: boolean;
  totalVotes: number | null;
  audienceCount: number | null;
}

export interface CreatePoll {
  question: string;
  description: string | null;
  opensOn: string;
  closesOn: string;
  departmentIds: string[];
  isAnonymous: boolean;
  resultsVisibility: PollResultsVisibility;
  options: string[];
}

export interface CustomField {
  id: string;
  label: string;
  type: CustomFieldType;
  options: string[];
  isRequired: boolean;
  visibleToEmployee: boolean;
  order: number;
  isActive: boolean;
  /** How many employees have a value. */
  filled: number;
}

export interface SaveCustomField {
  label: string;
  type: CustomFieldType;
  options: string[];
  isRequired: boolean;
  visibleToEmployee: boolean;
  isActive: boolean;
}

export interface CustomValue {
  fieldId: string;
  label: string;
  type: CustomFieldType;
  options: string[];
  isRequired: boolean;
  value: string;
}

export const announcementsApi = {
  feed: () => http.get<Announcement[]>('/announcements'),
  manage: () => http.get<Announcement[]>('/announcements/manage'),
  create: (b: SaveAnnouncement) => http.post<Announcement>('/announcements', b),
  update: (id: string, b: SaveAnnouncement) => http.put<Announcement>(`/announcements/${id}`, b),
  remove: (id: string) => http.del(`/announcements/${id}`),
  read: (id: string) => http.post(`/announcements/${id}/read`),
  polls: () => http.get<Poll[]>('/announcements/polls'),
  managePolls: () => http.get<Poll[]>('/announcements/polls/manage'),
  createPoll: (b: CreatePoll) => http.post<Poll>('/announcements/polls', b),
  removePoll: (id: string) => http.del(`/announcements/polls/${id}`),
  vote: (id: string, optionId: string) => http.post<Poll>(`/announcements/polls/${id}/vote`, { optionId }),
};

export const customFieldsApi = {
  list: () => http.get<CustomField[]>('/setup/custom-fields'),
  create: (b: SaveCustomField) => http.post('/setup/custom-fields', b),
  update: (id: string, b: SaveCustomField) => http.put(`/setup/custom-fields/${id}`, b),
  remove: (id: string) => http.del(`/setup/custom-fields/${id}`),
  values: (employeeId: string) => http.get<CustomValue[]>(`/employees/${employeeId}/custom-fields`),
  mine: () => http.get<CustomValue[]>('/employees/me/custom-fields'),
  save: (employeeId: string, values: Record<string, string | null>) => http.put<CustomValue[]>(`/employees/${employeeId}/custom-fields`, { values }),
};
