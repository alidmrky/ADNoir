import { Plus, RefreshCw } from 'lucide-react';
import { useRoles, getRole, saveRole } from '../api/queries';
import { roleKeys } from '../api/keys';
import { createFields, editFields, mapRoleField } from '../model/fields';
import { useEditor } from '../../../lib/query/use-editor';
import { errorFeedback } from '../../../lib/query/feedback';
import { useTranslation } from '../../../lib/i18n';
import { PageHeading } from '../../../components/common/page-heading';
import { DataTable } from '../../../components/common/table';
import { EditorDialog } from '../../../components/common/editor-dialog';
import { FeedbackBox } from '../../../components/common/feedback';
import { Button } from '../../../components/ui/button';
export function RolesPage() {
  const { t, language } = useTranslation();
  const query = useRoles(language);
  const edit = useEditor({
    rootKey: roleKeys.all,
    load: getRole,
    save: saveRole,
    mapFields: mapRoleField,
  });
  return (
    <>
      <PageHeading name="roles">
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
        rows={query.data || []}
        pending={query.isPending}
        columns={[
          {
            label: 'name',
            render: (row) => (
              <div className="primary-cell">
                <strong>{row.name}</strong>
                <small>{row.code}</small>
                <code>{row.id}</code>
              </div>
            ),
          },
          {
            label: 'permissions',
            render: (row) => (
              <div className="permission-tags">
                {row.permissions.map((permission) => (
                  <span className="badge" key={permission}>
                    {permission}
                  </span>
                ))}
              </div>
            ),
          },
          {
            label: 'kind',
            render: (row) => (
              <span className="badge">{t(row.isSystemRole ? 'system' : 'custom')}</span>
            ),
          },
          {
            label: 'details',
            render: (row) => (
              <div className="row-actions">
                {['edit', 'delete'].map((action) => (
                  <Button
                    key={action}
                    variant="ghost"
                    size="sm"
                    disabled={row.isSystemRole || edit.opening}
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
          fields={edit.editor.record ? editFields : createFields}
          initial={{ ...edit.editor.record }}
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
