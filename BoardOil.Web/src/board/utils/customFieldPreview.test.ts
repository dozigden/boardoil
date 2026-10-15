import { describe, expect, it } from 'vitest';
import type { CustomFieldDefinition } from '../../shared/types/customFieldTypes';
import { previewCustomFields } from './customFieldPreview';

const common = { emoji: null, nullable: true, showOnCard: true };

describe('card field preview', () => {
  it('uses saved defaults, including zero, and keeps visible definitions in catalogue order', () => {
    const fields: CustomFieldDefinition[] = [
      { ...common, code: 'hidden', name: 'Hidden', dataType: 'text', showOnCard: false,
        defaultValue: 'Private', minLength: null, maxLength: null },
      { ...common, code: 'estimate', name: 'Estimate', dataType: 'integer',
        defaultValue: 0, emoji: '📏', min: null, max: null },
      { ...common, code: 'note', name: 'Note', dataType: 'text',
        defaultValue: 'Sample note', minLength: null, maxLength: null },
      { ...common, code: 'due', name: 'Due', dataType: 'date', defaultValue: '2026-10-04' }
    ];

    expect(previewCustomFields(fields)).toEqual([
      { key: 'estimate', name: 'Estimate', dataType: 'integer', emoji: '📏', value: '0' },
      { key: 'note', name: 'Note', dataType: 'text', emoji: null, value: 'Sample note' },
      { key: 'due', name: 'Due', dataType: 'date', emoji: null,
        value: new Date('2026-10-04').toLocaleDateString(undefined, { timeZone: 'UTC' }) }
    ]);
  });

  it('uses example values without changing defaults and takes today from the browser calendar day', () => {
    const fields: CustomFieldDefinition[] = [
      { ...common, code: 'note', name: 'Note', dataType: 'text', defaultValue: null, minLength: null, maxLength: null },
      { ...common, code: 'estimate', name: 'Estimate', dataType: 'integer', defaultValue: null, min: null, max: null },
      { ...common, code: 'due', name: 'Due', dataType: 'date', defaultValue: null }
    ];
    const today = new Date(2026, 9, 11, 0, 15);

    expect(previewCustomFields(fields, today).map(field => field.value)).toEqual([
      'example', '12345', new Date('2026-10-11').toLocaleDateString(undefined, { timeZone: 'UTC' })
    ]);
    expect(fields.map(field => field.defaultValue)).toEqual([null, null, null]);
  });
});
