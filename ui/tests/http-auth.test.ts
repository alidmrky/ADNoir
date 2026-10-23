import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse, delay } from 'msw';
import { server } from '../mocks/server';
import { failure, success, tokens } from '../mocks/envelopes';
import { configureApplication } from '../src/app/bootstrap';
import { request } from '../src/lib/http/client';
import { ApiError } from '../src/lib/http/api-error';
import {
  signIn,
  registerAccount,
  signOut,
  forceSignOut,
  refreshSession,
  changePassword,
} from '../src/lib/auth/session';
import { tokenStore } from '../src/lib/auth/token-store';
import { queryClient, shouldRetry } from '../src/lib/query/client';
import { useSession } from '../src/stores/session-store';
import { setLanguage } from '../src/lib/i18n';
import { messageCodes } from '../src/contracts/message-codes';
import type { Page } from '../src/contracts/page';
const base = 'http://localhost:5070';
let cleanup: () => void;
beforeEach(() => {
  cleanup = configureApplication();
  forceSignOut();
  setLanguage('tr');
});
afterEach(() => {
  forceSignOut();
  cleanup();
});
async function login(subject = 'user-a', mustChangePassword = false) {
  server.use(
    http.post(`${base}/api/v1/auth/login`, () =>
      HttpResponse.json(success(tokens(subject, mustChangePassword))),
    ),
  );
  await signIn({
    tenantSlug: 'example',
    email: 'test@example.com',
    password: 'not-a-real-password',
  });
}

describe('wire envelope boundary', () => {
  it.each([
    ['notFound', 404],
    ['unexpectedError', 500],
    ['conflict', 409],
    ['concurrencyConflict', 409],
    ['externalServiceError', 502],
  ] as const)('preserves %s independently of HTTP status', async (status, httpStatus) => {
    server.use(
      http.get(`${base}/probe`, () => HttpResponse.json(failure(status), { status: httpStatus })),
    );
    const error = await request('/probe').catch((value: unknown) => value);
    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status,
      correlationId: 'server-reference',
      message: 'Sunucu hata açıklaması.',
    });
    expect(error).not.toHaveProperty('httpStatus');
  });
  it('accepts noContent carried by HTTP 200 and omitted data', async () => {
    server.use(
      http.get(`${base}/probe`, () =>
        HttpResponse.json(success(undefined, { status: 'noContent' })),
      ),
    );
    expect(await request('/probe')).toBeUndefined();
  });
  it.each([
    { id: 1 },
    { Status: 'Success', Data: [] },
    success([], { status: 0 }),
    success([], { isSuccess: false }),
    success([], { message: {} }),
  ])('rejects malformed or bare payloads', async (value) => {
    server.use(http.get(`${base}/probe`, () => HttpResponse.json(value)));
    await expect(request('/probe')).rejects.toMatchObject({ messageCode: 'ui.response.invalid' });
  });
  it('normalizes network failure and malformed JSON with a reference', async () => {
    server.use(http.get(`${base}/probe`, () => HttpResponse.error()));
    await expect(request('/probe')).rejects.toMatchObject({
      messageCode: 'ui.network.unavailable',
      correlationId: expect.any(String),
    });
    server.use(http.get(`${base}/probe`, () => new HttpResponse('<html>bad gateway</html>')));
    await expect(request('/probe')).rejects.toBeInstanceOf(ApiError);
  });
  it('unwraps pagination from typed top-level fields and forwards successful warnings', async () => {
    const warnings = vi.fn();
    window.addEventListener('adnoir:warning', warnings);
    server.use(
      http.get(`${base}/probe`, () =>
        HttpResponse.json(
          success([{ id: 'one' }], {
            pageNumber: 2,
            pageSize: 20,
            totalCount: 45,
            totalPages: 3,
            metadata: { totalCount: 999 },
            warnings: [{ code: 'common.partial', message: 'Bir kayıt atlandı.' }],
          }),
        ),
      ),
    );
    const page = await request<Page<{ id: string }>>('/probe', { paged: true });
    expect(page).toEqual({
      items: [{ id: 'one' }],
      pageNumber: 2,
      pageSize: 20,
      totalCount: 45,
      totalPages: 3,
    });
    expect(warnings).toHaveBeenCalledOnce();
    expect((warnings.mock.calls[0]![0] as CustomEvent).detail.message).toBe('Bir kayıt atlandı.');
    window.removeEventListener('adnoir:warning', warnings);
  });
  it('sets culture and authorization but never a tenant header', async () => {
    await login();
    setLanguage('en');
    server.use(
      http.get(`${base}/probe`, ({ request: incoming }) => {
        expect(incoming.headers.get('Accept-Language')).toBe('en');
        expect(incoming.headers.get('Authorization')).toBe(`Bearer ${tokens().accessToken}`);
        expect([...incoming.headers.keys()].some((key) => key.includes('tenant'))).toBe(false);
        return HttpResponse.json(success('ok'));
      }),
    );
    expect(await request('/probe')).toBe('ok');
  });
});

describe('session lifecycle', () => {
  it('five concurrent unauthorized requests produce exactly one refresh', async () => {
    await login();
    let refreshes = 0;
    let requests = 0;
    tokenStore.write('expired-access', 'old-refresh');
    server.use(
      http.post(`${base}/api/v1/auth/refresh`, async ({ request: incoming }) => {
        refreshes++;
        expect(await incoming.json()).toEqual({ refreshToken: 'old-refresh' });
        await delay(30);
        return HttpResponse.json(success(tokens()));
      }),
      http.get(`${base}/probe`, ({ request: incoming }) => {
        requests++;
        return incoming.headers.get('Authorization') === 'Bearer expired-access'
          ? HttpResponse.json(failure('unauthorized', 'auth.token_invalid'), { status: 401 })
          : HttpResponse.json(success('ok'));
      }),
    );
    expect(await Promise.all(Array.from({ length: 5 }, () => request('/probe')))).toEqual([
      'ok',
      'ok',
      'ok',
      'ok',
      'ok',
    ]);
    expect(refreshes).toBe(1);
    expect(requests).toBe(10);
    expect(tokenStore.refresh()).toBe(tokens().refreshToken);
  });
  it('a second unauthorized response logs out, clears caches and stops retrying', async () => {
    await login();
    let refreshes = 0;
    let requests = 0;
    queryClient.setQueryData(['private'], 'secret');
    server.use(
      http.post(`${base}/api/v1/auth/refresh`, () => {
        refreshes++;
        return HttpResponse.json(success(tokens()));
      }),
      http.get(`${base}/probe`, () => {
        requests++;
        return HttpResponse.json(failure('unauthorized'), { status: 401 });
      }),
    );
    await expect(request('/probe')).rejects.toMatchObject({ status: 'unauthorized' });
    expect(refreshes).toBe(1);
    expect(requests).toBe(2);
    expect(tokenStore.access()).toBeUndefined();
    expect(tokenStore.refresh()).toBeUndefined();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
    expect(useSession.getState().state).toBe('anonymous');
  });
  it.each(['login', 'register'] as const)(
    '%s failure never enters the refresh path',
    async (operation) => {
      let refreshes = 0;
      server.use(
        http.post(`${base}/api/v1/auth/${operation}`, () =>
          HttpResponse.json(failure('unauthorized', 'identity.credentials_invalid'), {
            status: 401,
          }),
        ),
        http.post(`${base}/api/v1/auth/refresh`, () => {
          refreshes++;
          return HttpResponse.json(success(tokens()));
        }),
      );
      const payload = {
        tenantSlug: 'demo',
        email: 'test@example.com',
        password: 'secret',
        displayName: 'Test',
      };
      await expect(
        operation === 'login' ? signIn(payload) : registerAccount(payload),
      ).rejects.toMatchObject({ status: 'unauthorized' });
      expect(refreshes).toBe(0);
    },
  );
  it.each([messageCodes.refreshInvalid, messageCodes.refreshReuseDetected])(
    'refresh %s forces exit without another attempt',
    async (code) => {
      await login();
      let refreshes = 0;
      server.use(
        http.post(`${base}/api/v1/auth/refresh`, () => {
          refreshes++;
          return HttpResponse.json(failure('unauthorized', code), { status: 401 });
        }),
      );
      await expect(refreshSession()).rejects.toMatchObject({ messageCode: code });
      expect(refreshes).toBe(1);
      expect(tokenStore.refresh()).toBeUndefined();
    },
  );
  it('login and a later password-required response set the same flag without logout', async () => {
    await login('user-a', true);
    expect(useSession.getState().mustChangePassword).toBe(true);
    await login();
    expect(useSession.getState().mustChangePassword).toBe(false);
    server.use(
      http.get(`${base}/probe`, () =>
        HttpResponse.json(failure('forbidden', messageCodes.passwordChangeRequired), {
          status: 403,
        }),
      ),
    );
    await expect(request('/probe')).rejects.toMatchObject({ status: 'forbidden' });
    expect(useSession.getState().mustChangePassword).toBe(true);
    expect(useSession.getState().state).toBe('authenticated');
    expect(tokenStore.access()).toBeDefined();
  });
  it.each([
    ['forbidden', messageCodes.permissionDenied, 403],
    ['externalServiceError', messageCodes.permissionMatrixUnavailable, 502],
  ] as const)('permission result %s retains the session', async (status, code, httpStatus) => {
    await login();
    server.use(
      http.get(`${base}/probe`, () =>
        HttpResponse.json(failure(status, code), { status: httpStatus }),
      ),
    );
    const error = await request('/probe').catch((value: unknown) => value as ApiError);
    expect(error).toMatchObject({ messageCode: code });
    expect(useSession.getState().state).toBe('authenticated');
    expect((error as ApiError).feedback.temporary).toBe(
      code === messageCodes.permissionMatrixUnavailable,
    );
  });
  it('logout revokes before clearing and still clears when the endpoint fails', async () => {
    await login();
    queryClient.setQueryData(['private'], 'private-user-data');
    let observed = false;
    server.use(
      http.post(`${base}/api/v1/auth/logout`, async ({ request: incoming }) => {
        expect(tokenStore.refresh()).toBeDefined();
        expect(await incoming.json()).toEqual({
          refreshToken: tokens().refreshToken,
          allSessions: true,
        });
        observed = true;
        return HttpResponse.json(failure('unexpectedError'), { status: 500 });
      }),
    );
    await expect(signOut(true)).rejects.toBeInstanceOf(ApiError);
    expect(observed).toBe(true);
    expect(tokenStore.access()).toBeUndefined();
    expect(tokenStore.refresh()).toBeUndefined();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });
  it('changing a password ends the session after successful response', async () => {
    await login();
    server.use(
      http.post(`${base}/api/v1/auth/change-password`, () => HttpResponse.json(success())),
    );
    await changePassword({ currentPassword: 'old', newPassword: 'new' });
    expect(useSession.getState().state).toBe('anonymous');
  });
  it('stores only refresh token persistently and never access token or credentials', async () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    await login();
    expect(storage.mock.calls).toEqual([['adnoir.refresh-token', tokens().refreshToken]]);
    expect(JSON.stringify(useSession.getState())).not.toContain(tokens().accessToken);
  });
  it('an old unauthorized mutation is never replayed as a new account', async () => {
    await login();
    let release!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    let started!: () => void;
    const ready = new Promise<void>((resolve) => {
      started = resolve;
    });
    let calls = 0;
    server.use(
      http.post(`${base}/probe`, async () => {
        calls++;
        started();
        await gate;
        return HttpResponse.json(failure('unauthorized'), { status: 401 });
      }),
    );
    const pending = request('/probe', { method: 'POST', body: { title: 'user-a-record' } }).catch(
      (error: unknown) => error,
    );
    await ready;
    forceSignOut();
    await login('user-b');
    release();
    await pending;
    expect(calls).toBe(1);
    expect(useSession.getState().claims.sub).toBe('user-b');
  });
  it.each([true, false])(
    'a stale refresh result (success=%s) cannot replace a new login',
    async (succeeds) => {
      await login();
      let release!: () => void;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      let started!: () => void;
      const ready = new Promise<void>((resolve) => {
        started = resolve;
      });
      server.use(
        http.post(`${base}/api/v1/auth/refresh`, async () => {
          started();
          await gate;
          return HttpResponse.json(
            succeeds
              ? success(tokens('user-a'))
              : failure('unauthorized', messageCodes.refreshInvalid),
          );
        }),
      );
      const pending = refreshSession().catch(() => undefined);
      await ready;
      await login('user-b');
      release();
      await pending;
      expect(useSession.getState().claims.sub).toBe('user-b');
      expect(tokenStore.access()).toBe(tokens('user-b').accessToken);
    },
  );
  it.each([
    'unauthorized',
    'forbidden',
    'rateLimited',
    'validationFailed',
    'notFound',
    'conflict',
    'concurrencyConflict',
    'businessRuleViolation',
  ] as const)('query retry is disabled for %s', (status) => {
    expect(shouldRetry(0, new ApiError(status, 'test.code', 'message'))).toBe(false);
  });
  it('does not replay a mutation if a shared refresh key resolves to a different account', async () => {
    await login();
    let calls = 0;
    queryClient.setQueryData(['private'], 'user-a-private');
    server.use(
      http.post(`${base}/probe`, () => {
        calls++;
        return HttpResponse.json(failure('unauthorized'), { status: 401 });
      }),
      http.post(`${base}/api/v1/auth/refresh`, () => HttpResponse.json(success(tokens('user-b')))),
    );
    await expect(
      request('/probe', { method: 'POST', body: { title: 'user-a-draft' } }),
    ).rejects.toBeInstanceOf(ApiError);
    expect(calls).toBe(1);
    expect(useSession.getState().claims.sub).toBe('user-b');
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });
});
