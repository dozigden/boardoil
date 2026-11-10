import type { Component } from 'vue';

type FieldName<T> = Extract<keyof T, string>;

type FieldSettings<T> = {
  name: FieldName<T>;
  label: string;
  help?: string;
  disabled?: (values: T) => boolean;
  reassessOnChange?: boolean;
  errorFields?: readonly string[];
  clearFields?: readonly FieldName<T>[] | 'all';
};

export type FormOption<Value> = {
  value: Value;
  label: string;
  description?: string;
  disabled?: boolean;
};

type ChoiceValue = string | number | null;
type Options<T> = readonly FormOption<ChoiceValue>[] | ((values: T) => readonly FormOption<ChoiceValue>[]);

type TextField = {
  type: 'text' | 'email' | 'password';
  maxLength?: number;
  autocomplete?: string;
  placeholder?: string;
  required?: boolean;
  clearable?: boolean;
  spellcheck?: boolean;
};

type TextareaField = { type: 'textarea'; rows?: number };
type FileField = { type: 'file'; accept?: string };
type CheckboxField = { type: 'checkbox' };
type EmojiField = { type: 'emoji' };
type StyleField = { type: 'style' };
type TokenScopesField = { type: 'tokenScopes'; allowedScopes: readonly string[] };
type TokenExpiryField = { type: 'tokenExpiry' };
type CustomField = { type: 'custom'; component: Component };

type SearchableSelectField = {
  type: 'searchableSelect';
  options: readonly { value: string; label: string }[];
};

type SelectField<T> = {
  type: 'select';
  options: Options<T>;
  required?: boolean;
};

type RadioField<T> = {
  type: 'radio';
  options: Options<T>;
};

export type FormFieldDefinition<T> = FieldSettings<T> & (
  | TextField
  | TextareaField
  | SelectField<T>
  | RadioField<T>
  | SearchableSelectField
  | FileField
  | CheckboxField
  | EmojiField
  | StyleField
  | TokenScopesField
  | TokenExpiryField
  | CustomField
);
