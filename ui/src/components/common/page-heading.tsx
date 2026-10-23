import type { ReactNode } from 'react';
import { useTranslation } from '../../lib/i18n';
export function PageHeading({ name, children }: { name: string; children?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <header className="page-heading">
      <div>
        <span className="eyebrow">{t('workspace')}</span>
        <h1>{t(name)}</h1>
        <p>{t(`${name}Description`)}</p>
      </div>
      <div className="heading-actions">{children}</div>
    </header>
  );
}
