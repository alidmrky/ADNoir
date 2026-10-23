import { useState } from 'react';
import { Plus, RefreshCw } from 'lucide-react';
import { useTenants, getTenant, saveTenant } from '../api/queries';
import { tenantKeys } from '../api/keys';
import { createFields, editFields, tenantInitial, mapTenantField } from '../model/fields';
import { useEditor } from '../../../lib/query/use-editor';
import { errorFeedback } from '../../../lib/query/feedback';
import { useTranslation } from '../../../lib/i18n';
import { formatDate } from '../../../lib/format';
import { PageHeading } from '../../../components/common/page-heading';
import { DataTable } from '../../../components/common/table';
import { EditorDialog } from '../../../components/common/editor-dialog';
import { FeedbackBox } from '../../../components/common/feedback';
import { Button } from '../../../components/ui/button';

export function TenantsPage() {
  const { t, language } = useTranslation();
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(20);
  const query = useTenants(page, size, language);
  const edit = useEditor({
    rootKey: tenantKeys.all,
    load: getTenant,
    save: saveTenant,
    mapFields: mapTenantField,
  });
  return (
    <>
      <PageHeading name="tenants">
        <Button variant="outline" onClick={() => void query.refetch()} disabled={query.isFetching}>
          <RefreshCw size={16} />
          {t('refresh')}
        </Button>
        <Button disabled={edit.opening} onClick={() => void edit.open('create')}>
          <Plus size={17} />
          {t('create')}
        </Button>
      </PageHeading>
      <FeedbackBox value={errorFeedback(query.error)} onRetry={() => void query.refetch()} />
      {!edit.editor && <FeedbackBox value={edit.feedback} />}
      <DataTable
        rows={query.data?.items || []}
        page={query.data}
        pending={query.isPending}
        onPage={setPage}
        onSize={(value) => {
          setSize(value);
          setPage(1);
        }}
        columns={[
          {
            label: 'name',
            render: (row) => (
              <div className="primary-cell">
                <strong>{row.name}</strong>
                <small>{row.slug}</small>
              </div>
            ),
          },
          {
            label: 'status',
            render: (row) => <span className={`badge badge-${row.status}`}>{t(row.status)}</span>,
          },
          { label: 'timeZoneId', render: (row) => row.timeZoneId },
          { label: 'createdAtUtc', render: (row) => formatDate(row.createdAtUtc, language) },
          {
            label: 'details',
            render: (row) => (
              <Button
                variant="ghost"
                size="sm"
                disabled={edit.opening}
                onClick={() => void edit.open('edit', row.id)}
              >
                {t('edit')}
              </Button>
            ),
          },
        ]}
      />
      {edit.editor && (
        <EditorDialog
          action={edit.editor.action}
          fields={edit.editor.record ? editFields : createFields}
          initial={tenantInitial(edit.editor.record)}
          revision={edit.editor.revision}
          busy={edit.busy}
          feedback={edit.feedback}
          fieldErrors={edit.fieldErrors}
          onClose={edit.close}
          onSubmit={edit.submit}
          onReload={edit.reload}
        />
      )}
    </>
  );
}
