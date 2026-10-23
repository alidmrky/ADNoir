import type { FormField } from '../../../contracts/form';
export const loginFields: FormField[] = [
  { name: 'tenantSlug' },
  { name: 'email', type: 'email' },
  { name: 'password', type: 'password' },
];
export const registerFields: FormField[] = [...loginFields, { name: 'displayName' }];
export const passwordFields: FormField[] = [
  { name: 'currentPassword', type: 'password' },
  { name: 'newPassword', type: 'password' },
];
export function mapAuthField(field: string) {
  const normalized = field[0]?.toLowerCase() + field.slice(1);
  return [...registerFields, ...passwordFields].some((item) => item.name === normalized)
    ? normalized
    : undefined;
}
