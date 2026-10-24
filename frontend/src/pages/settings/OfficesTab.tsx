import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Crosshair, ExternalLink, MapPin, Plus } from 'lucide-react';
import { setupApi } from '../../api/organization';
import { errorMessage } from '../../api/client';
import { qk } from '../../api/keys';
import { Button, Card, CardHeader, EmptyState, ErrorState, Field, Input, SkeletonRows, Table, TBody, Td, Th, THead, Tr } from '../../components/ui';
import { getCurrentPosition } from '../../lib/geo';
import type { OfficeLocation } from '../../types';
import { DeleteConfirm, EditModal, RowActions, useSaveMutation } from './shared';

export function OfficesTab() {
  const list = useQuery({ queryKey: qk.offices, queryFn: setupApi.offices });
  const [editing, setEditing] = useState<OfficeLocation | 'new' | null>(null);
  const [deleting, setDeleting] = useState<OfficeLocation | null>(null);

  return (
    <Card>
      <CardHeader
        title="Office locations"
        description="When at least one office exists, web check-ins must be within an office's radius."
        actions={
          <Button size="sm" icon={<Plus className="size-3.5" />} onClick={() => setEditing('new')}>
            Add office
          </Button>
        }
      />
      {list.isLoading ? (
        <SkeletonRows rows={3} cols={4} />
      ) : list.isError ? (
        <ErrorState error={list.error} onRetry={() => list.refetch()} />
      ) : !list.data?.length ? (
        <EmptyState
          icon={MapPin}
          title="No office locations"
          description="Without offices, employees can check in from anywhere."
          action={<Button variant="secondary" onClick={() => setEditing('new')}>Add office</Button>}
        />
      ) : (
        <Table>
          <THead>
            <tr>
              <Th>Name</Th>
              <Th>Coordinates</Th>
              <Th className="text-end">Radius</Th>
              <Th className="text-end">
                <span className="sr-only">Actions</span>
              </Th>
            </tr>
          </THead>
          <TBody>
            {list.data.map((o) => (
              <Tr key={o.id}>
                <Td className="font-medium text-zinc-900 dark:text-zinc-100">{o.name}</Td>
                <Td>
                  <a
                    href={`https://www.openstreetmap.org/?mlat=${o.latitude}&mlon=${o.longitude}#map=17/${o.latitude}/${o.longitude}`}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 font-mono text-xs text-accent-600 hover:underline dark:text-accent-400"
                  >
                    {o.latitude.toFixed(5)}, {o.longitude.toFixed(5)}
                    <ExternalLink className="size-3" />
                  </a>
                </Td>
                <Td className="text-end tabular-nums">{o.radiusMeters} m</Td>
                <Td>
                  <RowActions history={{ type: 'OfficeLocation', id: o.id }} label={o.name} onEdit={() => setEditing(o)} onDelete={() => setDeleting(o)} />
                </Td>
              </Tr>
            ))}
          </TBody>
        </Table>
      )}
      {editing && <OfficeModal item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <DeleteConfirm
        item={deleting}
        name="Office location"
        remove={setupApi.deleteOffice}
        invalidate={[qk.offices, qk.attendanceToday]}
        onClose={() => setDeleting(null)}
      />
    </Card>
  );
}

function OfficeModal({ item, onClose }: { item: OfficeLocation | null; onClose: () => void }) {
  const [name, setName] = useState(item?.name ?? '');
  const [lat, setLat] = useState(item ? String(item.latitude) : '');
  const [lng, setLng] = useState(item ? String(item.longitude) : '');
  const [radius, setRadius] = useState(item ? String(item.radiusMeters) : '150');
  const [locating, setLocating] = useState(false);
  const { mutation, error, setError, fieldErrors, setFieldErrors } = useSaveMutation({
    save: (b: Omit<OfficeLocation, 'id'>) => (item ? setupApi.updateOffice(item.id, b) : setupApi.createOffice(b)),
    invalidate: [qk.offices, qk.attendanceToday],
    successMessage: item ? 'Office updated' : 'Office added',
    onSuccess: onClose,
  });

  const useMyLocation = async () => {
    setLocating(true);
    setError(null);
    try {
      const c = await getCurrentPosition();
      setLat(c.latitude.toFixed(6));
      setLng(c.longitude.toFixed(6));
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setLocating(false);
    }
  };

  return (
    <EditModal
      open
      title={item ? 'Edit office' : 'Add office'}
      onClose={onClose}
      saving={mutation.isPending}
      error={error}
      onSubmit={() => {
        const la = Number(lat);
        const lo = Number(lng);
        const r = Number(radius);
        const fe: Record<string, string> = {};
        if (!name.trim()) fe.name = 'Name is required.';
        if (lat === '' || !Number.isFinite(la) || la < -90 || la > 90) fe.latitude = 'Between -90 and 90.';
        if (lng === '' || !Number.isFinite(lo) || lo < -180 || lo > 180) fe.longitude = 'Between -180 and 180.';
        if (!Number.isFinite(r) || r < 20 || r > 5000) fe.radiusMeters = 'Between 20 and 5000 metres.';
        setFieldErrors(fe);
        if (!Object.keys(fe).length) mutation.mutate({ name: name.trim(), latitude: la, longitude: lo, radiusMeters: Math.round(r) });
      }}
    >
      <Field label="Name" required error={fieldErrors.name}>
        <Input value={name} maxLength={150} onChange={(e) => setName(e.target.value)} placeholder="Head office, Lahore" />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Latitude" required error={fieldErrors.latitude}>
          <Input inputMode="decimal" value={lat} onChange={(e) => setLat(e.target.value)} placeholder="31.520370" />
        </Field>
        <Field label="Longitude" required error={fieldErrors.longitude}>
          <Input inputMode="decimal" value={lng} onChange={(e) => setLng(e.target.value)} placeholder="74.358749" />
        </Field>
      </div>
      <Button variant="secondary" size="sm" loading={locating} icon={<Crosshair className="size-3.5" />} onClick={useMyLocation}>
        Use my current location
      </Button>
      <Field label="Radius (metres)" required hint="How far from this point a check-in is accepted (20–5000)." error={fieldErrors.radiusMeters}>
        <Input type="number" min={20} max={5000} value={radius} onChange={(e) => setRadius(e.target.value)} />
      </Field>
    </EditModal>
  );
}
