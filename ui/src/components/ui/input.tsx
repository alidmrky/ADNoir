import type { ComponentProps } from 'react';
import { cn } from '../../lib/utils/cn';
// Adapt the shadcn input primitive to the project's semantic CSS tokens.
export function Input({ className, ...props }: ComponentProps<'input'>) {
  return <input className={cn('input', className)} {...props} />;
}
