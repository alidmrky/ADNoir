import type {
  AuthTokensDto,
  LoginRequest,
  RegisterRequest,
  ChangePasswordRequest,
} from '../../contracts/generated/identity';
import type { Claims } from '../../contracts/claims';
import { request } from '../http/client';
import { ApiError } from '../http/api-error';
import { tokenStore } from './token-store';
import { decodeClaims } from './claims';

let pendingRefresh: { epoch: number; promise: Promise<void> } | undefined;
let epoch = 0;
let identity: string | undefined;
let callbacks: {
  onSession: (claims: Claims, mustChangePassword: boolean) => void;
  onClear: () => void;
  expiredMessage: () => string;
};
export function configureAuth(value: typeof callbacks) {
  callbacks = value;
}
export const sessionGeneration = () => epoch;
export function forceSignOut() {
  epoch++;
  identity = undefined;
  pendingRefresh = undefined;
  tokenStore.clear();
  callbacks.onClear();
}
function accept(tokens: AuthTokensDto) {
  const claims = decodeClaims(tokens.accessToken);
  const nextIdentity = JSON.stringify([claims.sub, claims.tenant_id]);
  // Another tab may have signed into a different account using the shared refresh key.
  if (identity && identity !== nextIdentity) epoch++;
  identity = nextIdentity;
  tokenStore.write(tokens.accessToken, tokens.refreshToken);
  callbacks.onSession(claims, tokens.mustChangePassword);
}

export async function signIn(payload: LoginRequest) {
  forceSignOut();
  const captured = epoch;
  const tokens = await request<AuthTokensDto>('/api/v1/auth/login', {
    method: 'POST',
    body: payload,
    anonymous: true,
  });
  if (captured === epoch) accept(tokens);
}
export async function registerAccount(payload: RegisterRequest) {
  await request('/api/v1/auth/register', { method: 'POST', body: payload, anonymous: true });
}
export function refreshSession(): Promise<void> {
  if (pendingRefresh?.epoch === epoch) return pendingRefresh.promise;
  const captured = epoch;
  const renew = async () => {
    if (captured !== epoch) return;
    const refreshToken = tokenStore.refresh();
    if (!refreshToken)
      throw new ApiError('unauthorized', 'ui.session.expired', callbacks.expiredMessage());
    const tokens = await request<AuthTokensDto>('/api/v1/auth/refresh', {
      method: 'POST',
      body: { refreshToken },
      anonymous: true,
    });
    if (captured === epoch) accept(tokens);
  };
  // Web Locks serializes rotation across tabs; the token is read only after acquiring the lock.
  const promise = (async () => {
    if (navigator.locks) await navigator.locks.request('adnoir.refresh', renew);
    else await renew();
  })()
    .catch((error: unknown) => {
      if (captured === epoch) forceSignOut();
      throw error;
    })
    .finally(() => {
      if (pendingRefresh?.promise === promise) pendingRefresh = undefined;
    });
  pendingRefresh = { epoch: captured, promise };
  return promise;
}
export async function restoreSession() {
  if (!tokenStore.refresh()) {
    callbacks.onClear();
    return;
  }
  try {
    await refreshSession();
  } catch {
    /* Refresh already cleared the session. */
  }
}
export async function signOut(allSessions = false) {
  const captured = epoch;
  // Finish an in-flight rotation before revoking its successor.
  try {
    await pendingRefresh?.promise;
    const revoke = async () => {
      if (captured !== epoch) return;
      const refreshToken = tokenStore.refresh();
      if (refreshToken)
        await request('/api/v1/auth/logout', {
          method: 'POST',
          body: { refreshToken, allSessions },
          anonymous: true,
        });
    };
    if (navigator.locks) await navigator.locks.request('adnoir.refresh', revoke);
    else await revoke();
  } finally {
    if (captured === epoch) forceSignOut();
  }
}
export async function changePassword(payload: ChangePasswordRequest) {
  await request('/api/v1/auth/change-password', { method: 'POST', body: payload });
  forceSignOut();
}
