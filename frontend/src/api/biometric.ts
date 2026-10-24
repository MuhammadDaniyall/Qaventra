import { http } from './client';

export interface BiometricDevice {
  id: string;
  name: string;
  serialNumber: string | null;
  officeLocationId: string | null;
  office: string | null;
  tokenPrefix: string;
  isActive: boolean;
  lastSeenAtUtc: string | null;
  punchesReceived: number;
}

export interface SaveBiometricDevice {
  name: string;
  serialNumber: string | null;
  officeLocationId: string | null;
  isActive: boolean;
}

export const biometricApi = {
  list: () => http.get<BiometricDevice[]>('/biometric/devices'),
  create: (b: SaveBiometricDevice) => http.post<{ device: BiometricDevice; token: string }>('/biometric/devices', b),
  update: (id: string, b: SaveBiometricDevice) => http.put<BiometricDevice>(`/biometric/devices/${id}`, b),
  newToken: (id: string) => http.post<{ device: BiometricDevice; token: string }>(`/biometric/devices/${id}/token`),
  remove: (id: string) => http.del(`/biometric/devices/${id}`),
};
