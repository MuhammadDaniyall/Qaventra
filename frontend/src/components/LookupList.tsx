import { useLookups, type LookupKind } from '../api/employeeFile';

/** Suggestions from a company list (banks, cities, nationalities) for a free-text input: give the input list={lookupListId(kind)}. */
export function LookupList({ kind }: { kind: LookupKind }) {
  const lookups = useLookups();
  return (
    <datalist id={lookupListId(kind)}>
      {(lookups.data ?? [])
        .filter((l) => l.kind === kind && l.isActive)
        .map((l) => (
          <option key={l.id} value={l.name} />
        ))}
    </datalist>
  );
}

export const lookupListId = (kind: LookupKind) => `lookup-list-${kind}`;
