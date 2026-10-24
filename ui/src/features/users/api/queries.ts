import { useQuery } from '@tanstack/react-query';
import type {
  UserDto,
  CreateUserRequest,
  AssignRolesRequest,
  ResetUserPasswordRequest,
} from '../../../contracts/generated/identity';
import type { Page } from '../../../contracts/page';
import { request } from '../../../lib/http/client';
import { userKeys } from './keys';
export const getUser = (id: string) => request<UserDto>(`/api/v1/users/${encodeURIComponent(id)}`);
export function useUsers(pageNumber: number, pageSize: number, language: string) {
  return useQuery({
    queryKey: userKeys.list(pageNumber, pageSize, language),
    queryFn: ({ signal }) =>
      request<Page<UserDto>>(`/api/v1/users/?pageNumber=${pageNumber}&pageSize=${pageSize}`, {
        paged: true,
        signal,
      }),
  });
}
export async function saveUser(
  values: Record<string, unknown>,
  record: UserDto | undefined,
  action: string,
) {
  if (record) {
    if (action === 'assignRoles') {
      const body: AssignRolesRequest = {
        assignments: values.assignments as AssignRolesRequest['assignments'],
        expectedVersion: record.version,
      };
      return request(`/api/v1/users/${record.id}/roles`, { method: 'PUT', body });
    }
    if (action === 'resetPassword') {
      const body: ResetUserPasswordRequest = {
        newPassword: String(values.newPassword),
        expectedVersion: record.version,
      };
      return request(`/api/v1/users/${record.id}/reset-password`, { method: 'POST', body });
    }
    return request(
      `/api/v1/users/${record.id}/${record.isActive ? 'deactivate' : 'activate'}?expectedVersion=${record.version}`,
      { method: 'POST' },
    );
  }
  const body: CreateUserRequest = {
    email: String(values.email),
    displayName: String(values.displayName),
    password: String(values.password),
    orgUnitId: String(values.orgUnitId),
    mustChangePassword: Boolean(values.mustChangePassword),
  };
  return request('/api/v1/users/', { method: 'POST', body });
}
