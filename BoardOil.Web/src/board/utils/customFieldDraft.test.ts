import { describe, expect, it } from 'vitest';
import { buildCustomFieldDefinitions, createCustomFieldDraft, describeFieldError, fieldErrorIndex, resetFieldTypeOptions, retainCustomFieldErrors, updateFieldName } from './customFieldDraft';
import type { CustomFieldDefinition } from '../../shared/types/customFieldTypes';

const integer: CustomFieldDefinition = {
  dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null,
  nullable: false, showOnCard: true, defaultValue: 0, min: 0, max: 20
};

describe('card field editor drafts', () => {
  it('keeps a new field code in sync with the name using only letters and numbers', () => {
    const draft = createCustomFieldDraft();
    updateFieldName(draft, 'Due date!');
    expect(draft.code).toBe('Duedate');
    updateFieldName(draft, 'Estimate (v2)_');
    expect(draft.code).toBe('Estimatev2');
  });

  it.each(['CustomCode', ''])('preserves a manually edited code, including a deliberate clear', code => {
    const draft = createCustomFieldDraft();
    draft.code = code;
    draft.codeEdited = true;
    updateFieldName(draft, 'New name');
    expect(draft.code).toBe(code);
  });

  it('keeps an existing field code when renaming its display name', () => {
    const draft = createCustomFieldDraft(integer);
    updateFieldName(draft, 'Effort');
    expect(draft.code).toBe('Points');
  });

  it('round trips the typed union, keeping zero, null, dates and order', () => {
    const fields: CustomFieldDefinition[] = [
      integer,
      { dataType: 'text', code: 'Note', name: 'Note', emoji: '📝', nullable: true, showOnCard: false, defaultValue: null, minLength: null, maxLength: 250 },
      { dataType: 'date', code: 'Due', name: 'Due date', emoji: null, nullable: true, showOnCard: true, defaultValue: '2026-10-04' }
    ];
    const drafts = fields.map(createCustomFieldDraft);
    expect(buildCustomFieldDefinitions(drafts)).toEqual(fields);
    drafts[0].name = 'Effort';
    expect(fields[0].name).toBe('Estimate');
    expect(drafts[0].code).toBe('Points');
  });

  it.each([
    ['1.5', '1.5'], ['2147483648', 2147483648], ['-2147483649', -2147483649],
    ['abc', 'abc'], ['Infinity', 'Infinity'], ['1e-400', '1e-400'],
    ['1.0000000000000001', '1.0000000000000001'], ['2147483647.00000001', '2147483647.00000001'],
    ['9007199254740993', '9007199254740993']
  ])('passes %s to the backend without silently sending null', (value, expected) => {
    const draft = createCustomFieldDraft(integer);
    draft.defaultValue = String(value);
    expect(buildCustomFieldDefinitions([draft])[0].defaultValue).toBe(expected);
  });

  it.each(['0', '+0012', '-2147483648', '2147483647'])('encodes exact integer input %s as a JSON number', value => {
    const draft = createCustomFieldDraft(integer);
    draft.defaultValue = value;
    expect(buildCustomFieldDefinitions([draft])[0].defaultValue).toBe(Number(value));
  });

  it('preserves lossy input in number bounds and text length constraints too', () => {
    const number = createCustomFieldDraft(integer);
    const text = createCustomFieldDraft();
    for (const draft of [number, text]) {
      draft.minimum = '1e-400';
      draft.maximum = '1.0000000000000001';
    }
    expect(buildCustomFieldDefinitions([number, text])).toMatchObject([
      { min: '1e-400', max: '1.0000000000000001' },
      { minLength: '1e-400', maxLength: '1.0000000000000001' }
    ]);
  });

  it('clears only the edited input error, keeping errors on other inputs and fields', () => {
    const previous = [createCustomFieldDraft(), createCustomFieldDraft()];
    const errors = { 'customFields[0].code': ['Required'], 'customFields[0].name': ['Required'], 'customFields[1].name': ['Required'] };
    const current = previous.map(field => ({ ...field }));
    current[0].code = 'Owner';
    expect(retainCustomFieldErrors(errors, previous, current)).toEqual({
      'customFields[0].name': ['Required'], 'customFields[1].name': ['Required']
    });
  });

  it('moves errors with their fields and drops errors for removed fields', () => {
    const previous = [createCustomFieldDraft(), createCustomFieldDraft(), createCustomFieldDraft()];
    const errors = { 'customFields[0].defaultValue': ['Required'], 'customFields[1].code': ['Required'], 'customFields[2].max': ['0 minimum'] };
    expect(retainCustomFieldErrors(errors, previous, [previous[2], previous[0]])).toEqual({
      'customFields[1].defaultValue': ['Required'], 'customFields[0].max': ['0 minimum']
    });
  });

  it('keeps existing-card errors until the backend checks again', () => {
    const previous = [createCustomFieldDraft(integer)];
    const errors = { 'cards[42].customFieldValues.Points': ['5 maximum'] };
    expect(retainCustomFieldErrors(errors, previous, [{ ...previous[0], maximum: '20' }])).toEqual(errors);
  });

  it('clears errors belonging to a previous field type', () => {
    const previous = [createCustomFieldDraft(integer)];
    expect(retainCustomFieldErrors({ 'customFields[0].max': ['0 minimum'] }, previous, [{ ...previous[0], dataType: 'date' }])).toEqual({});
  });

  it('clears incompatible options after a type change and emits only that variant', () => {
    const draft = createCustomFieldDraft(integer);
    draft.dataType = 'date';
    resetFieldTypeOptions(draft);
    const result = buildCustomFieldDefinitions([draft]);
    expect(result[0]).toEqual({ dataType: 'date', code: 'Points', name: 'Estimate', emoji: null, nullable: false, showOnCard: true, defaultValue: null });
  });

  it('maps existing-card validation paths to a display name and editable row', () => {
    const drafts = [createCustomFieldDraft(integer)];
    expect(fieldErrorIndex('cards[42].customFieldValues.points', drafts)).toBe(0);
    expect(describeFieldError('cards[42].customFieldValues.Points', drafts)).toBe('Card #42 · Estimate');
    expect(describeFieldError('customFields[0].defaultValue', drafts)).toBe('Estimate · Default value');
    expect(fieldErrorIndex('cards[42].description', drafts)).toBeNull();
  });
});
