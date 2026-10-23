import type { FormField } from '../../contracts/form';
import type { Feedback } from '../../contracts/feedback';
import { useTranslation } from '../../lib/i18n';
import { Dialog } from './dialog';
import { RecordForm } from './record-form';
import { FeedbackBox } from './feedback';
import { Button } from '../ui/button';

export function EditorDialog({
  action,
  fields,
  initial,
  revision,
  busy,
  feedback,
  fieldErrors,
  onClose,
  onSubmit,
  onReload,
}: {
  action: string;
  fields: FormField[];
  initial: Record<string, unknown>;
  revision: number;
  busy: boolean;
  feedback: Feedback | undefined;
  fieldErrors: Record<string, string>;
  onClose: () => void;
  onSubmit: (values: Record<string, unknown>) => Promise<void>;
  onReload: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog title={t(action)} onClose={onClose} busy={busy}>
      <FeedbackBox value={feedback} onRetry={feedback?.conflict ? onReload : undefined} />
      {action === 'delete' ? (
        <>
          <h3>{t('confirmDelete')}</h3>
          <p>{t('deleteDescription')}</p>
          <Button variant="destructive" disabled={busy} onClick={() => void onSubmit({})}>
            {t('delete')}
          </Button>
        </>
      ) : (
        <RecordForm
          key={revision}
          fields={fields}
          initial={initial}
          onSubmit={onSubmit}
          busy={busy}
          disabled={feedback?.conflict === true}
          fieldErrors={fieldErrors}
        />
      )}
    </Dialog>
  );
}
