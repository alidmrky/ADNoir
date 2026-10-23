export const tenantKeys = {
  all: ['tenants'] as const,
  list: (pageNumber: number, pageSize: number, language: string) =>
    ['tenants', 'list', pageNumber, pageSize, language] as const,
};
