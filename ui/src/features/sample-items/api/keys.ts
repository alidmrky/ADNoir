export const sampleKeys = {
  all: ['sample-items'] as const,
  detail: (id: string, language: string) => ['sample-items', 'detail', id, language] as const,
};
