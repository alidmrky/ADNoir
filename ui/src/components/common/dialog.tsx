import { useEffect, useRef, type ReactNode } from 'react';
import { useTranslation } from '../../lib/i18n';
import { Button } from '../ui/button';
export function Dialog({
  title,
  children,
  onClose,
  busy = false,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  busy?: boolean;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const { t } = useTranslation();
  useEffect(() => {
    const dialog = ref.current;
    dialog?.showModal();
    return () => dialog?.close();
  }, []);
  return (
    <dialog
      ref={ref}
      className="dialog"
      aria-labelledby="dialog-title"
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onClose();
      }}
    >
      <div className="dialog-header">
        <h2 id="dialog-title">{title}</h2>
        <Button variant="ghost" disabled={busy} onClick={onClose}>
          {t('close')}
        </Button>
      </div>
      {children}
    </dialog>
  );
}
