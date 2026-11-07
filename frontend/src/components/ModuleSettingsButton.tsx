import { Settings2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useUser } from '../auth/AuthContext';
import { isHr } from '../auth/roles';
import { Button } from './ui';

/**
 * Gear button in a module's page header that opens that module's settings (a page, or `?tab=…` for a settings tab on
 * the same page). HR and admins only.
 */
export function ModuleSettingsButton({ to, onClick }: { to?: string; onClick?: () => void }) {
  const navigate = useNavigate();
  if (!isHr(useUser())) return null;
  return (
    <Button variant="secondary" icon={<Settings2 className="size-4" />} onClick={onClick ?? (() => to && navigate(to))}>
      Settings
    </Button>
  );
}
