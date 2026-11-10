import { expectTypeOf, it } from 'vitest';
import type { FormDefinition, FormFieldDefinition } from './formDefinition';

type Draft = {
  name: string;
  boardId: number | null;
  enabled: boolean;
};

type Field = FormFieldDefinition<Draft>;
type Option<Value> = { value: Value; label: string };

it('checks field names and fields to clear', () => {
  expectTypeOf<{ name: 'name'; label: string; type: 'text'; clearFields: ['boardId'] }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'text'; clearFields: 'all' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'missing'; label: string; type: 'text' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'text'; clearFields: ['missing'] }>().not.toMatchTypeOf<Field>();
});

it('requires recognised controls and their settings', () => {
  expectTypeOf<{ name: 'name'; label: string; type: 'unknown' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; type: 'text' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'select' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'radio' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'custom' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'custom'; component: {} }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'tokenScopes' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'tokenScopes'; allowedScopes: string[] }>().toMatchTypeOf<Field>();
});

it('accepts string, numeric and null choices', () => {
  expectTypeOf<{ name: 'boardId'; label: string; type: 'select'; options: Option<string | number | null>[] }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'radio'; options: Option<string | number | null>[] }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'select'; options: Option<boolean>[] }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'radio'; options: Option<object>[] }>().not.toMatchTypeOf<Field>();
});

it('leaves control and property compatibility to the form author', () => {
  expectTypeOf<{ name: 'name'; label: string; type: 'checkbox' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'file' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'enabled'; label: string; type: 'text' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'select'; options: Option<'other'>[] }>().toMatchTypeOf<Field>();
});

it('keeps callback drafts and submission results typed', () => {
  const field: Field = {
    name: 'boardId',
    label: 'Board',
    type: 'select',
    disabled: values => {
      expectTypeOf(values).toEqualTypeOf<Draft>();
      return !values.enabled;
    },
    options: values => {
      expectTypeOf(values).toEqualTypeOf<Draft>();
      return [{ value: values.boardId, label: values.name }];
    },
  };
  expectTypeOf(field).toMatchTypeOf<Field>();
  type Form = FormDefinition<Draft, { id: number }>;
  expectTypeOf<Form['submit']>().parameter(0).toEqualTypeOf<Draft>();
  expectTypeOf<NonNullable<Form['savedValues']>>().parameter(0).toEqualTypeOf<{ id: number }>();
  expectTypeOf<NonNullable<Form['savedValues']>>().returns.toEqualTypeOf<Draft>();
});
