import { useEffect } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import type { UserDto } from '../../../contracts/generated/identity';
import { assignmentInitial, scopeLabels } from '../model/fields';
import { useTranslation } from '../../../lib/i18n';
import { Button } from '../../../components/ui/button';
import { Input } from '../../../components/ui/input';

export function AssignmentForm({
  user,
  onSubmit,
  busy,
  fieldErrors,
}: {
  user: UserDto;
  onSubmit: (values: Record<string, unknown>) => Promise<void>;
  busy: boolean;
  fieldErrors: Record<string, string>;
}) {
  const { t } = useTranslation();
  const schema = z.object({
    assignments: z.array(
      z.object({
        roleId: z.string().uuid(t('invalidFormat')),
        roleCode: z.string().min(1, t('required')),
        readScope: z.number(),
        writeScope: z.number(),
      }),
    ),
  });
  type Values = z.infer<typeof schema>;
  const {
    control,
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    defaultValues: { assignments: assignmentInitial(user.roles) },
    resolver: zodResolver(schema),
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'assignments' });
  useEffect(() => {
    for (const [name, message] of Object.entries(fieldErrors))
      setError(name as `assignments.${number}.roleId`, { type: 'server', message });
  }, [fieldErrors, setError]);
  return (
    <form className="record-form" onSubmit={handleSubmit(onSubmit)} noValidate>
      <p>{t('assignmentsHint')}</p>
      {fields.map((field, index) => (
        <fieldset className="assignment" key={field.id}>
          <legend>{index + 1}</legend>
          <label htmlFor={`role-${index}`}>{t('roleId')}</label>
          <Input id={`role-${index}`} {...register(`assignments.${index}.roleId`)} />
          <label htmlFor={`code-${index}`}>{t('roleCode')}</label>
          <Input id={`code-${index}`} {...register(`assignments.${index}.roleCode`)} />
          {(['readScope', 'writeScope'] as const).map((scope) => (
            <label key={scope}>
              {t(scope)}
              <select {...register(`assignments.${index}.${scope}`, { valueAsNumber: true })}>
                {scopeLabels.map((label, value) => (
                  <option key={value} value={value}>
                    {t(label)}
                  </option>
                ))}
              </select>
            </label>
          ))}
          {Object.values(errors.assignments?.[index] || {}).map((error, i) =>
            typeof error === 'object' && error && 'message' in error ? (
              <small className="field-error" key={i}>
                {String(error.message)}
              </small>
            ) : null,
          )}
          <Button type="button" variant="ghost" disabled={busy} onClick={() => remove(index)}>
            {t('remove')}
          </Button>
        </fieldset>
      ))}
      <Button
        type="button"
        variant="outline"
        disabled={busy}
        onClick={() => append({ roleId: '', roleCode: '', readScope: 0, writeScope: 0 })}
      >
        {t('addAssignment')}
      </Button>
      <Button type="submit" disabled={busy || isSubmitting}>
        {t('save')}
      </Button>
    </form>
  );
}
