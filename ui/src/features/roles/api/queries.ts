import { useQuery } from '@tanstack/react-query';
import type {
  RoleDto,
  CreateRoleRequest,
  UpdateRoleRequest,
} from '../../../contracts/generated/identity';
import { request } from '../../../lib/http/client';
import { roleKeys } from './keys';
export const getRole = (id: string) => request<RoleDto>(`/api/v1/roles/${encodeURIComponent(id)}`);
export function useRoles(language: string) {
  return useQuery({
    queryKey: roleKeys.list(language),
    queryFn: ({ signal }) => request<RoleDto[]>('/api/v1/roles/', { signal }),
  });
}
export async function saveRole(
  values: Record<string, unknown>,
  record: RoleDto | undefined,
  action: string,
) {
  if (action === 'delete' && record)
    return request(`/api/v1/roles/${record.id}`, { method: 'DELETE' });
  if (record) {
    const body: UpdateRoleRequest = {
      name: String(values.name),
      permissions: values.permissions as string[],
      expectedVersion: record.version,
    };
    return request(`/api/v1/roles/${record.id}`, { method: 'PUT', body });
  }
  const body: CreateRoleRequest = {
    name: String(values.name),
    code: String(values.code),
    permissions: values.permissions as string[],
  };
  return request('/api/v1/roles/', { method: 'POST', body });
}
