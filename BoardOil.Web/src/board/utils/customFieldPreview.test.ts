import { describe, expect, it } from 'vitest';
import { createCustomFieldDraft, type CustomFieldDraft } from './customFieldDraft';
import { previewCustomFields } from './customFieldPreview';

function draft(overrides: Partial<CustomFieldDraft>) {
  return { ...createCustomFieldDraft(), showOnCard: true, ...overrides };
}

describe('card field preview', () => {
  it('uses defaults, including zero, and keeps visible draft fields in their configured order', () => {
    const fields = [
      draft({ name: 'Hidden', showOnCard: false, defaultValue: 'Private' }),
      draft({ name: 'Estimate', dataType: 'integer', defaultValue: '0', emoji: '📏' }),
      draft({ name: 'Note', defaultValue: '  Sample note  ' }),
      draft({ name: 'Due', dataType: 'date', defaultValue: '2026-10-04' })
    ];

    expect(previewCustomFields(fields)).toEqual([
      { key: fields[1].key, name: 'Estimate', dataType: 'integer', emoji: '📏', value: '0' },
      { key: fields[2].key, name: 'Note', dataType: 'text', emoji: null, value: 'Sample note' },
      { key: fields[3].key, name: 'Due', dataType: 'date', emoji: null,
        value: new Date('2026-10-04').toLocaleDateString(undefined, { timeZone: 'UTC' }) }
    ]);
  });

  it('uses example values without changing defaults and takes today from the browser calendar day', () => {
    const fields = [draft({ dataType: 'text' }), draft({ dataType: 'integer' }), draft({ dataType: 'date' })];
    const today = new Date(2026, 9, 11, 0, 15);

    expect(previewCustomFields(fields, today).map(field => field.value)).toEqual([
      'example', '12345', new Date('2026-10-11').toLocaleDateString(undefined, { timeZone: 'UTC' })
    ]);
    expect(fields.map(field => field.defaultValue)).toEqual(['', '', '']);
  });
});
