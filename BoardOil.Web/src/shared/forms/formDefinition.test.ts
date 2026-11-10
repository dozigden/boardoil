import { expectTypeOf, it } from 'vitest';
import type { StyleDraft } from '../utils/styleTypes';
import type { FormFieldDefinition } from './formDefinition';

type Draft = {
  emoji: string | null;
  style: StyleDraft;
  name: string;
  restrictedEmoji: '😀' | null;
  optionalStyle?: StyleDraft;
  extendedStyle: StyleDraft & { extra: string };
};

it('binds domain controls only to draft values that accept their full output', () => {
  type Field = FormFieldDefinition<Draft>;
  expectTypeOf<{ name: 'emoji'; label: string; type: 'emoji' }>().toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'style'; label: string; type: 'style' }>().toMatchTypeOf<Field>();

  // The emoji picker can clear its value or select any emoji.
  expectTypeOf<{ name: 'name'; label: string; type: 'emoji' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'restrictedEmoji'; label: string; type: 'emoji' }>().not.toMatchTypeOf<Field>();
  // The style editor needs a complete draft and emits a plain StyleDraft.
  expectTypeOf<{ name: 'name'; label: string; type: 'style' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'optionalStyle'; label: string; type: 'style' }>().not.toMatchTypeOf<Field>();
  expectTypeOf<{ name: 'extendedStyle'; label: string; type: 'style' }>().not.toMatchTypeOf<Field>();
});
