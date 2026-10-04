import { describe, expect, it } from 'vitest';
import { buildCardFieldDefinitions, createCardFieldDraft, describeFieldError, fieldErrorIndex, resetFieldTypeOptions, retainCardFieldErrors, updateFieldDisplayName } from './cardFieldDraft';
import type { CardFieldDefinition } from '../../shared/types/cardFieldTypes';

const integer: CardFieldDefinition = {
  type: 'integer', fieldCode: 'Points', displayName: 'Estimate', emoji: null,
  nullable: false, showOnCard: true, defaultValue: 0, min: 0, max: 20
};

describe('card field editor drafts', () => {
  it('keeps a new field code in sync with the name using only letters and numbers', () => {
    const draft = createCardFieldDraft();
    updateFieldDisplayName(draft, 'Due date!');
    expect(draft.fieldCode).toBe('Duedate');
    updateFieldDisplayName(draft, 'Estimate (v2)_');
    expect(draft.fieldCode).toBe('Estimatev2');
  });

  it.each(['CustomCode', ''])('preserves a manually edited code, including a deliberate clear', code => {
    const draft = createCardFieldDraft();
    draft.fieldCode = code;
    draft.fieldCodeEdited = true;
    updateFieldDisplayName(draft, 'New name');
    expect(draft.fieldCode).toBe(code);
  });

  it('keeps an existing field code when renaming its display name', () => {
    const draft = createCardFieldDraft(integer);
    updateFieldDisplayName(draft, 'Effort');
    expect(draft.fieldCode).toBe('Points');
  });

  it('round trips the typed union, keeping zero, null, dates and order', () => {
    const fields: CardFieldDefinition[] = [
      integer,
      { type: 'text', fieldCode: 'Note', displayName: 'Note', emoji: '📝', nullable: true, showOnCard: false, defaultValue: null, minLength: null, maxLength: 250 },
      { type: 'date', fieldCode: 'Due', displayName: 'Due date', emoji: null, nullable: true, showOnCard: true, defaultValue: '2026-10-04' }
    ];
    const drafts = fields.map(createCardFieldDraft);
    expect(buildCardFieldDefinitions(drafts)).toEqual(fields);
    drafts[0].displayName = 'Effort';
    expect(fields[0].displayName).toBe('Estimate');
    expect(drafts[0].fieldCode).toBe('Points');
  });

  it.each([
    ['1.5', '1.5'], ['2147483648', 2147483648], ['-2147483649', -2147483649],
    ['abc', 'abc'], ['Infinity', 'Infinity'], ['1e-400', '1e-400'],
    ['1.0000000000000001', '1.0000000000000001'], ['2147483647.00000001', '2147483647.00000001'],
    ['9007199254740993', '9007199254740993']
  ])('passes %s to the backend without silently sending null', (value, expected) => {
    const draft = createCardFieldDraft(integer);
    draft.defaultValue = String(value);
    expect(buildCardFieldDefinitions([draft])[0].defaultValue).toBe(expected);
  });

  it.each(['0', '+0012', '-2147483648', '2147483647'])('encodes exact integer input %s as a JSON number', value => {
    const draft = createCardFieldDraft(integer);
    draft.defaultValue = value;
    expect(buildCardFieldDefinitions([draft])[0].defaultValue).toBe(Number(value));
  });

  it('preserves lossy input in number bounds and text length constraints too', () => {
    const number = createCardFieldDraft(integer);
    const text = createCardFieldDraft();
    for (const draft of [number, text]) {
      draft.minimum = '1e-400';
      draft.maximum = '1.0000000000000001';
    }
    expect(buildCardFieldDefinitions([number, text])).toMatchObject([
      { min: '1e-400', max: '1.0000000000000001' },
      { minLength: '1e-400', maxLength: '1.0000000000000001' }
    ]);
  });

  it('clears only the edited input error, keeping errors on other inputs and fields', () => {
    const previous = [createCardFieldDraft(), createCardFieldDraft()];
    const errors = { 'fields[0].fieldCode': ['Required'], 'fields[0].displayName': ['Required'], 'fields[1].displayName': ['Required'] };
    const current = previous.map(field => ({ ...field }));
    current[0].fieldCode = 'Owner';
    expect(retainCardFieldErrors(errors, previous, current)).toEqual({
      'fields[0].displayName': ['Required'], 'fields[1].displayName': ['Required']
    });
  });

  it('moves errors with their fields and drops errors for removed fields', () => {
    const previous = [createCardFieldDraft(), createCardFieldDraft(), createCardFieldDraft()];
    const errors = { 'fields[0].defaultValue': ['Required'], 'fields[1].fieldCode': ['Required'], 'fields[2].max': ['0 minimum'] };
    expect(retainCardFieldErrors(errors, previous, [previous[2], previous[0]])).toEqual({
      'fields[1].defaultValue': ['Required'], 'fields[0].max': ['0 minimum']
    });
  });

  it('keeps existing-card errors until the backend checks again', () => {
    const previous = [createCardFieldDraft(integer)];
    const errors = { 'cards[42].fields.Points': ['5 maximum'] };
    expect(retainCardFieldErrors(errors, previous, [{ ...previous[0], maximum: '20' }])).toEqual(errors);
  });

  it('clears errors belonging to a previous field type', () => {
    const previous = [createCardFieldDraft(integer)];
    expect(retainCardFieldErrors({ 'fields[0].max': ['0 minimum'] }, previous, [{ ...previous[0], type: 'date' }])).toEqual({});
  });

  it('clears incompatible options after a type change and emits only that variant', () => {
    const draft = createCardFieldDraft(integer);
    draft.type = 'date';
    resetFieldTypeOptions(draft);
    const result = buildCardFieldDefinitions([draft]);
    expect(result[0]).toEqual({ type: 'date', fieldCode: 'Points', displayName: 'Estimate', emoji: null, nullable: false, showOnCard: true, defaultValue: null });
  });

  it('maps existing-card validation paths to a display name and editable row', () => {
    const drafts = [createCardFieldDraft(integer)];
    expect(fieldErrorIndex('cards[42].fields.points', drafts)).toBe(0);
    expect(describeFieldError('cards[42].fields.Points', drafts)).toBe('Card #42 · Estimate');
    expect(describeFieldError('fields[0].defaultValue', drafts)).toBe('Estimate · Default value');
    expect(fieldErrorIndex('cards[42].description', drafts)).toBeNull();
  });
});
