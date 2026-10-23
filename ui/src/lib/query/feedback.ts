import type { Feedback } from '../../contracts/feedback';
import { ApiError } from '../http/api-error';
export function errorFeedback(error: unknown): Feedback | undefined {
  if (!error) return undefined;
  if (error instanceof ApiError) return error.feedback;
  return {
    kind: 'error',
    message: error instanceof Error ? error.message : String(error),
    errors: [],
    correlationId: crypto.randomUUID(),
  };
}
