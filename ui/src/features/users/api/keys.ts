export const userKeys = {
  all: ['users'] as const,
  list: (pageNumber: number, pageSize: number, language: string) =>
    ['users', 'list', pageNumber, pageSize, language] as const,
};
