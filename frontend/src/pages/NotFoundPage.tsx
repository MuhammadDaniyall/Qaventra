import { useNavigate } from 'react-router-dom';
import { Compass } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { homeFor } from '../auth/guards';
import { Button, EmptyState } from '../components/ui';

export function NotFoundPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  return (
    <EmptyState
      icon={Compass}
      title="Page not found"
      description="The page you're looking for doesn't exist or has moved."
      className="min-h-[60vh]"
      action={<Button onClick={() => navigate(user ? homeFor(user) : '/login')}>Go home</Button>}
    />
  );
}
