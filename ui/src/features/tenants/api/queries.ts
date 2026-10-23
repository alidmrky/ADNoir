import { useQuery } from '@tanstack/react-query';
import type {
  TenantDto,
  CreateTenantRequest,
  UpdateTenantRequest,
} from '../../../contracts/generated/identity';
import type { Page } from '../../../contracts/page';
import { request } from '../../../lib/http/client';
import { tenantKeys } from './keys';
export const getTenant = (id: string) =>
  request<TenantDto>(`/api/v1/tenants/${encodeURIComponent(id)}`);
export function useTenants(pageNumber: number, pageSize: number, language: string) {
  return useQuery({
    queryKey: tenantKeys.list(pageNumber, pageSize, language),
    queryFn: ({ signal }) =>
      request<Page<TenantDto>>(`/api/v1/tenants/?pageNumber=${pageNumber}&pageSize=${pageSize}`, {
        paged: true,
        signal,
      }),
  });
}
export async function saveTenant(values: Record<string, unknown>, record: TenantDto | undefined) {
  if (record) {
    const body: UpdateTenantRequest = {
      name: String(values.name),
      timeZoneId: String(values.timeZoneId),
      status: Number(values.status) as UpdateTenantRequest['status'],
      expectedVersion: record.version,
    };
    return request<TenantDto>(`/api/v1/tenants/${record.id}`, { method: 'PUT', body });
  }
  const body: CreateTenantRequest = {
    name: String(values.name),
    hierarchyEnabled: Boolean(values.hierarchyEnabled),
    timeZoneId: String(values.timeZoneId),
    ...(values.slug ? { slug: String(values.slug) } : {}),
  };
  return request<TenantDto>('/api/v1/tenants/', { method: 'POST', body });
}
