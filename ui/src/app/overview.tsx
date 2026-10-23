import { ArrowUpRight, Building2, Users, ShieldCheck, Mail, Network } from 'lucide-react';
import { useTranslation } from '../lib/i18n';
import { useSession } from '../stores/session-store';
import { Button } from '../components/ui/button';
export function Overview() {
  const { t } = useTranslation();
  const claims = useSession((session) => session.claims);
  return (
    <>
      <header className="page-heading">
        <div>
          <span className="eyebrow">{t('overview')}</span>
          <h1>{t('welcome')}</h1>
          <p>{t('overviewDescription')}</p>
        </div>
      </header>
      <section className="welcome-banner">
        <div>
          <span className="eyebrow">{t('workspaceLabel')}</span>
          <h2>{t('workspaceTitle')}</h2>
          <p>{t('workspaceDescription')}</p>
          <Button asChild>
            <a href="#/org-units">
              {t('openOrganization')}
              <ArrowUpRight size={17} />
            </a>
          </Button>
        </div>
        <div className="organization-art" aria-hidden="true">
          <span className="org-node org-top">
            <Building2 />
          </span>
          <i />
          <div>
            <span className="org-node">
              <Users />
            </span>
            <span className="org-node">
              <Network />
            </span>
            <span className="org-node">
              <ShieldCheck />
            </span>
          </div>
        </div>
      </section>
      <section className="quick-section">
        <h2>{t('quickAccess')}</h2>
        <div className="quick-grid">
          {[
            { name: 'users', Icon: Users },
            { name: 'org-units', Icon: Network },
            { name: 'roles', Icon: ShieldCheck },
            { name: 'notifications', Icon: Mail },
          ].map(({ name, Icon }) => (
            <a className="quick-card" key={name} href={`#/${name}`}>
              <span className="quick-icon">
                <Icon size={22} />
              </span>
              <ArrowUpRight className="quick-arrow" size={18} />
              <h3>{t(name)}</h3>
              <p>{t(`${name}Description`)}</p>
            </a>
          ))}
        </div>
      </section>
      <section className="panel session-panel">
        <h2>
          {t('sessionDetails')}
          <span className="badge badge-active">{t('active')}</span>
        </h2>
        <dl className="detail-grid">
          <div>
            <dt>{t('tenantIdentity')}</dt>
            <dd>
              <code>{claims.tenant_id || t('none')}</code>
            </dd>
          </div>
          <div>
            <dt>{t('readScope')}</dt>
            <dd>{claims.read_scope || t('none')}</dd>
          </div>
          <div>
            <dt>{t('writeScope')}</dt>
            <dd>{claims.write_scope || t('none')}</dd>
          </div>
          <div>
            <dt>{t('hierarchyPath')}</dt>
            <dd>{claims.hierarchy_path || t('none')}</dd>
          </div>
        </dl>
        <p className="helper-text">{t('sessionNote')}</p>
      </section>
    </>
  );
}
