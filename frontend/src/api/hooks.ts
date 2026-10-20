import { useQuery } from '@tanstack/react-query';
import { employeesApi } from './employees';
import { qk } from './keys';
import { orgApi, setupApi } from './organization';

const STALE = 5 * 60 * 1000;

export const useDepartments = () => useQuery({ queryKey: qk.departments, queryFn: orgApi.departments, staleTime: STALE });
export const useDesignations = () => useQuery({ queryKey: qk.designations, queryFn: orgApi.designations, staleTime: STALE });
export const useShifts = () => useQuery({ queryKey: qk.shifts, queryFn: setupApi.shifts, staleTime: STALE });
export const useLeaveTypes = () => useQuery({ queryKey: qk.leaveTypes, queryFn: setupApi.leaveTypes, staleTime: STALE });
export const useEmployeeLookup = (enabled = true) =>
  useQuery({ queryKey: qk.employeeLookup, queryFn: employeesApi.lookup, staleTime: STALE, enabled });
