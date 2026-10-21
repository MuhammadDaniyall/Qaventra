import { http } from './client';
import { downloadFile } from './excel';

export type AssetCondition = 'New' | 'Good' | 'Fair' | 'Damaged';
export type AssetStatus = 'Available' | 'Assigned' | 'InRepair' | 'Retired' | 'Lost';
export type AssetRequestKind = 'New' | 'Return';
export type AssetRequestStatus = 'Pending' | 'Approved' | 'Fulfilled' | 'Rejected' | 'Cancelled';

export const CONDITIONS: AssetCondition[] = ['New', 'Good', 'Fair', 'Damaged'];
export const ASSET_STATUS_LABELS: Record<AssetStatus, string> = {
  Available: 'Available',
  Assigned: 'Assigned',
  InRepair: 'In repair',
  Retired: 'Retired',
  Lost: 'Lost',
};

export interface AssetType {
  id: string;
  name: string;
  allowMultiple: boolean;
  returnOnExit: boolean;
  handlerIds: string[];
  handlers: string[];
  isActive: boolean;
  total: number;
  available: number;
  assigned: number;
}

export interface SaveAssetType {
  name: string;
  allowMultiple: boolean;
  returnOnExit: boolean;
  handlerIds: string[];
  isActive: boolean;
}

export interface AssetKit {
  id: string;
  name: string;
  typeIds: string[];
  types: string[];
}

export interface Asset {
  id: string;
  tag: string;
  name: string;
  typeId: string;
  type: string;
  serialNumber: string | null;
  purchasedOn: string | null;
  cost: number | null;
  warrantyUntil: string | null;
  condition: AssetCondition;
  status: AssetStatus;
  notes: string | null;
  holderEmployeeId: string | null;
  holder: string | null;
  assignmentId: string | null;
  assignedOn: string | null;
  dueBack: string | null;
  isOverdue: boolean;
  canManage: boolean;
}

export interface SaveAsset {
  typeId: string;
  tag: string | null;
  name: string;
  serialNumber: string | null;
  purchasedOn: string | null;
  cost: number | null;
  warrantyUntil: string | null;
  condition: AssetCondition;
  notes: string | null;
}

export interface Assignment {
  id: string;
  assetId: string;
  tag: string;
  assetName: string;
  type: string;
  serialNumber: string | null;
  employeeId: string;
  employeeName: string;
  assignedOn: string;
  dueBack: string | null;
  returnedOn: string | null;
  conditionOut: AssetCondition;
  conditionIn: AssetCondition | null;
  handedOverByName: string | null;
  receivedByName: string | null;
  notes: string | null;
  isOverdue: boolean;
  returnOnExit: boolean;
  returnRequested: boolean;
}

export interface AssetRequest {
  id: string;
  kind: AssetRequestKind;
  employeeId: string;
  employeeName: string;
  employeeCode: string;
  typeId: string;
  type: string;
  assignmentId: string | null;
  assetLabel: string | null;
  reason: string;
  neededBy: string | null;
  returnBy: string | null;
  status: AssetRequestStatus;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  decisionNote: string | null;
  /** The signed-in user can hand it over or take it back now. */
  canFulfill: boolean;
}

export interface AssetSummary {
  total: number;
  available: number;
  assigned: number;
  inRepair: number;
  retired: number;
  lost: number;
  overdueReturns: number;
  toHandOver: number;
  toTakeBack: number;
  value: number;
}

export const assetsApi = {
  me: () => http.get<{ isHandler: boolean }>('/assets/me'),
  types: () => http.get<AssetType[]>('/assets/types'),
  createType: (b: SaveAssetType) => http.post('/assets/types', b),
  updateType: (id: string, b: SaveAssetType) => http.put(`/assets/types/${id}`, b),
  deleteType: (id: string) => http.del(`/assets/types/${id}`),
  kits: () => http.get<AssetKit[]>('/assets/kits'),
  createKit: (b: { name: string; typeIds: string[] }) => http.post('/assets/kits', b),
  updateKit: (id: string, b: { name: string; typeIds: string[] }) => http.put(`/assets/kits/${id}`, b),
  deleteKit: (id: string) => http.del(`/assets/kits/${id}`),
  applyKit: (id: string, employeeId: string) => http.post<{ requests: number }>(`/assets/kits/${id}/apply/${employeeId}`),
  summary: () => http.get<AssetSummary>('/assets/summary'),
  list: (q: { status?: string; typeId?: string; search?: string; overdue?: boolean }) => http.get<Asset[]>('/assets', q),
  get: (id: string) => http.get<{ asset: Asset; history: Assignment[] }>(`/assets/${id}`),
  create: (b: SaveAsset) => http.post<Asset>('/assets', b),
  update: (id: string, b: SaveAsset) => http.put<Asset>(`/assets/${id}`, b),
  setStatus: (id: string, status: AssetStatus, note: string | null) => http.post(`/assets/${id}/status`, { status, note }),
  assign: (id: string, b: { employeeId: string; assignedOn: string; dueBack: string | null; condition: AssetCondition; notes: string | null }) =>
    http.post(`/assets/${id}/assign`, b),
  takeBack: (assignmentId: string, b: { returnedOn: string; condition: AssetCondition; notes: string | null }) =>
    http.post(`/assets/assignments/${assignmentId}/return`, b),
  exportExcel: (fileName: string) => downloadFile('/assets/export', fileName),
  my: () => http.get<{ holding: Assignment[]; returned: Assignment[]; requests: AssetRequest[] }>('/assets/my'),
  forEmployee: (employeeId: string) => http.get<{ holding: Assignment[]; returned: Assignment[]; open: AssetRequest[] }>(`/assets/employees/${employeeId}`),
  requestNew: (b: { typeId: string; reason: string; neededBy: string | null; returnBy: string | null }) => http.post<AssetRequest>('/assets/requests/new', b),
  requestReturn: (assignmentId: string, reason: string | null) => http.post<AssetRequest>('/assets/requests/return', { assignmentId, reason }),
  requests: (status: AssetRequestStatus = 'Approved') => http.get<AssetRequest[]>('/assets/requests', { status }),
  approve: (id: string, note: string | null) => http.post(`/assets/requests/${id}/approve`, { note }),
  reject: (id: string, note: string | null) => http.post(`/assets/requests/${id}/reject`, { note }),
  cancel: (id: string) => http.post(`/assets/requests/${id}/cancel`),
  fulfill: (id: string, b: { assetId: string | null; on: string; condition: AssetCondition; notes: string | null }) => http.post(`/assets/requests/${id}/fulfill`, b),
};
