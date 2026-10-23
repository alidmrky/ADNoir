import { useEffect, useState } from 'react';
import {
  LayoutDashboard,
  Building2,
  Network,
  Users,
  ShieldCheck,
  Mail,
  Layers,
  LogOut,
  Menu,
  ChevronRight,
  KeyRound,
} from 'lucide-react';
import type { Feedback } from '../contracts/feedback';
import { AuthPage, signOut } from '../features/auth';
import { TenantsPage } from '../features/tenants';
import { OrgUnitsPage } from '../features/org-units';
import { UsersPage } from '../features/users';
import { RolesPage } from '../features/roles';
import { NotificationsPage } from '../features/notifications';
import { SamplePage } from '../features/sample-items';
import { useSession } from '../stores/session-store';
import { useTranslation } from '../lib/i18n';
import { restoreSession } from '../lib/auth/session';
import { Button } from '../components/ui/button';
import { FeedbackBox } from '../components/common/feedback';
import { useRoute } from './router';
import { Overview } from './overview';
const navigation = [
  { name: 'overview', path: '/', Icon: LayoutDashboard },
  { name: 'tenants', path: '/tenants', Icon: Building2 },
  { name: 'org-units', path: '/org-units', Icon: Network },
  { name: 'users', path: '/users', Icon: Users },
  { name: 'roles', path: '/roles', Icon: ShieldCheck },
  { name: 'notifications', path: '/notifications', Icon: Mail },
  { name: 'sample-items', path: '/sample-items', Icon: Layers },
];
export function App() {
  const { t, language, setLanguage } = useTranslation();
  const session = useSession();
  const path = useRoute();
  const [sidebar, setSidebar] = useState(false);
  const [warnings, setWarnings] = useState<Feedback[]>([]);
  const [loggingOut, setLoggingOut] = useState(false);
  useEffect(() => {
    const listener = (event: Event) => {
      setWarnings((items) => [...items, (event as CustomEvent<Feedback>).detail]);
    };
    window.addEventListener('adnoir:warning', listener);
    void restoreSession();
    return () => window.removeEventListener('adnoir:warning', listener);
  }, []);
  useEffect(() => {
    if (session.state === 'anonymous') setWarnings([]);
  }, [session.state]);
  useEffect(() => {
    setWarnings([]);
  }, [language]);
  async function logout(all = false) {
    setLoggingOut(true);
    try {
      await signOut(all);
    } catch {
      /* Local cleanup is guaranteed by signOut. */
    } finally {
      setLoggingOut(false);
    }
  }
  const warningContent = warnings.map((warning, index) => (
    <div className="warning-item" key={`${warning.correlationId}-${index}`}>
      <FeedbackBox value={warning} />
      <Button
        variant="ghost"
        onClick={() => setWarnings((items) => items.filter((_, i) => i !== index))}
      >
        {t('close')}
      </Button>
    </div>
  ));
  if (session.state === 'loading')
    return (
      <div className="boot-screen" role="status">
        <span className="spinner" />
        {t('loading')}
      </div>
    );
  if (session.state === 'anonymous')
    return (
      <>
        {warningContent}
        <AuthPage key={language} />
      </>
    );
  if (session.mustChangePassword)
    return (
      <>
        {warningContent}
        <div className="forced-logout">
          <Button variant="outline" disabled={loggingOut} onClick={() => void logout()}>
            {t('logout')}
          </Button>
        </div>
        <AuthPage key={language} passwordChange requiredChange />
      </>
    );
  const current = navigation.find((item) => item.path === path);
  return (
    <div className="app-shell">
      <a
        className="skip-link"
        href="#main-content"
        onClick={(event) => {
          event.preventDefault();
          document.getElementById('main-content')?.focus();
        }}
      >
        {t('skip')}
      </a>
      {sidebar && (
        <button
          className="sidebar-backdrop"
          aria-label={t('close')}
          onClick={() => setSidebar(false)}
        />
      )}
      <aside className={`sidebar ${sidebar ? 'sidebar-open' : ''}`}>
        <a className="brand" href="#/">
          <span className="brand-symbol" aria-hidden="true">
            {t('brandSymbol')}
          </span>
          {t('brand')}
          <span className="brand-dot" />
        </a>
        <div className="workspace-badge">
          <span className="workspace-avatar">
            <Building2 size={19} />
          </span>
          <div>
            <strong>{t('workspace')}</strong>
            <small>{session.claims.tenant_id?.slice(0, 18) || t('none')}</small>
          </div>
        </div>
        <nav aria-label={t('management')}>
          {navigation.map(({ name, path: target, Icon }, index) => (
            <div key={name}>
              {index === 1 && <span className="nav-section">{t('management')}</span>}
              {index === 5 && <span className="nav-section">{t('operations')}</span>}
              <a
                href={`#${target}`}
                className={path === target ? 'nav-link active' : 'nav-link'}
                aria-current={path === target ? 'page' : undefined}
                onClick={() => setSidebar(false)}
              >
                <Icon size={19} />
                {t(name)}
                {path === target && <span className="nav-dot" />}
              </a>
            </div>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <a className="nav-link" href="#/change-password">
            <KeyRound size={18} />
            {t('changePassword')}
          </a>
          <Button variant="ghost" disabled={loggingOut} onClick={() => void logout()}>
            <LogOut size={18} />
            {t('logout')}
          </Button>
          <Button variant="ghost" size="sm" disabled={loggingOut} onClick={() => void logout(true)}>
            {t('logoutAll')}
          </Button>
        </div>
      </aside>
      <div className="main-shell">
        <header className="topbar">
          <div className="breadcrumb">
            <Button
              className="mobile-menu"
              variant="ghost"
              size="icon"
              aria-label={t('menu')}
              onClick={() => setSidebar(true)}
            >
              <Menu size={20} />
            </Button>
            <span>{t('workspace')}</span>
            <ChevronRight size={14} />
            <strong>
              {t(
                current?.name || (path === '/change-password' ? 'changePassword' : 'notFoundPage'),
              )}
            </strong>
          </div>
          <div className="topbar-account">
            <select
              aria-label={t('language')}
              value={language}
              onChange={(event) => setLanguage(event.target.value as 'tr' | 'en')}
            >
              <option value="tr">{t('tr')}</option>
              <option value="en">{t('en')}</option>
            </select>
            <span className="topbar-divider" />
            <span className="avatar">
              {(session.claims.name || session.claims.email || 'A')
                .slice(0, 2)
                .toLocaleUpperCase(language)}
            </span>
            <span className="account-name">{session.claims.name || session.claims.email}</span>
          </div>
        </header>
        <main
          id="main-content"
          tabIndex={-1}
          className="main-content"
          key={`${session.claims.sub}:${session.claims.tenant_id}:${path}:${language}`}
        >
          {warningContent}
          {path === '/' ? (
            <Overview />
          ) : path === '/tenants' ? (
            <TenantsPage />
          ) : path === '/org-units' ? (
            <OrgUnitsPage />
          ) : path === '/users' ? (
            <UsersPage />
          ) : path === '/roles' ? (
            <RolesPage />
          ) : path === '/notifications' ? (
            <NotificationsPage />
          ) : path === '/sample-items' ? (
            <SamplePage />
          ) : path === '/change-password' ? (
            <AuthPage passwordChange />
          ) : (
            <section className="empty-state">
              <h1>{t('notFoundPage')}</h1>
              <a href="#/">{t('backHome')}</a>
            </section>
          )}
        </main>
      </div>
    </div>
  );
}
