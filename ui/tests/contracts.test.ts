import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { permissions } from '../src/contracts/permissions';
import { messageCodes } from '../src/contracts/message-codes';
import { mapUserField, assignmentInitial } from '../src/features/users/model/fields';
import { mapAuthField } from '../src/features/auth/model/fields';
import { mapTenantField, tenantInitial } from '../src/features/tenants/model/fields';
import { mapOrgField } from '../src/features/org-units/model/fields';
import { mapRoleField } from '../src/features/roles/model/fields';
import { mapSampleField } from '../src/features/sample-items/model/fields';
import tr from '../src/lib/i18n/tr.json';
import en from '../src/lib/i18n/en.json';
describe('backend mirrors and bilingual catalogs', () => {
  it('matches the entire permission catalog exactly', () => {
    const source = readFileSync(
      resolve('../src/Shared/AdNoir.Shared.Contracts/Permissions/PermissionCatalog.cs'),
      'utf8',
    );
    const values = [...source.matchAll(/public const string \w+ = "([^"]+)"/g)].map(
      (match) => match[1],
    );
    expect([...permissions].sort()).toEqual(values.sort());
  });
  it('only uses message constants that exist in the backend', () => {
    const source = readFileSync(
      resolve('../src/Shared/AdNoir.Shared.Contracts/Messages/MessageCodes.cs'),
      'utf8',
    );
    for (const code of Object.values(messageCodes)) expect(source).toContain(`"${code}"`);
    expect(JSON.stringify([tr, en])).not.toContain('identity.password.change_required');
  });
  it('has identical language keys and named placeholders', () => {
    expect(Object.keys(tr).sort()).toEqual(Object.keys(en).sort());
    for (const key of Object.keys(tr) as (keyof typeof tr)[]) {
      const placeholders = (text: string) =>
        [...text.matchAll(/\{([^}]+)\}/g)].map((match) => match[1]).sort();
      expect(placeholders(tr[key]), key).toEqual(placeholders(en[key]));
    }
  });
  it('reproduces the committed generated payloads', () => {
    expect(() =>
      execFileSync(process.execPath, ['scripts/generate-contracts.mjs', '--check'], {
        cwd: process.cwd(),
        stdio: 'pipe',
      }),
    ).not.toThrow();
  });
});
describe('explicit server field and enum mappings', () => {
  it('maps nested assignments without lowercasing the server path indiscriminately', () => {
    expect(mapUserField('Assignments[0].RoleId')).toBe('assignments.0.roleId');
    expect(mapUserField('Assignments[12].WriteScope')).toBe('assignments.12.writeScope');
    expect(mapUserField('Assignments[0].Unknown')).toBeUndefined();
    expect(mapUserField('OrgUnitId')).toBe('orgUnitId');
  });
  it('maps each feature form explicitly and leaves unknown errors at form level', () => {
    expect(mapAuthField('CurrentPassword')).toBe('currentPassword');
    expect(mapTenantField('TimeZoneId')).toBe('timeZoneId');
    expect(mapOrgField('NewParentId')).toBe('newParentId');
    expect(mapRoleField('Permissions')).toBe('permissions');
    expect(mapSampleField('Description')).toBe('description');
    expect(mapAuthField('SecurityStamp')).toBeUndefined();
    expect(tenantInitial()).toEqual({ timeZoneId: 'Europe/Istanbul' });
  });
  it('converts response scopes to the numeric request contract', () => {
    expect(
      assignmentInitial([
        { roleId: 'role', roleCode: 'Viewer', readScope: 'tenant', writeScope: 'unit' },
      ]),
    ).toEqual([{ roleId: 'role', roleCode: 'Viewer', readScope: 3, writeScope: 1 }]);
  });
});
