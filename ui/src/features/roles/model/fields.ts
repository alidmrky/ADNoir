import type { FormField } from '../../../contracts/form';
import { permissions } from '../../../contracts/permissions';
export const editFields: FormField[] = [
  { name: 'name' },
  {
    name: 'permissions',
    type: 'permissions',
    options: permissions.map((value) => ({ value, label: value })),
  },
];
export const createFields: FormField[] = [{ name: 'code' }, ...editFields];
export function mapRoleField(field: string) {
  const value = field[0]?.toLowerCase() + field.slice(1);
  return createFields.some((item) => item.name === value) ? value : undefined;
}
