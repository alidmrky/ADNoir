import type { ResponseStatus } from '../../contracts/response-status';
import type { Feedback } from '../../contracts/feedback';
import { messageCodes } from '../../contracts/message-codes';

export class ApiError extends Error {
  constructor(
    public readonly status: ResponseStatus,
    public readonly messageCode: string,
    message: string,
    public readonly errors: Feedback['errors'] = [],
    public readonly correlationId: string = crypto.randomUUID(),
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get feedback(): Feedback {
    return {
      kind: 'error',
      message: this.message,
      errors: this.errors,
      correlationId: this.correlationId,
      conflict: this.status === 'concurrencyConflict',
      temporary: this.messageCode === messageCodes.permissionMatrixUnavailable,
    };
  }
}
