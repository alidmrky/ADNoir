import type { FormField } from '../../../contracts/form';
import type {
  DataScope,
  DataScopeInput,
  RoleAssignmentDto,
} from '../../../contracts/generated/identity';
export const createFields: FormField[] = [
  { name: 'displayName' },
  { name: 'email', type: 'email' },
  { name: 'password', type: 'password' },
  { name: 'orgUnitId', type: 'uuid' },
  { name: 'mustChangePassword', type: 'checkbox' },
];
const scopeValues: Record<DataScope, DataScopeInput> = { self: 0, unit: 1, subtree: 2, tenant: 3 };
export const scopeLabels = ['scopeSelf', 'scopeOrgUnit', 'scopeSubtree', 'scopeTenant'];
export function assignmentInitial(roles: RoleAssignmentDto[]) {
  return roles.map((role) => ({
    ...role,
    readScope: scopeValues[role.readScope],
    writeScope: scopeValues[role.writeScope],
  }));
}
export function mapUserField(field: string) {
  const value = field[0]?.toLowerCase() + field.slice(1);
  if (createFields.some((item) => item.name === value)) return value;
  const match = /^assignments\[(\d+)\]\.(RoleId|RoleCode|ReadScope|WriteScope)$/.exec(value);
  const mapping: Record<string, string> = {
    RoleId: 'roleId',
    RoleCode: 'roleCode',
    ReadScope: 'readScope',
    WriteScope: 'writeScope',
  };
  return match ? `assignments.${match[1]}.${mapping[match[2]!]}` : undefined;
}
