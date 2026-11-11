import type { Component } from 'vue';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { FormFieldDefinition } from './fieldDefinitions';

export type { FormFieldDefinition, FormOption } from './fieldDefinitions';
export type FormError = Pick<AppError, 'message' | 'validationErrors'>;
export type FormErrors<T> = Partial<Record<Extract<keyof T, string>, string[]>>;

export type FormAssessment<T> = {
  values: T;
  fields?: readonly FormFieldDefinition<T>[];
  error?: string;
};

export type FormAction<T> = {
  name: string;
  label: string;
  icon?: Component;
  danger?: boolean;
  confirm?: () => Promise<boolean>;
  run: (values: T) => Promise<Result<unknown, FormError>>;
};

// Drafts are plain, structured-cloneable data. Controls replace values through updates.
export type FormDefinition<T extends object, R> = {
  title: string;
  submitLabel: string | ((values: T) => string);
  submitText?: string;
  cancelLabel?: string;
  closeLabel?: string;
  category?: string;
  initialValues: () => T;
  fields: readonly FormFieldDefinition<T>[];
  reassess?: (values: T) => FormAssessment<T> | Promise<FormAssessment<T>>;
  savedValues?: (result: R) => T;
  validate?: (values: T) => FormErrors<T>;
  canSubmit?: (values: T) => boolean;
  // Preserve the owning board's feedback boundary when an operation completes late.
  isCurrent?: () => boolean;
  submit: (values: T) => Promise<Result<R, FormError>>;
  actions?: readonly FormAction<T>[];
};

export function defineForm<T extends object, R>(definition: FormDefinition<T, R>) {
  return definition;
}
