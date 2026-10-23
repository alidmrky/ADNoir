import type { ReactNode } from 'react';
import type { Page } from '../../contracts/page';
import { useTranslation } from '../../lib/i18n';
import { Button } from '../ui/button';
export interface Column<T> {
  label: string;
  render: (record: T) => ReactNode;
}
export function DataTable<T extends { id: string }>({
  rows,
  columns,
  pending,
  page,
  onPage,
  onSize,
}: {
  rows: T[];
  columns: Column<T>[];
  pending?: boolean;
  page?: Page<T> | undefined;
  onPage?: (page: number) => void;
  onSize?: (size: number) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="table-card" aria-busy={pending}>
      {pending ? (
        <div className="empty-state" role="status">
          <span className="spinner" />
          {t('loading')}
        </div>
      ) : rows.length === 0 ? (
        <div className="empty-state">
          <span className="empty-icon">◇</span>
          <h3>{t('empty')}</h3>
          <p>{t('emptyDescription')}</p>
        </div>
      ) : (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                {columns.map((column, index) => (
                  <th key={index}>{t(column.label)}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id}>
                  {columns.map((column, index) => (
                    <td key={index}>{column.render(row)}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {page && (
        <div className="pagination">
          <span>
            {t('pagination', {
              page: page.pageNumber,
              pages: Math.max(page.totalPages, 1),
              total: page.totalCount,
            })}
          </span>
          <label>
            {t('pageSize')}
            <select
              value={page.pageSize}
              onChange={(event) => onSize?.(Number(event.target.value))}
            >
              {[10, 20, 50].map((size) => (
                <option key={size}>{size}</option>
              ))}
            </select>
          </label>
          <Button
            variant="outline"
            size="sm"
            disabled={page.pageNumber <= 1 || pending}
            onClick={() => onPage?.(page.pageNumber - 1)}
          >
            {t('previous')}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={page.pageNumber >= page.totalPages || pending}
            onClick={() => onPage?.(page.pageNumber + 1)}
          >
            {t('next')}
          </Button>
        </div>
      )}
    </div>
  );
}
