import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileText, Printer } from 'lucide-react';
import { lettersApi } from '../../api/letters';
import { Button, Modal } from '../../components/ui';
import { formatDate } from '../../lib/format';

/** The prepared offer letter for an application (from the template set to issue on offers), to print or save as PDF. */
export function OfferLetterButton({ applicationId }: { applicationId: string }) {
  const [open, setOpen] = useState(false);
  const letter = useQuery({ queryKey: ['letters', 'candidate', applicationId], queryFn: () => lettersApi.candidate(applicationId) });
  const l = letter.data;
  if (!l) return null;
  return (
    <>
      <Button size="sm" variant="secondary" icon={<FileText className="size-3.5" />} onClick={() => setOpen(true)}>
        Offer letter
      </Button>
      {open && (
        <Modal
          open
          onClose={() => setOpen(false)}
          size="xl"
          title={l.subject}
          footer={
            <Button icon={<Printer className="size-4" />} onClick={() => window.print()}>
              Print / PDF
            </Button>
          }
        >
          <article className="print-area bg-white text-zinc-800" style={{ padding: `${l.marginTopMm}mm ${l.marginSideMm}mm 15mm` }}>
            <div className="flex justify-between border-b-2 border-accent-600 pb-4">
              <div>
                <p className="text-xl font-semibold">{l.companyName}</p>
                {l.companyAddress && <p className="text-sm text-zinc-500">{l.companyAddress}</p>}
              </div>
              <div className="text-right text-sm">
                <p>Ref: {l.referenceNo}</p>
                <p>Date: {formatDate(l.issuedOn)}</p>
              </div>
            </div>
            <p className="mt-6 font-medium">{l.candidateName}</p>
            <h2 className="mt-6 font-semibold underline">Subject: {l.subject}</h2>
            <div className="mt-4 font-serif text-[15px] leading-7 whitespace-pre-wrap">{l.body}</div>
            <div className="mt-12 w-56 border-t border-zinc-400 pt-2 text-sm">
              <p className="font-medium">{l.issuedByName ?? 'Human Resources'}</p>
              <p className="text-zinc-500">For {l.companyName}</p>
            </div>
            {l.footer && <p className="mt-8 border-t pt-2 text-center text-xs text-zinc-500">{l.footer}</p>}
          </article>
        </Modal>
      )}
    </>
  );
}
