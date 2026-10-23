export const orgKeys = {
  all: ['org-units'] as const,
  list: (pageNumber: number, pageSize: number, language: string) =>
    ['org-units', 'list', pageNumber, pageSize, language] as const,
};
