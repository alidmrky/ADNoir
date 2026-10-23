import { useQuery } from '@tanstack/react-query';
import type {
  OrgUnitDto,
  CreateOrgUnitRequest,
  RenameOrgUnitRequest,
  MoveOrgUnitRequest,
} from '../../../contracts/generated/identity';
import type { Page } from '../../../contracts/page';
import { request } from '../../../lib/http/client';
import { orgKeys } from './keys';
export const getOrgUnit = (id: string) =>
  request<OrgUnitDto>(`/api/v1/org-units/${encodeURIComponent(id)}`);
export function useOrgUnits(pageNumber: number, pageSize: number, language: string) {
  return useQuery({
    queryKey: orgKeys.list(pageNumber, pageSize, language),
    queryFn: ({ signal }) =>
      request<Page<OrgUnitDto>>(
        `/api/v1/org-units/?pageNumber=${pageNumber}&pageSize=${pageSize}`,
        { paged: true, signal },
      ),
  });
}
export async function saveOrgUnit(
  values: Record<string, unknown>,
  record: OrgUnitDto | undefined,
  action: string,
) {
  if (action === 'delete' && record)
    return request(`/api/v1/org-units/${record.id}`, { method: 'DELETE' });
  if (action === 'move' && record) {
    const body: MoveOrgUnitRequest = {
      expectedVersion: record.version,
      ...(values.newParentId ? { newParentId: String(values.newParentId) } : {}),
    };
    return request(`/api/v1/org-units/${record.id}/move`, { method: 'POST', body });
  }
  const optional = {
    ...(values.slug ? { slug: String(values.slug) } : {}),
    ...(values.code ? { code: String(values.code) } : {}),
  };
  if (record) {
    const body: RenameOrgUnitRequest = {
      name: String(values.name),
      ...optional,
      expectedVersion: record.version,
    };
    return request(`/api/v1/org-units/${record.id}`, { method: 'PUT', body });
  }
  const body: CreateOrgUnitRequest = {
    name: String(values.name),
    ...optional,
    ...(values.parentId ? { parentId: String(values.parentId) } : {}),
  };
  return request('/api/v1/org-units/', { method: 'POST', body });
}
