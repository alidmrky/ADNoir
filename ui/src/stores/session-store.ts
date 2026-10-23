import { create } from 'zustand';
import type { Claims } from '../contracts/claims';
interface Session {
  state: 'loading' | 'anonymous' | 'authenticated';
  claims: Claims;
  mustChangePassword: boolean;
}
export const useSession = create<Session>(() => ({
  state: 'loading',
  claims: {},
  mustChangePassword: false,
}));
