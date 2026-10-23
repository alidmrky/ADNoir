import { navigate } from './router';
import { configureHttp } from '../lib/http/client';
import {
  configureAuth,
  forceSignOut,
  refreshSession,
  sessionGeneration,
} from '../lib/auth/session';
import { tokenStore } from '../lib/auth/token-store';
import { queryClient } from '../lib/query/client';
import { getLanguage, subscribeLanguage, t } from '../lib/i18n';
import { useSession } from '../stores/session-store';

export function configureApplication() {
  configureAuth({
    onSession: (claims, mustChangePassword) => {
      const previous = useSession.getState().claims;
      if (previous.sub !== claims.sub || previous.tenant_id !== claims.tenant_id)
        queryClient.clear();
      useSession.setState({ state: 'authenticated', claims, mustChangePassword });
    },
    onClear: () => {
      queryClient.clear();
      useSession.setState({ state: 'anonymous', claims: {}, mustChangePassword: false });
      navigate('/');
    },
    expiredMessage: () => t('authExpired'),
  });
  configureHttp({
    getAccessToken: tokenStore.access,
    getSessionGeneration: sessionGeneration,
    getLanguage,
    getNetworkMessage: () => t('networkError'),
    onUnauthorized: refreshSession,
    onSignOut: forceSignOut,
    onPasswordRequired: () => {
      useSession.setState({ mustChangePassword: true });
      navigate('/change-password');
    },
    onWarning: (feedback) =>
      window.dispatchEvent(new CustomEvent('adnoir:warning', { detail: feedback })),
  });
  // Rotation invalidates the memory token without starting a cross-tab refresh loop.
  const unsubscribeTokens = tokenStore.onExternalChange(() => {
    if (!tokenStore.refresh()) forceSignOut();
  });
  const unsubscribeLanguage = subscribeLanguage(() => {
    void queryClient.invalidateQueries();
  });
  return () => {
    unsubscribeTokens();
    unsubscribeLanguage();
  };
}
