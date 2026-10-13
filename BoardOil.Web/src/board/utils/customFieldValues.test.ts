import { describe, expect, it } from 'vitest';
import { buildCustomFieldValues, visibleCustomFields } from './customFieldValues';
import type { CustomFieldDefinition } from '../../shared/types/customFieldTypes';

const definitions: CustomFieldDefinition[] = [
  { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null, nullable: false, defaultValue: 0, min: 0, max: 10, showOnCard: true },
  { dataType: 'date', code: 'Due', name: 'Due date', emoji: null, nullable: true, defaultValue: null, showOnCard: true },
  { dataType: 'text', code: 'Note', name: 'Note', emoji: null, nullable: true, defaultValue: null, minLength: null, maxLength: 250, showOnCard: false }
];

describe('card field values', () => {
  it('serialises native values and explicit nulls without filling or pruning stale keys', () => {
    expect(buildCustomFieldValues(definitions, { Points: '0', Due: '2026-10-04', Note: '  ', Old: 'keep' }))
      .toEqual({ Points: 0, Due: '2026-10-04', Note: null, Old: 'keep' });
    expect(buildCustomFieldValues(definitions, { Points: '1e-400' })).toEqual({ Points: '1e-400' });
  });

  it('renders configured fields in definition order, including zero, omitting null and hidden fields', () => {
    expect(visibleCustomFields(definitions, { Note: 'Private', Due: '2026-10-04', Points: 0 }).map(field => [field.name, field.value]))
      .toEqual([['Estimate', '0'], ['Due date', new Date('2026-10-04').toLocaleDateString(undefined, { timeZone: 'UTC' })]]);
    expect(visibleCustomFields(definitions, { Points: null, Due: null, Note: 'Private' })).toEqual([]);
  });
});
