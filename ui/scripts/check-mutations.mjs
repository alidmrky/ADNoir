import { readFileSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// Deliberately break one safety property at a time and require its regression test to fail.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const cases = [
  [
    'failure-envelope',
    'src/lib/http/envelope.ts',
    'if (!value.isSuccess) throw',
    'if (false) throw',
    'preserves notFound',
  ],
  [
    'successful-warning',
    'src/lib/http/envelope.ts',
    'for (const warning of value.warnings)',
    'for (const warning of [])',
    'unwraps pagination',
  ],
  [
    'single-flight-refresh',
    'src/lib/auth/session.ts',
    'if (pendingRefresh?.epoch === epoch) return pendingRefresh.promise;',
    '',
    'five concurrent',
  ],
  [
    'second-unauthorized-exit',
    'src/lib/http/client.ts',
    'if (repeated) { config.onSignOut(); throw error; }',
    'if (repeated) { throw error; }',
    'a second unauthorized',
  ],
  [
    'cache-clear',
    'src/app/bootstrap.ts',
    'onClear: () => { queryClient.clear();',
    'onClear: () => {',
    'a second unauthorized',
  ],
  [
    'password-required',
    'src/lib/http/client.ts',
    'config.onPasswordRequired();',
    'void 0;',
    'login and a later password-required',
  ],
  [
    'permission-outage',
    'src/lib/http/api-error.ts',
    'temporary: this.messageCode === messageCodes.permissionMatrixUnavailable',
    'temporary: false',
    'permission result externalServiceError',
  ],
  [
    'access-persistence',
    'src/lib/auth/token-store.ts',
    'localStorage.setItem(refreshKey, refresh)',
    'localStorage.setItem(refreshKey, access)',
    'stores only refresh token',
  ],
  [
    'logout-revocation',
    'src/lib/auth/session.ts',
    "if (refreshToken) await request('/api/v1/auth/logout'",
    "if (false) await request('/api/v1/auth/logout'",
    'logout revokes before clearing',
  ],
  [
    'tenant-header',
    'src/lib/http/client.ts',
    'if (token) headers.Authorization',
    "headers['X-Tenant-Id'] = 'tenant'; if (token) headers.Authorization",
    'sets culture and authorization',
  ],
  [
    'culture-header',
    'src/lib/http/client.ts',
    "'Accept-Language': config.getLanguage()",
    "'Accept-Language': 'incorrect'",
    'sets culture and authorization',
  ],
  [
    'retry-quota',
    'src/lib/query/client.ts',
    "'gatewayTimeout', 'unexpectedError'",
    "'gatewayTimeout', 'unexpectedError', 'rateLimited'",
    'query retry is disabled for rateLimited',
  ],
];
for (const [name, relative, before, after, testName] of cases) {
  const file = path.join(root, relative);
  const source = readFileSync(file, 'utf8');
  const anchor = new RegExp(
    before
      .split(/\s+/)
      .map((part) => part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
      .join('\\s*'),
  );
  if (!anchor.test(source)) throw new Error(`Mutation anchor missing: ${name}`);
  try {
    writeFileSync(file, source.replace(anchor, after));
    const result = spawnSync(
      process.execPath,
      ['node_modules/vitest/vitest.mjs', 'run', 'tests/http-auth.test.ts', '-t', testName],
      { cwd: root, encoding: 'utf8', timeout: 30_000 },
    );
    const output = `${result.stdout || ''}\n${result.stderr || ''}`;
    if (result.error || result.status === 0 || !output.includes('FAIL'))
      throw new Error(`Mutation not caught by a test assertion: ${name}\n${output}`);
    console.log(`Caught: ${name}`);
  } finally {
    writeFileSync(file, source);
  }
}
console.log(`All ${cases.length} deliberate regressions were caught; source files restored.`);
