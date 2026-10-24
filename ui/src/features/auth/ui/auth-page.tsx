import { useState } from 'react';
import { ArrowUpRight, ShieldCheck } from 'lucide-react';
import type {
  LoginRequest,
  RegisterRequest,
  ChangePasswordRequest,
} from '../../../contracts/generated/identity';
import { signIn, registerAccount, changePassword } from '../../../lib/auth/session';
import { loginFields, registerFields, passwordFields, mapAuthField } from '../model/fields';
import { useTranslation } from '../../../lib/i18n';
import { errorFeedback } from '../../../lib/query/feedback';
import { RecordForm } from '../../../components/common/record-form';
import { FeedbackBox } from '../../../components/common/feedback';
import { Button } from '../../../components/ui/button';

export function AuthPage({
  passwordChange = false,
  requiredChange = false,
}: {
  passwordChange?: boolean;
  requiredChange?: boolean;
}) {
  const { t, language, setLanguage } = useTranslation();
  const [registration, setRegistration] = useState(false);
  const [forgot, setForgot] = useState(false);
  const [error, setError] = useState<unknown>();
  const [success, setSuccess] = useState(false);
  const [busy, setBusy] = useState(false);
  const feedback = errorFeedback(error);
  const fieldErrors: Record<string, string> = {};
  for (const item of feedback?.errors || []) {
    const field = item.field && mapAuthField(item.field);
    if (field) fieldErrors[field] = item.message || feedback!.message;
  }
  const mode = passwordChange ? 'changePassword' : registration ? 'register' : 'login';
  async function submit(values: Record<string, unknown>) {
    setBusy(true);
    setError(undefined);
    setSuccess(false);
    try {
      if (passwordChange) await changePassword(values as unknown as ChangePasswordRequest);
      else if (registration) {
        await registerAccount(values as unknown as RegisterRequest);
        setSuccess(true);
        setRegistration(false);
      } else await signIn(values as unknown as LoginRequest);
    } catch (cause) {
      setError(cause);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="auth-layout">
      <aside className="auth-story">
        <a className="brand" href="#/">
          <span className="brand-symbol" aria-hidden="true">
            {t('brandSymbol')}
          </span>
          {t('brand')}
          <span className="brand-dot" />
        </a>
        <div className="auth-story-content">
          <span className="eyebrow">{t('authEyebrow')}</span>
          <h1>{t('authTitle')}</h1>
          <p>{t('authDescription')}</p>
          <div className="orbital-art" aria-hidden="true">
            <div />
            <div />
            <div />
            <span />
            <span />
            <span />
          </div>
        </div>
        <footer>
          <ShieldCheck size={17} />
          {t('authFooter')}
        </footer>
      </aside>
      <main className="auth-main">
        <div className="auth-language">
          <label htmlFor="auth-language">{t('language')}</label>
          <select
            id="auth-language"
            value={language}
            onChange={(event) => setLanguage(event.target.value as 'tr' | 'en')}
          >
            <option value="tr">{t('tr')}</option>
            <option value="en">{t('en')}</option>
          </select>
        </div>
        <div className="auth-form">
          <span className="auth-mark">
            <ArrowUpRight size={25} />
          </span>
          <h2>
            {t(
              passwordChange
                ? 'changePassword'
                : forgot
                  ? 'forgotPasswordTitle'
                  : registration
                    ? 'register'
                    : 'loginTitle',
            )}
          </h2>
          <p>
            {t(
              passwordChange
                ? 'changePasswordDescription'
                : forgot
                  ? 'forgotPasswordDescription'
                  : registration
                    ? 'registerDescription'
                    : 'loginDescription',
            )}
          </p>
          {requiredChange && <p className="password-notice">{t('passwordRequired')}</p>}
          {success && (
            <p className="feedback feedback-success" role="status">
              {t('registerSuccess')}
            </p>
          )}
          <FeedbackBox value={feedback} />
          {!forgot && (
            <RecordForm
              key={mode}
              fields={passwordChange ? passwordFields : registration ? registerFields : loginFields}
              onSubmit={submit}
              busy={busy}
              submitLabel={mode}
              fieldErrors={fieldErrors}
            />
          )}
          {!passwordChange && !registration && !forgot && (
            <Button
              className="auth-switch"
              variant="ghost"
              disabled={busy}
              onClick={() => {
                setForgot(true);
                setError(undefined);
                setSuccess(false);
              }}
            >
              {t('forgotPassword')}
            </Button>
          )}
          {!passwordChange && !forgot && (
            <Button
              className="auth-switch"
              variant="ghost"
              disabled={busy}
              onClick={() => {
                setRegistration(!registration);
                setError(undefined);
                setSuccess(false);
              }}
            >
              {t(registration ? 'login' : 'register')}
            </Button>
          )}
          {forgot && (
            <Button className="auth-switch" variant="ghost" onClick={() => setForgot(false)}>
              {t('backToLogin')}
            </Button>
          )}
        </div>
      </main>
    </div>
  );
}
