// @vitest-environment node
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';
const eslint = new ESLint({ cwd: process.cwd() });
const probes = [
  ['src/lib/http/probe.ts', "import { tokenStore } from '../auth/token-store'; void tokenStore;"],
  ['src/contracts/probe.ts', "import { request } from '../lib/http/client'; void request;"],
  [
    'src/features/users/api/probe.ts',
    "import { useSession } from '../../../stores/session-store'; void useSession;",
  ],
  [
    'src/features/users/ui/probe.ts',
    "import { request } from '../../../lib/http/client'; void request;",
  ],
  [
    'src/features/users/ui/probe.ts',
    "import { getRole } from '../../roles/api/queries'; void getRole;",
  ],
  [
    'src/features/users/model/probe.ts',
    "import { Button } from '../../../components/ui/button'; void Button;",
  ],
  [
    'src/features/users/api/probe.ts',
    "import type { Envelope } from '../../../contracts/envelope'; const value: Envelope | undefined = undefined; void value;",
  ],
  [
    'src/lib/auth/probe.ts',
    "import type { Envelope } from '../../contracts/envelope'; const value: Envelope | undefined = undefined; void value;",
  ],
  ['src/stores/probe.ts', "import { useQuery } from '@tanstack/react-query'; void useQuery;"],
  [
    'src/components/common/probe.ts',
    "import { tokenStore } from '../../lib/auth/token-store'; void tokenStore;",
  ],
  ['src/features/users/ui/probe.ts', "fetch('/api/v1/users');"],
  ['src/features/auth/model/probe.ts', "localStorage.setItem('access', 'secret');"],
  ['src/features/auth/model/probe.ts', "document.cookie = 'access=secret';"],
  ['src/features/users/ui/probe.ts', "import { Slot } from '@radix-ui/react-slot'; void Slot;"],
];
describe('architecture violation probes', () => {
  it.each(probes)('rejects forbidden dependency in %s: %s', async (filePath, code) => {
    const [result] = await eslint.lintText(code!, { filePath: filePath! });
    expect(
      result?.messages.some((message) =>
        ['import/no-restricted-paths', 'no-restricted-imports', 'no-restricted-syntax'].includes(
          message.ruleId || '',
        ),
      ),
    ).toBe(true);
  });
  it('allows HTTP as the sole envelope owner and public feature imports', async () => {
    const results = await Promise.all([
      eslint.lintText(
        "import type { Envelope } from '../../contracts/envelope'; const value: Envelope | undefined = undefined; void value;",
        { filePath: 'src/lib/http/probe.ts' },
      ),
      eslint.lintText("import { RolesPage } from '../../roles/index'; void RolesPage;", {
        filePath: 'src/features/users/ui/probe.ts',
      }),
    ]);
    expect(results.flat().flatMap((result) => result.messages)).toEqual([]);
  });
});
