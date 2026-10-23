import type { Envelope } from '../../contracts/envelope';
import type { Page } from '../../contracts/page';
import type { Feedback } from '../../contracts/feedback';
import type { ResponseStatus } from '../../contracts/response-status';
import { ApiError } from './api-error';

const statuses: ResponseStatus[] = [
  'success',
  'created',
  'accepted',
  'noContent',
  'validationFailed',
  'notFound',
  'conflict',
  'unauthorized',
  'forbidden',
  'businessRuleViolation',
  'concurrencyConflict',
  'rateLimited',
  'externalServiceError',
  'unexpectedError',
  'methodNotAllowed',
  'unsupportedMediaType',
  'serviceUnavailable',
  'gatewayTimeout',
];
const successStatuses = new Set(['success', 'created', 'accepted', 'noContent']);

export function unwrap<T>(
  raw: unknown,
  correlationId: string,
  fallback: string,
  onWarning: (feedback: Feedback) => void,
  paged: boolean,
): T {
  const invalid = () =>
    new ApiError('unexpectedError', 'ui.response.invalid', fallback, [], correlationId);
  if (!raw || typeof raw !== 'object') throw invalid();
  const value = raw as Envelope;
  if (
    !statuses.includes(value.status as ResponseStatus) ||
    typeof value.messageCode !== 'string' ||
    typeof value.isSuccess !== 'boolean' ||
    value.isSuccess !== successStatuses.has(value.status) ||
    !Array.isArray(value.errors) ||
    !Array.isArray(value.warnings)
  )
    throw invalid();
  if (value.message !== undefined && typeof value.message !== 'string') throw invalid();
  for (const item of [...value.errors, ...value.warnings]) {
    if (
      !item ||
      typeof item.code !== 'string' ||
      (item.message !== undefined && typeof item.message !== 'string') ||
      (item.field !== undefined && typeof item.field !== 'string')
    )
      throw invalid();
  }
  const id = typeof value.correlationId === 'string' ? value.correlationId : correlationId;
  if (!value.isSuccess)
    throw new ApiError(
      value.status as ResponseStatus,
      value.messageCode,
      value.message || fallback,
      value.errors,
      id,
    );
  for (const warning of value.warnings)
    onWarning({
      kind: 'warning',
      message: warning.message || fallback,
      correlationId: id,
      errors: [],
    });
  if (paged) {
    if (
      !Array.isArray(value.data) ||
      !Number.isInteger(value.pageNumber) ||
      !Number.isInteger(value.pageSize) ||
      typeof value.totalCount !== 'number' ||
      typeof value.totalPages !== 'number'
    )
      throw invalid();
    const page: Page<unknown> = {
      items: value.data,
      pageNumber: value.pageNumber!,
      pageSize: value.pageSize!,
      totalCount: value.totalCount,
      totalPages: value.totalPages,
    };
    return page as T;
  }
  return value.data as T;
}
