export function success(data?: unknown, extra: Record<string, unknown> = {}) {
  return {
    isSuccess: true,
    status: 'success',
    messageCode: 'common.success',
    message: 'Başarılı.',
    errors: [],
    warnings: [],
    correlationId: 'server-reference',
    ...(data !== undefined ? { data } : {}),
    ...extra,
  };
}
export function failure(
  status: string,
  messageCode = `common.${status}`,
  extra: Record<string, unknown> = {},
) {
  return {
    isSuccess: false,
    status,
    messageCode,
    message: 'Sunucu hata açıklaması.',
    errors: [],
    warnings: [],
    correlationId: 'server-reference',
    ...extra,
  };
}
export function tokens(subject = 'user-a', mustChangePassword = false) {
  return {
    accessToken: `header.${btoa(JSON.stringify({ sub: subject, tenant_id: 'tenant-a', email: 'test@example.com', name: 'Test User', roles: 'TenantAdmin', read_scope: 'Tenant', write_scope: 'Unit' }))}.signature`,
    refreshToken: `refresh-${subject}`,
    expiresInSeconds: 300,
    mustChangePassword,
    tokenType: 'Bearer',
  };
}
