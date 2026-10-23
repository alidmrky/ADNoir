import type { Feedback } from '../../contracts/feedback';
import { useTranslation } from '../../lib/i18n';
import { Button } from '../ui/button';
export function FeedbackBox({
  value,
  onRetry,
}: {
  value?: Feedback | undefined;
  onRetry?: (() => void) | undefined;
}) {
  const { t } = useTranslation();
  if (!value) return null;
  return (
    <div
      className={`feedback feedback-${value.kind}`}
      role={value.kind === 'error' ? 'alert' : 'status'}
    >
      <strong>
        {value.temporary ? t('temporaryError') : value.conflict ? t('conflict') : value.message}
      </strong>
      {(value.temporary || value.conflict) && <p>{value.message}</p>}
      {value.errors.length > 0 && (
        <ul>
          {value.errors.map((error, i) => (
            <li key={i}>
              {error.field && <span>{error.field}: </span>}
              {error.message || value.message}
            </li>
          ))}
        </ul>
      )}
      {value.correlationId && (
        <small>
          {t('reference')}: <code>{value.correlationId}</code>
        </small>
      )}
      {onRetry && (
        <Button type="button" variant="outline" size="sm" onClick={onRetry}>
          {t(value.conflict ? 'reloadRecord' : 'retry')}
        </Button>
      )}
    </div>
  );
}
