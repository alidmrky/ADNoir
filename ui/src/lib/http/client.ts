import { gatewayUrl } from '../utils/config';
import type { Feedback } from '../../contracts/feedback';
import { messageCodes } from '../../contracts/message-codes';
import { ApiError } from './api-error';
import { unwrap } from './envelope';

interface Configuration {
  getAccessToken: () => string | undefined;
  getSessionGeneration: () => number;
  getLanguage: () => string;
  getNetworkMessage: () => string;
  onUnauthorized: () => Promise<void>;
  onSignOut: () => void;
  onPasswordRequired: () => void;
  onWarning: (feedback: Feedback) => void;
}
let config: Configuration;
export function configureHttp(value: Configuration) {
  config = value;
}
export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  anonymous?: boolean;
  paged?: boolean;
  signal?: AbortSignal;
}

export async function request<T>(
  path: string,
  options: RequestOptions = {},
  repeated = false,
): Promise<T> {
  const correlationId = crypto.randomUUID();
  const generation = config.getSessionGeneration();
  const token = options.anonymous ? undefined : config.getAccessToken();
  const headers: Record<string, string> = {
    'Accept-Language': config.getLanguage(),
    'X-Correlation-Id': correlationId,
  };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  const timeout = AbortSignal.timeout(20_000);
  try {
    const response = await fetch(`${gatewayUrl()}${path}`, {
      method: options.method || 'GET',
      headers,
      ...(options.body !== undefined ? { body: JSON.stringify(options.body) } : {}),
      signal: options.signal ? AbortSignal.any([options.signal, timeout]) : timeout,
    });
    const raw: unknown = await response.json();
    if (!options.anonymous && generation !== config.getSessionGeneration())
      throw new ApiError(
        'unauthorized',
        'ui.session.changed',
        config.getNetworkMessage(),
        [],
        correlationId,
      );
    return unwrap<T>(
      raw,
      response.headers.get('X-Correlation-Id') || correlationId,
      config.getNetworkMessage(),
      config.onWarning,
      options.paged || false,
    );
  } catch (cause) {
    if (options.signal?.aborted) throw cause;
    const error =
      cause instanceof ApiError
        ? cause
        : new ApiError(
            'serviceUnavailable',
            'ui.network.unavailable',
            config.getNetworkMessage(),
            [],
            correlationId,
          );
    if (generation !== config.getSessionGeneration()) throw error;
    if (
      error.messageCode === messageCodes.refreshInvalid ||
      error.messageCode === messageCodes.refreshReuseDetected
    ) {
      config.onSignOut();
      throw error;
    }
    if (
      !options.anonymous &&
      error.status === 'forbidden' &&
      error.messageCode === messageCodes.passwordChangeRequired
    )
      config.onPasswordRequired();
    if (!options.anonymous && error.status === 'unauthorized') {
      if (repeated) {
        config.onSignOut();
        throw error;
      }
      // A late unauthorized response can arrive after another request already rotated the token.
      if (token === config.getAccessToken()) await config.onUnauthorized();
      if (generation !== config.getSessionGeneration()) throw error;
      return request<T>(path, options, true);
    }
    throw error;
  }
}
