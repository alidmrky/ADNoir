import type { FormField } from '../../../contracts/form';
export const editFields: FormField[] = [
  { name: 'name' },
  { name: 'slug', optional: true },
  { name: 'code', optional: true },
];
export const createFields: FormField[] = [
  ...editFields,
  { name: 'parentId', type: 'uuid', optional: true },
];
export const moveFields: FormField[] = [{ name: 'newParentId', type: 'uuid', optional: true }];
export function mapOrgField(field: string) {
  const value = field[0]?.toLowerCase() + field.slice(1);
  return [...createFields, ...moveFields].some((item) => item.name === value) ? value : undefined;
}
