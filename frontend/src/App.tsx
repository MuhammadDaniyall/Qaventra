import { lazy, Suspense } from 'react';
import { Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { GuestOnly, homeFor, RequireAuth, RequireEmployee, RequireRole } from './auth/guards';
import { APPROVER_ROLES, COMPANY_ROLES, HR_ROLES } from './auth/roles';
import { AppLayout } from './components/layout/AppLayout';
import { PageSpinner } from './components/ui';
import { NotFoundPage } from './pages/NotFoundPage';
import { LoginPage } from './pages/auth/LoginPage';

const DailyAttendancePage = lazy(() => import('./pages/attendance/DailyAttendancePage').then((m) => ({ default: m.DailyAttendancePage })));
const RosterPage = lazy(() => import('./pages/attendance/RosterPage').then((m) => ({ default: m.RosterPage })));
const MonthlyAttendancePage = lazy(() => import('./pages/attendance/MonthlyAttendancePage').then((m) => ({ default: m.MonthlyAttendancePage })));
const MyAttendancePage = lazy(() => import('./pages/attendance/MyAttendancePage').then((m) => ({ default: m.MyAttendancePage })));
const ApprovalsInboxPage = lazy(() => import('./pages/approvals/ApprovalsInboxPage').then((m) => ({ default: m.ApprovalsInboxPage })));
const AuditLogsPage = lazy(() => import('./pages/audit/AuditLogsPage').then((m) => ({ default: m.AuditLogsPage })));
const DashboardPage = lazy(() => import('./pages/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })));
const EmployeeDetailPage = lazy(() => import('./pages/employees/EmployeeDetailPage').then((m) => ({ default: m.EmployeeDetailPage })));
const EmployeesPage = lazy(() => import('./pages/employees/EmployeesPage').then((m) => ({ default: m.EmployeesPage })));
const NewEmployeePage = lazy(() => import('./pages/employees/NewEmployeePage').then((m) => ({ default: m.NewEmployeePage })));
const LeaveApprovalsPage = lazy(() => import('./pages/leave/LeaveApprovalsPage').then((m) => ({ default: m.LeaveApprovalsPage })));
const MyLeavePage = lazy(() => import('./pages/leave/MyLeavePage').then((m) => ({ default: m.MyLeavePage })));
const MyPayslipsPage = lazy(() => import('./pages/payroll/MyPayslipsPage').then((m) => ({ default: m.MyPayslipsPage })));
const PayrollRunPage = lazy(() => import('./pages/payroll/PayrollRunPage').then((m) => ({ default: m.PayrollRunPage })));
const PayrollRunsPage = lazy(() => import('./pages/payroll/PayrollRunsPage').then((m) => ({ default: m.PayrollRunsPage })));
const PayrollSettingsPage = lazy(() => import('./pages/payroll/PayrollSettingsPage').then((m) => ({ default: m.PayrollSettingsPage })));
const PayslipPage = lazy(() => import('./pages/payroll/PayslipPage').then((m) => ({ default: m.PayslipPage })));
const CompanyDetailPage = lazy(() => import('./pages/platform/CompanyDetailPage').then((m) => ({ default: m.CompanyDetailPage })));
const CompaniesPage = lazy(() => import('./pages/platform/CompaniesPage').then((m) => ({ default: m.CompaniesPage })));
const PlatformDashboardPage = lazy(() => import('./pages/platform/PlatformDashboardPage').then((m) => ({ default: m.PlatformDashboardPage })));
const MyPayPage = lazy(() => import('./pages/pay/MyPayPage').then((m) => ({ default: m.MyPayPage })));
const PayAdjustmentsPage = lazy(() => import('./pages/pay/PayAdjustmentsPage').then((m) => ({ default: m.PayAdjustmentsPage })));
const MyProfilePage = lazy(() => import('./pages/profile/MyProfilePage').then((m) => ({ default: m.MyProfilePage })));
const ExpenseApprovalsPage = lazy(() => import('./pages/expenses/ExpenseApprovalsPage').then((m) => ({ default: m.ExpenseApprovalsPage })));
const MyExpensesPage = lazy(() => import('./pages/expenses/MyExpensesPage').then((m) => ({ default: m.MyExpensesPage })));
const LetterPage = lazy(() => import('./pages/letters/LetterPage').then((m) => ({ default: m.LetterPage })));
const LettersPage = lazy(() => import('./pages/letters/LettersPage').then((m) => ({ default: m.LettersPage })));
const MyDocumentsPage = lazy(() => import('./pages/letters/MyDocumentsPage').then((m) => ({ default: m.MyDocumentsPage })));
const MyExitPage = lazy(() => import('./pages/separations/MyExitPage').then((m) => ({ default: m.MyExitPage })));
const SeparationDetailPage = lazy(() => import('./pages/separations/SeparationDetailPage').then((m) => ({ default: m.SeparationDetailPage })));
const SeparationsPage = lazy(() => import('./pages/separations/SeparationsPage').then((m) => ({ default: m.SeparationsPage })));
const OrganizationPage = lazy(() => import('./pages/settings/OrganizationPage').then((m) => ({ default: m.OrganizationPage })));

function Lazy() {
  return (
    <Suspense fallback={<PageSpinner />}>
      <Outlet />
    </Suspense>
  );
}

function Home() {
  const { user, initializing } = useAuth();
  if (initializing) return <PageSpinner />;
  return <Navigate to={user ? homeFor(user) : '/login'} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route element={<GuestOnly />}>
        <Route path="/login" element={<LoginPage />} />
      </Route>

      <Route path="/" element={<Home />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route element={<Lazy />}>
          {/* Platform (owner, not inside a company) */}
          <Route element={<RequireRole roles={['SuperAdmin']} />}>
            <Route path="/platform" element={<PlatformDashboardPage />} />
            <Route path="/platform/companies" element={<CompaniesPage />} />
            <Route path="/platform/companies/:id" element={<CompanyDetailPage />} />
            <Route path="/platform/audit-logs" element={<AuditLogsPage />} />
          </Route>

          {/* Company area */}
          <Route element={<RequireRole roles={COMPANY_ROLES} />}>
            <Route path="/dashboard" element={<DashboardPage />} />
            {/* Self-service needs an employee record (hidden for the owner inside a company). */}
            <Route element={<RequireEmployee />}>
              <Route path="/me/attendance" element={<MyAttendancePage />} />
              <Route path="/me/leave" element={<MyLeavePage />} />
              <Route path="/me/payslips" element={<MyPayslipsPage />} />
              <Route path="/me/profile" element={<MyProfilePage />} />
              <Route path="/me/expenses" element={<MyExpensesPage />} />
              <Route path="/me/pay" element={<MyPayPage />} />
              <Route path="/me/documents" element={<MyDocumentsPage />} />
              <Route path="/me/resignation" element={<MyExitPage />} />
            </Route>
            <Route path="/payslips/:id" element={<PayslipPage />} />
            <Route path="/letters/:id" element={<LetterPage />} />

            <Route element={<RequireRole roles={APPROVER_ROLES} />}>
              <Route path="/approvals" element={<ApprovalsInboxPage />} />
              <Route path="/employees" element={<EmployeesPage />} />
              <Route path="/employees/:id" element={<EmployeeDetailPage />} />
              <Route path="/attendance/daily" element={<DailyAttendancePage />} />
              <Route path="/attendance/roster" element={<RosterPage />} />
              <Route path="/attendance/monthly" element={<MonthlyAttendancePage />} />
              <Route path="/leave/approvals" element={<LeaveApprovalsPage />} />
              <Route path="/expenses/approvals" element={<ExpenseApprovalsPage />} />
            </Route>

            <Route element={<RequireRole roles={HR_ROLES} />}>
              <Route path="/employees/new" element={<NewEmployeePage />} />
              <Route path="/payroll" element={<PayrollRunsPage />} />
              <Route path="/payroll/runs/:id" element={<PayrollRunPage />} />
              <Route path="/payroll/settings" element={<PayrollSettingsPage />} />
              <Route path="/payroll/adjustments" element={<PayAdjustmentsPage />} />
              <Route path="/settings" element={<OrganizationPage />} />
              <Route path="/letters" element={<LettersPage />} />
              <Route path="/separations" element={<SeparationsPage />} />
              <Route path="/separations/:id" element={<SeparationDetailPage />} />
            </Route>

            <Route element={<RequireRole roles={['CompanyAdmin']} />}>
              <Route path="/audit-logs" element={<AuditLogsPage />} />
            </Route>
          </Route>

          <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}
