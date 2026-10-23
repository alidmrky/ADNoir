export function gatewayUrl(): string {
  const value = import.meta.env.VITE_GATEWAY_URL || 'http://localhost:5070';
  const url = new URL(value);
  if (
    !['http:', 'https:'].includes(url.protocol) ||
    url.username ||
    url.password ||
    url.search ||
    url.hash
  ) {
    throw new Error('Invalid VITE_GATEWAY_URL');
  }
  return value.replace(/\/$/, '');
}
