import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { employeesApi } from '../../api/employees';
import { ApiError, errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { PageHeader, useToast } from '../../components/ui';
import type { SaveEmployeeRequest } from '../../types';
import { EmployeeForm, emptyEmployee } from './EmployeeForm';

export function NewEmployeePage() {
  const navigate = useNavigate();
  const toast = useToast();
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const create = useMutation({
    mutationFn: (b: SaveEmployeeRequest) => employeesApi.create(b),
    onSuccess: (e) => {
      toast.success(`${e.firstName} ${e.lastName} added`, `Employee code ${e.employeeCode}`);
      qc.invalidateQueries({ queryKey: qk.employees });
      qc.invalidateQueries({ queryKey: qk.dashboard });
      navigate(`/employees/${e.id}`, { replace: true });
    },
    onError: (err) => {
      // e.g. the company's plan limit: the ProblemDetails title explains it.
      toast.error(err);
      setError(errorMessage(err));
      if (err instanceof ApiError) setFieldErrors(err.fieldErrors());
      window.scrollTo({ top: 0, behavior: 'smooth' });
    },
  });

  return (
    <>
      <PageHeader
        title="Add employee"
        description="You can set up salary and a login after saving."
        back={{ to: '/employees', label: 'Employees' }}
      />
      <EmployeeForm
        initial={emptyEmployee()}
        submitLabel="Save employee"
        submitting={create.isPending}
        serverError={error}
        serverFieldErrors={fieldErrors}
        onSubmit={(b) => {
          setError(null);
          setFieldErrors({});
          create.mutate(b);
        }}
        onCancel={() => navigate('/employees')}
      />
    </>
  );
}
