export const roleKeys = {
  all: ['roles'] as const,
  list: (language: string) => ['roles', 'list', language] as const,
};
