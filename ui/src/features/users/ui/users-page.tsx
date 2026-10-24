import { useState } from 'react';
import { Plus, RefreshCw } from 'lucide-react';
import { useUsers, getUser, saveUser } from '../api/queries';
import { userKeys } from '../api/keys';
import { createFields, resetPasswordFields, mapUserField } from '../model/fields';
import { AssignmentForm } from './assignment-form';
import { useEditor } from '../../../lib/query/use-editor';
import { errorFeedback } from '../../../lib/query/feedback';
import { useTranslation } from '../../../lib/i18n';
import { formatDate } from '../../../lib/format';
import { PageHeading } from '../../../components/common/page-heading';
import { DataTable } from '../../../components/common/table';
import { RecordForm } from '../../../components/common/record-form';
import { Dialog } from '../../../components/common/dialog';
import { FeedbackBox } from '../../../components/common/feedback';
import { Button } from '../../../components/ui/button';
export function UsersPage() {
  const { t, language } = useTranslation();
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(20);
  const query = useUsers(page, size, language);
  const edit = useEditor({
    rootKey: userKeys.all,
    load: getUser,
    save: saveUser,
    mapFields: mapUserField,
  });
  const busy = edit.busy || edit.feedback?.conflict === true;
  return (
    <>
      <PageHeading name="users">
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
            label: 'displayName',
            render: (row) => (
              <div className="user-cell">
                <span className="avatar">
                  {row.displayName.slice(0, 2).toLocaleUpperCase(language)}
                </span>
                <div className="primary-cell">
                  <strong>{row.displayName}</strong>
                  <small>{row.email}</small>
                </div>
              </div>
            ),
          },
          {
            label: 'roles',
            render: (row) => (
              <div className="permission-tags">
                {row.roles.map((role) => (
                  <span className="badge" key={role.roleId}>
                    {role.roleCode}
                  </span>
                ))}
              </div>
            ),
          },
          {
            label: 'status',
            render: (row) => (
              <span className={`badge ${row.isActive ? 'badge-active' : ''}`}>
                {t(row.isActive ? 'active' : 'inactive')}
              </span>
            ),
          },
          { label: 'lastLoginAtUtc', render: (row) => formatDate(row.lastLoginAtUtc, language) },
          {
            label: 'details',
            render: (row) => (
              <div className="row-actions">
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={edit.opening}
                  onClick={() => void edit.open('assignRoles', row.id)}
                >
                  {t('assignRoles')}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={edit.opening}
                  onClick={() => void edit.open('resetPassword', row.id)}
                >
                  {t('resetPassword')}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={edit.opening}
                  onClick={() => void edit.open(row.isActive ? 'deactivate' : 'activate', row.id)}
                >
                  {t(row.isActive ? 'deactivate' : 'activate')}
                </Button>
              </div>
            ),
          },
        ]}
      />
      {edit.editor && (
        <Dialog title={t(edit.editor.action)} onClose={edit.close} busy={edit.busy}>
          <FeedbackBox
            value={edit.feedback}
            onRetry={edit.feedback?.conflict ? edit.reload : undefined}
          />
          {!edit.editor.record ? (
            <RecordForm
              fields={createFields}
              initial={{ mustChangePassword: true }}
              onSubmit={edit.submit}
              busy={busy}
              fieldErrors={edit.fieldErrors}
            />
          ) : edit.editor.action === 'assignRoles' ? (
            <AssignmentForm
              key={edit.editor.revision}
              user={edit.editor.record}
              onSubmit={edit.submit}
              busy={busy}
              fieldErrors={edit.fieldErrors}
            />
          ) : edit.editor.action === 'resetPassword' ? (
            <>
              <p>{edit.editor.record.displayName}</p>
              <p>{t('resetPasswordDescription')}</p>
              <RecordForm
                key={edit.editor.revision}
                fields={resetPasswordFields}
                onSubmit={edit.submit}
                busy={busy}
                fieldErrors={edit.fieldErrors}
              />
            </>
          ) : (
            <>
              <p>{edit.editor.record.displayName}</p>
              <p>{t('activationConfirm')}</p>
              <Button disabled={busy} onClick={() => void edit.submit({})}>
                {t(edit.editor.action)}
              </Button>
            </>
          )}
        </Dialog>
      )}
    </>
  );
}
