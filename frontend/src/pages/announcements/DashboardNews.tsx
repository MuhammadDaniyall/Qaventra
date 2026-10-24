import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { BarChart3, ChevronRight, Megaphone, Pin } from 'lucide-react';
import { announcementsApi } from '../../api/engagement';
import { qk } from '../../api/keys';
import { Badge, Card, CardHeader } from '../../components/ui';
import { formatDate } from '../../lib/format';

/** Latest announcements and polls waiting for a vote. Hidden when there is nothing. */
export function DashboardNews({ className }: { className?: string }) {
  const news = useQuery({ queryKey: qk.announcements, queryFn: announcementsApi.feed });
  const polls = useQuery({ queryKey: qk.polls, queryFn: announcementsApi.polls });
  const items = (news.data ?? []).slice(0, 3);
  const toVote = (polls.data ?? []).filter((p) => p.isOpen && !p.myOptionId);
  if (!items.length && !toVote.length) return null;
  return (
    <Card className={className}>
      <CardHeader
        title="Announcements"
        actions={
          <Link to="/announcements" className="flex items-center gap-0.5 text-xs font-medium text-accent-600 hover:underline dark:text-accent-400">
            See all <ChevronRight className="size-3.5" />
          </Link>
        }
      />
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {toVote.map((p) => (
          <li key={p.id}>
            <Link to="/announcements?tab=polls" className="flex items-center gap-3 px-5 py-3 hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
              <BarChart3 className="size-4 shrink-0 text-accent-500" />
              <span className="min-w-0 flex-1 truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{p.question}</span>
              <Badge tone="amber">Vote by {formatDate(p.closesOn)}</Badge>
            </Link>
          </li>
        ))}
        {items.map((a) => (
          <li key={a.id}>
            <Link to="/announcements" className="flex items-center gap-3 px-5 py-3 hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
              {a.isPinned ? <Pin className="size-4 shrink-0 text-accent-500" /> : <Megaphone className="size-4 shrink-0 text-zinc-500 dark:text-zinc-400" />}
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{a.title}</span>
                <span className="block truncate text-xs text-zinc-500 dark:text-zinc-400">{a.body}</span>
              </span>
              {!a.isRead && <span className="size-2 shrink-0 rounded-full bg-accent-500" aria-label="Unread" />}
            </Link>
          </li>
        ))}
      </ul>
    </Card>
  );
}
