import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { QueryKey } from '@tanstack/react-query';
import { errorFeedback } from './feedback';

export function useEditor<T extends { id: string }>(options: {
  rootKey: QueryKey;
  load: (id: string) => Promise<T>;
  save: (
    values: Record<string, unknown>,
    record: T | undefined,
    action: string,
  ) => Promise<unknown>;
  mapFields: (field: string) => string | undefined;
}) {
  const client = useQueryClient();
  const [editor, setEditor] = useState<{ action: string; record?: T; revision: number }>();
  const [openingError, setOpeningError] = useState<unknown>();
  const [opening, setOpening] = useState(false);
  const mutation = useMutation({
    mutationFn: (values: Record<string, unknown>) =>
      options.save(values, editor?.record, editor?.action || 'create'),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: options.rootKey });
      setEditor(undefined);
    },
  });
  async function open(action: string, id?: string) {
    mutation.reset();
    setOpeningError(undefined);
    setOpening(true);
    try {
      const record = id ? await options.load(id) : undefined;
      setEditor({ action, ...(record ? { record } : {}), revision: Date.now() });
    } catch (error) {
      setOpeningError(error);
    } finally {
      setOpening(false);
    }
  }
  const feedback = errorFeedback(mutation.error || openingError);
  const fieldErrors: Record<string, string> = {};
  for (const error of feedback?.errors || []) {
    if (error.field) {
      const name = options.mapFields(error.field);
      if (name) fieldErrors[name] = error.message || feedback!.message;
    }
  }
  return {
    editor,
    open,
    opening,
    feedback,
    fieldErrors,
    busy: mutation.isPending || opening,
    close: () => {
      if (!mutation.isPending && !opening) {
        setEditor(undefined);
        mutation.reset();
      }
    },
    reload: () => {
      if (editor) void open(editor.action, editor.record?.id);
    },
    submit: async (values: Record<string, unknown>) => {
      if (mutation.isPending || opening || feedback?.conflict) return;
      try {
        await mutation.mutateAsync(values);
      } catch {
        /* Mutation feedback remains visible with the user's draft. */
      }
    },
  };
}
