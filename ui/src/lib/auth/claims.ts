import type { Claims } from '../../contracts/claims';
export function decodeClaims(token: string): Claims {
  try {
    const part = token.split('.')[1];
    if (!part) return {};
    const binary = atob(part.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(
      new TextDecoder().decode(Uint8Array.from(binary, (c) => c.charCodeAt(0))),
    ) as Claims;
  } catch {
    return {};
  }
}
