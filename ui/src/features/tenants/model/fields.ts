import type { FormField } from '../../../contracts/form';
import type { TenantDto } from '../../../contracts/generated/identity';
export const tenantStatuses = [
  'trial',
  'active',
  'suspended',
  'pendingDeletion',
  'deleted',
] as const;
export const createFields: FormField[] = [
  { name: 'name' },
  { name: 'slug', optional: true },
  { name: 'timeZoneId' },
  { name: 'hierarchyEnabled', type: 'checkbox' },
];
export const editFields: FormField[] = [
  { name: 'name' },
  { name: 'timeZoneId' },
  {
    name: 'status',
    type: 'select',
    options: tenantStatuses.map((label, value) => ({ value: String(value), label })),
  },
];
export function tenantInitial(record?: TenantDto): Record<string, unknown> {
  return record
    ? { ...record, status: String(tenantStatuses.indexOf(record.status)) }
    : { timeZoneId: 'Europe/Istanbul' };
}
export function mapTenantField(field: string) {
  const value = field[0]?.toLowerCase() + field.slice(1);
  return [...createFields, ...editFields].some((item) => item.name === value) ? value : undefined;
}
