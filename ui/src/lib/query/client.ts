import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '../http/api-error';
export function shouldRetry(count: number, error: Error): boolean {
  return (
    count < 1 &&
    error instanceof ApiError &&
    ['externalServiceError', 'serviceUnavailable', 'gatewayTimeout', 'unexpectedError'].includes(
      error.status,
    )
  );
}
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: shouldRetry, refetchOnWindowFocus: false },
    mutations: { retry: false },
  },
});
