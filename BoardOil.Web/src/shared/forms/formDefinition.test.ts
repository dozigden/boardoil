import { expectTypeOf, it } from 'vitest';
import type { StyleDraft } from '../utils/styleTypes';
import type { FormFieldDefinition } from './formDefinition';
import type { TokenExpiryDraft } from '../utils/accessTokenDraft';

type Draft = {
  emoji: string | null;
  boardId: number | null;
  columnId: number;
  file: File | null;
  nonNullableFile: File;
  style: StyleDraft;
  name: string;
  enabled: boolean;
  fixed: true;
  restrictedEmoji: '😀' | null;
  optionalStyle?: StyleDraft;
  extendedStyle: StyleDraft & { extra: string };
  scopes: string[];
  restrictedScopes: ('mcp:read')[];
  expiry: TokenExpiryDraft;
};

it('binds domain controls only to draft values that accept their full output', () => {
  type Field = FormFieldDefinition<Draft>;
  expectTypeOf<{ name: 'enabled'; label: string; type: 'checkbox' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'fixed'; label: string; type: 'checkbox' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'searchableSelect'; options: { value: string; label: string }[] }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'enabled'; label: string; type: 'searchableSelect'; options: { value: string; label: string }[] }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'emoji'; label: string; type: 'emoji' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'style'; label: string; type: 'style' }>().toMatchTypeOf<Field>();

  // The emoji picker can clear its value or select any emoji.
  expectTypeOf<{ name: 'name'; label: string; type: 'emoji' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'restrictedEmoji'; label: string; type: 'emoji' }>().not.toMatchTypeOf<Field>();
  // The style editor needs a complete draft and emits a plain StyleDraft.
  expectTypeOf<{ name: 'name'; label: string; type: 'style' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'optionalStyle'; label: string; type: 'style' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'extendedStyle'; label: string; type: 'style' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'scopes'; label: string; type: 'tokenScopes'; allowedScopes: string[] }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'expiry'; label: string; type: 'tokenExpiry' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'restrictedScopes'; label: string; type: 'tokenScopes'; allowedScopes: string[] }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'tokenExpiry' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'scopes'; label: string; type: 'tokenScopes' }>().not.toMatchTypeOf<Field>();
});

it('retains typed numeric select values', () => {
  type Field = FormFieldDefinition<Draft>;
  expectTypeOf<{ name: 'boardId'; label: string; type: 'select'; options: { value: number | null; label: string }[] }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'boardId'; label: string; type: 'select'; options: { value: string; label: string }[] }>().not.toMatchTypeOf<Field>();

});

it('requires file fields to accept both a selection and clearing it', () => {
  type Field = FormFieldDefinition<Draft>;
  expectTypeOf<{ name: 'file'; label: string; type: 'file' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'nonNullableFile'; label: string; type: 'file' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'file' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'name'; label: string; type: 'textarea' }>().toMatchTypeOf<Field>();
});
