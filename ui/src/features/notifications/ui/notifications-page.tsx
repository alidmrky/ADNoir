import { useState } from 'react';
import { RefreshCw, Mail, RotateCw, Send } from 'lucide-react';
import { useEmails, useEmail, useStatistics } from '../api/queries';
import { deliveryStatuses } from '../model/statuses';
import { errorFeedback } from '../../../lib/query/feedback';
import { useTranslation } from '../../../lib/i18n';
import { formatDate } from '../../../lib/format';
import { PageHeading } from '../../../components/common/page-heading';
import { DataTable } from '../../../components/common/table';
import { Dialog } from '../../../components/common/dialog';
import { FeedbackBox } from '../../../components/common/feedback';
import { Button } from '../../../components/ui/button';
export function NotificationsPage() {
  const { t, language } = useTranslation();
  const [status, setStatus] = useState('');
  const [selected, setSelected] = useState('');
  const query = useEmails(status, language);
  const statistics = useStatistics(language);
  const detail = useEmail(selected, language);
  const stats = statistics.data;
  return (
    <>
      <PageHeading name="notifications">
        <Button
          variant="outline"
          disabled={query.isFetching || statistics.isFetching}
          onClick={() => {
            void query.refetch();
            void statistics.refetch();
          }}
        >
          <RefreshCw size={16} />
          {t('refresh')}
        </Button>
      </PageHeading>
      <FeedbackBox
        value={errorFeedback(statistics.error)}
        onRetry={() => void statistics.refetch()}
      />
      {stats && (
        <>
          <div className="stat-grid">
            {[
              { label: 'total', value: stats.total, Icon: Mail },
              { label: 'attempts', value: stats.attempts, Icon: Send },
              { label: 'retriedMessages', value: stats.retriedMessages, Icon: RotateCw },
            ].map(({ label, value, Icon }) => (
              <div className="stat-card" key={label}>
                <span>
                  {t(label)}
                  <Icon size={19} />
                </span>
                <strong>{value.toLocaleString(language)}</strong>
              </div>
            ))}
          </div>
          <p className="helper-text">
            {t('statisticsNote')} {formatDate(stats.fromUtc, language)} —{' '}
            {formatDate(stats.toUtc, language)}
          </p>
        </>
      )}
      <div className="table-toolbar">
        <h2>{t('notifications')}</h2>
        <label>
          {t('status')}
          <select value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">{t('all')}</option>
            {deliveryStatuses.map((value) => (
              <option key={value} value={value}>
                {t(value)}
              </option>
            ))}
          </select>
        </label>
      </div>
      <FeedbackBox value={errorFeedback(query.error)} onRetry={() => void query.refetch()} />
      <DataTable
        rows={query.data || []}
        pending={query.isPending}
        columns={[
          {
            label: 'maskedRecipient',
            render: (row) => (
              <div className="primary-cell">
                <strong>{row.maskedRecipient || t('none')}</strong>
                <small>{row.kind}</small>
              </div>
            ),
          },
          {
            label: 'status',
            render: (row) => <span className={`badge badge-${row.status}`}>{t(row.status)}</span>,
          },
          { label: 'createdAtUtc', render: (row) => formatDate(row.createdAtUtc, language) },
          { label: 'attempts', render: (row) => row.attempts.length },
          {
            label: 'details',
            render: (row) => (
              <Button variant="ghost" size="sm" onClick={() => setSelected(row.id)}>
                {t('details')}
              </Button>
            ),
          },
        ]}
      />
      {selected && (
        <Dialog title={t('details')} onClose={() => setSelected('')}>
          <FeedbackBox value={errorFeedback(detail.error)} onRetry={() => void detail.refetch()} />
          {detail.isPending && <p role="status">{t('loading')}</p>}
          {detail.data && (
            <>
              <dl className="detail-grid">
                {(
                  [
                    'id',
                    'maskedRecipient',
                    'provider',
                    'templateVersion',
                    'culture',
                    'correlationId',
                    'outcomeCode',
                  ] as const
                ).map((key) => (
                  <div key={key}>
                    <dt>{t(key)}</dt>
                    <dd>{detail.data![key] || t('none')}</dd>
                  </div>
                ))}
              </dl>
              <h3>{t('attempts')}</h3>
              {detail.data.attempts.length === 0 && <p>{t('empty')}</p>}
              {detail.data.attempts.map((attempt) => (
                <div className="attempt" key={attempt.number}>
                  <strong>
                    {t('number')} {attempt.number} · {attempt.outcome}
                  </strong>
                  <span>{formatDate(attempt.startedAtUtc, language)}</span>
                  <span>
                    {t('durationMilliseconds')}: {attempt.durationMilliseconds ?? t('none')}
                  </span>
                </div>
              ))}
            </>
          )}
        </Dialog>
      )}
    </>
  );
}
