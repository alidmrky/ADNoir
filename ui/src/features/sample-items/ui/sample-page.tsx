import { useState } from 'react';
import { useSample, useCreateSample } from '../api/queries';
import { sampleFields, lookupFields, mapSampleField } from '../model/fields';
import { errorFeedback } from '../../../lib/query/feedback';
import { useTranslation } from '../../../lib/i18n';
import { formatDate } from '../../../lib/format';
import { PageHeading } from '../../../components/common/page-heading';
import { RecordForm } from '../../../components/common/record-form';
import { FeedbackBox } from '../../../components/common/feedback';
export function SamplePage() {
  const { t, language } = useTranslation();
  const [id, setId] = useState('');
  const query = useSample(id, language);
  const create = useCreateSample(language, setId);
  const feedback = errorFeedback(create.error);
  const fieldErrors: Record<string, string> = {};
  for (const error of feedback?.errors || []) {
    const field = error.field && mapSampleField(error.field);
    if (field) fieldErrors[field] = error.message || feedback!.message;
  }
  return (
    <>
      <PageHeading name="sample-items" />
      <div className="two-column">
        <section className="panel">
          <h2>{t('create')}</h2>
          <FeedbackBox value={feedback} />
          <RecordForm
            fields={sampleFields}
            fieldErrors={fieldErrors}
            busy={create.isPending}
            onSubmit={async (values) => {
              try {
                await create.mutateAsync({
                  title: String(values.title),
                  ...(values.description ? { description: String(values.description) } : {}),
                });
              } catch {
                /* Display mutation feedback. */
              }
            }}
          />
        </section>
        <section className="panel">
          <h2>{t('find')}</h2>
          <RecordForm
            fields={lookupFields}
            submitLabel="find"
            onSubmit={async (values) => {
              const next = String(values.id);
              if (next === id) await query.refetch();
              else setId(next);
            }}
          />
          <FeedbackBox value={errorFeedback(query.error)} onRetry={() => void query.refetch()} />
          {id && query.isPending && <p role="status">{t('loading')}</p>}
          {query.data && (
            <div className="sample-result">
              <h3>{query.data.title}</h3>
              <p>{query.data.description}</p>
              <code>{query.data.id}</code>
              <small>{formatDate(query.data.createdAtUtc, language)}</small>
            </div>
          )}
        </section>
      </div>
    </>
  );
}
