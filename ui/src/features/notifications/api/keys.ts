export const notificationKeys = {
  all: ['notifications'] as const,
  list: (status: string, language: string) => ['notifications', 'list', status, language] as const,
  detail: (id: string, language: string) => ['notifications', 'detail', id, language] as const,
  statistics: (language: string) => ['notifications', 'statistics', language] as const,
};
