import type { FormField } from '../../../contracts/form';
export const sampleFields: FormField[] = [
  { name: 'title' },
  { name: 'description', type: 'textarea', optional: true },
];
export const lookupFields: FormField[] = [{ name: 'id', type: 'uuid', label: 'recordId' }];
export function mapSampleField(field: string) {
  const value = field[0]?.toLowerCase() + field.slice(1);
  return sampleFields.some((item) => item.name === value) ? value : undefined;
}
