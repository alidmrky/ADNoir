import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import type { FormField } from '../../contracts/form';
import { useTranslation } from '../../lib/i18n';
import { Button } from '../ui/button';
import { Input } from '../ui/input';

export function RecordForm({
  fields,
  initial = {},
  onSubmit,
  busy,
  disabled,
  submitLabel = 'save',
  fieldErrors = {},
}: {
  fields: FormField[];
  initial?: Record<string, unknown>;
  onSubmit: (values: Record<string, unknown>) => Promise<void>;
  busy?: boolean;
  disabled?: boolean;
  submitLabel?: string;
  fieldErrors?: Record<string, string>;
}) {
  const { t } = useTranslation();
  const shape: Record<string, z.ZodType> = {};
  for (const field of fields) {
    if (field.type === 'checkbox') shape[field.name] = z.boolean();
    else if (field.type === 'permissions') shape[field.name] = z.array(z.string());
    else {
      let rule = z.string();
      if (!field.optional) rule = rule.min(1, t('required'));
      if (field.type === 'email') rule = rule.email(t('invalidFormat'));
      if (field.type === 'uuid') rule = rule.uuid(t('invalidFormat'));
      shape[field.name] = field.optional ? z.union([rule, z.literal('')]) : rule;
    }
  }
  const defaults = Object.fromEntries(
    fields.map((field) => [
      field.name,
      initial[field.name] ??
        (field.type === 'checkbox' ? false : field.type === 'permissions' ? [] : ''),
    ]),
  );
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Record<string, unknown>>({
    defaultValues: defaults,
    resolver: zodResolver(z.object(shape)),
  });
  useEffect(() => {
    for (const [name, message] of Object.entries(fieldErrors))
      setError(name, { type: 'server', message });
  }, [fieldErrors, setError]);
  return (
    <form className="record-form" onSubmit={handleSubmit(onSubmit)} noValidate>
      {fields.map((field) => (
        <div
          className={`form-field ${field.type === 'checkbox' ? 'checkbox-field' : ''}`}
          key={field.name}
        >
          {field.type !== 'checkbox' && (
            <label htmlFor={field.name}>
              {t(field.label || field.name)}
              {field.optional && <small>{t('optional')}</small>}
            </label>
          )}
          {field.type === 'textarea' ? (
            <textarea id={field.name} {...register(field.name)} />
          ) : field.type === 'select' ? (
            <select id={field.name} {...register(field.name)}>
              {field.options?.map((option) => (
                <option key={option.value} value={option.value}>
                  {t(option.label)}
                </option>
              ))}
            </select>
          ) : field.type === 'permissions' ? (
            <div className="permission-grid">
              {field.options?.map((option) => (
                <label key={option.value}>
                  <input type="checkbox" value={option.value} {...register(field.name)} />
                  {option.label}
                </label>
              ))}
            </div>
          ) : (
            <Input
              id={field.name}
              type={field.type === 'uuid' ? 'text' : field.type || 'text'}
              autoComplete={
                field.type === 'password'
                  ? field.name === 'currentPassword' || field.name === 'password'
                    ? 'current-password'
                    : 'new-password'
                  : field.type === 'email'
                    ? 'email'
                    : 'off'
              }
              aria-invalid={!!errors[field.name]}
              aria-describedby={errors[field.name] ? `${field.name}-error` : undefined}
              {...register(field.name)}
            />
          )}
          {field.type === 'checkbox' && (
            <label htmlFor={field.name}>{t(field.label || field.name)}</label>
          )}
          {errors[field.name]?.message && (
            <small className="field-error" id={`${field.name}-error`}>
              {String(errors[field.name]?.message)}
            </small>
          )}
        </div>
      ))}
      <Button type="submit" disabled={disabled || busy || isSubmitting}>
        {t(busy || isSubmitting ? 'saving' : submitLabel)}
      </Button>
    </form>
  );
}
