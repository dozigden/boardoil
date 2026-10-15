import { describe, expect, it } from 'vitest';
import { buildCustomFieldDefinition, createCustomFieldDraft, resetFieldTypeOptions, retainCustomFieldInputErrors, updateFieldName } from './customFieldDraft';
import type { CustomFieldDefinition } from '../../shared/types/customFieldTypes';

const integer: CustomFieldDefinition = {
  dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null,
  nullable: false, showOnCard: true, defaultValue: 0, min: 0, max: 20
};

describe('card field editor drafts', () => {
  it('keeps a new field code in sync with the name using only lowercase letters and numbers', () => {
    const draft = createCustomFieldDraft();
    updateFieldName(draft, 'Due date!');
    expect(draft.code).toBe('duedate');
    updateFieldName(draft, 'Estimate (v2)_');
    expect(draft.code).toBe('estimatev2');
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
    expect(drafts.map(buildCustomFieldDefinition)).toEqual(fields);
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
    expect(buildCustomFieldDefinition(draft).defaultValue).toBe(expected);
  });

  it.each(['0', '+0012', '-2147483648', '2147483647'])('encodes exact integer input %s as a JSON number', value => {
    const draft = createCustomFieldDraft(integer);
    draft.defaultValue = value;
    expect(buildCustomFieldDefinition(draft).defaultValue).toBe(Number(value));
  });

  it('preserves lossy input in number bounds and text length constraints too', () => {
    const number = createCustomFieldDraft(integer);
    const text = createCustomFieldDraft();
    for (const draft of [number, text]) {
      draft.minimum = '1e-400';
      draft.maximum = '1.0000000000000001';
    }
    expect([number, text].map(buildCustomFieldDefinition)).toMatchObject([
      { min: '1e-400', max: '1.0000000000000001' },
      { minLength: '1e-400', maxLength: '1.0000000000000001' }
    ]);
  });

  it('clears only edited definition errors while retaining applicability and existing-card errors', () => {
    const previous = { definition: integer, appliesTo: { mode: 'all' as const } };
    const errors = { 'definition.code': ['Required'], 'definition.name': ['Required'], 'appliesTo': ['Required'], 'cards[42].customFieldValues.Points': ['Invalid'] };
    expect(retainCustomFieldInputErrors(errors, previous, { ...previous, definition: { ...integer, name: 'New' } })).toEqual({
      'definition.code': ['Required'], 'appliesTo': ['Required'], 'cards[42].customFieldValues.Points': ['Invalid']
    });
  });

  it('clears incompatible validation when type and applicability change', () => {
    const previous = { definition: integer, appliesTo: { mode: 'all' as const } };
    const next = { definition: { ...integer, dataType: 'date' as const, defaultValue: null }, appliesTo: { mode: 'selected' as const, cardTypeIds: [] } };
    expect(retainCustomFieldInputErrors({ 'definition.max': ['Invalid'], 'appliesTo.cardTypeIds': ['Invalid'] }, previous, next)).toEqual({});
  });

  it('clears incompatible options after a type change and emits only that variant', () => {
    const draft = createCustomFieldDraft(integer);
    draft.dataType = 'date';
    resetFieldTypeOptions(draft);
    const result = buildCustomFieldDefinition(draft);
    expect(result).toEqual({ dataType: 'date', code: 'Points', name: 'Estimate', emoji: null, nullable: false, showOnCard: true, defaultValue: null });
  });

});
