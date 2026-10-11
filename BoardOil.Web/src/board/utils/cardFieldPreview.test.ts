import { describe, expect, it } from 'vitest';
import { createCardFieldDraft, type CardFieldDraft } from './cardFieldDraft';
import { previewCardFields } from './cardFieldPreview';

function draft(overrides: Partial<CardFieldDraft>) {
  return { ...createCardFieldDraft(), showOnCard: true, ...overrides };
}

describe('card field preview', () => {
  it('uses defaults, including zero, and keeps visible draft fields in their configured order', () => {
    const fields = [
      draft({ displayName: 'Hidden', showOnCard: false, defaultValue: 'Private' }),
      draft({ displayName: 'Estimate', type: 'integer', defaultValue: '0', emoji: '📏' }),
      draft({ displayName: 'Note', defaultValue: '  Sample note  ' }),
      draft({ displayName: 'Due', type: 'date', defaultValue: '2026-10-04' })
    ];

    expect(previewCardFields(fields)).toEqual([
      { key: fields[1].key, displayName: 'Estimate', type: 'integer', emoji: '📏', value: '0' },
      { key: fields[2].key, displayName: 'Note', type: 'text', emoji: null, value: 'Sample note' },
      { key: fields[3].key, displayName: 'Due', type: 'date', emoji: null,
        value: new Date('2026-10-04').toLocaleDateString(undefined, { timeZone: 'UTC' }) }
    ]);
  });

  it('uses example values without changing defaults and takes today from the browser calendar day', () => {
    const fields = [draft({ type: 'text' }), draft({ type: 'integer' }), draft({ type: 'date' })];
    const today = new Date(2026, 9, 11, 0, 15);

    expect(previewCardFields(fields, today).map(field => field.value)).toEqual([
      'example', '12345', new Date('2026-10-11').toLocaleDateString(undefined, { timeZone: 'UTC' })
    ]);
    expect(fields.map(field => field.defaultValue)).toEqual(['', '', '']);
  });
});
