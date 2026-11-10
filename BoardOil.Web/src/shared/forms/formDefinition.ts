import type { Component } from 'vue';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { StyleDraft } from '../utils/styleTypes';

export type FormError = Pick<AppError, 'message' | 'validationErrors'>;
export type FormErrors<T> = Partial<Record<Extract<keyof T, string>, string[]>>;

// A control must be able to read the draft value and write its full output type back.
type FieldForValue<Value, Expected, Field> = [Value, Expected] extends [Expected, Value] ? Field : never;

export type FormFieldDefinition<T> = {
  [K in Extract<keyof T, string>]: {
    name: K;
    label: string;
    // A compound control can represent several API validation keys.
    errorFields?: readonly string[];
    clearFields?: readonly Extract<keyof T, string>[];
  } & (
    | { type: 'custom'; component: Component }
    | FieldForValue<T[K], string | null, { type: 'emoji' }>
    | FieldForValue<T[K], StyleDraft, { type: 'style' }>
    | (T[K] extends string ? (
      | { type: 'text' | 'email' | 'password'; maxLength?: number; autocomplete?: string; placeholder?: string; required?: boolean }
      | { type: 'select'; options: readonly { value: T[K]; label: string }[]; required?: boolean }
    ) : never)
  )
}[Extract<keyof T, string>];

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
  submitLabel: string;
  submitText?: string;
  cancelLabel?: string;
  closeLabel?: string;
  category?: string;
  initialValues: () => T;
  fields: readonly FormFieldDefinition<T>[];
  normalize?: (values: T) => T;
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
