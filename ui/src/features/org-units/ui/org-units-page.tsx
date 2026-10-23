import { useState } from 'react';
import { Plus, RefreshCw, GitBranch } from 'lucide-react';
import { useOrgUnits, getOrgUnit, saveOrgUnit } from '../api/queries';
import { orgKeys } from '../api/keys';
import { createFields, editFields, moveFields, mapOrgField } from '../model/fields';
import { useEditor } from '../../../lib/query/use-editor';
import { errorFeedback } from '../../../lib/query/feedback';
import { useTranslation } from '../../../lib/i18n';
import { PageHeading } from '../../../components/common/page-heading';
import { DataTable } from '../../../components/common/table';
import { EditorDialog } from '../../../components/common/editor-dialog';
import { FeedbackBox } from '../../../components/common/feedback';
import { Button } from '../../../components/ui/button';
export function OrgUnitsPage() {
  const { t, language } = useTranslation();
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(20);
  const query = useOrgUnits(page, size, language);
  const edit = useEditor({
    rootKey: orgKeys.all,
    load: getOrgUnit,
    save: saveOrgUnit,
    mapFields: mapOrgField,
  });
  return (
    <>
      <PageHeading name="org-units">
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
                <strong>
                  <GitBranch size={16} />
                  {row.name}
                </strong>
                <small>{row.hierarchyPath}</small>
              </div>
            ),
          },
          { label: 'code', render: (row) => row.code || t('none') },
          { label: 'id', render: (row) => <code>{row.id}</code> },
          {
            label: 'status',
            render: (row) => (
              <span className={`badge ${row.isActive ? 'badge-active' : ''}`}>
                {t(row.isActive ? 'active' : 'inactive')}
              </span>
            ),
          },
          {
            label: 'details',
            render: (row) => (
              <div className="row-actions">
                {['edit', 'move', 'delete'].map((action) => (
                  <Button
                    key={action}
                    variant="ghost"
                    size="sm"
                    disabled={edit.opening}
                    onClick={() => void edit.open(action, row.id)}
                  >
                    {t(action)}
                  </Button>
                ))}
              </div>
            ),
          },
        ]}
      />
      {edit.editor && (
        <EditorDialog
          action={edit.editor.action}
          fields={
            edit.editor.action === 'move'
              ? moveFields
              : edit.editor.record
                ? editFields
                : createFields
          }
          initial={{ ...edit.editor.record, newParentId: edit.editor.record?.parentId || '' }}
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
