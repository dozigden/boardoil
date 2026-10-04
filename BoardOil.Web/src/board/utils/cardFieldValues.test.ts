import { describe, expect, it } from 'vitest';
import { buildCardFieldValues, visibleCardFields } from './cardFieldValues';
import type { CardFieldDefinition } from '../../shared/types/cardFieldTypes';

const definitions: CardFieldDefinition[] = [
  { type: 'integer', fieldCode: 'Points', displayName: 'Estimate', emoji: null, nullable: false, defaultValue: 0, min: 0, max: 10, showOnCard: true },
  { type: 'date', fieldCode: 'Due', displayName: 'Due date', emoji: null, nullable: true, defaultValue: null, showOnCard: true },
  { type: 'text', fieldCode: 'Note', displayName: 'Note', emoji: null, nullable: true, defaultValue: null, minLength: null, maxLength: 250, showOnCard: false }
];

describe('card field values', () => {
  it('serialises native values and explicit nulls without filling or pruning stale keys', () => {
    expect(buildCardFieldValues(definitions, { Points: '0', Due: '2026-10-04', Note: '  ', Old: 'keep' }))
      .toEqual({ Points: 0, Due: '2026-10-04', Note: null, Old: 'keep' });
    expect(buildCardFieldValues(definitions, { Points: '1e-400' })).toEqual({ Points: '1e-400' });
  });

  it('renders configured fields in definition order, including zero, omitting null and hidden fields', () => {
    expect(visibleCardFields(definitions, { Note: 'Private', Due: '2026-10-04', Points: 0 }).map(field => [field.displayName, field.value]))
      .toEqual([['Estimate', '0'], ['Due date', '2026-10-04']]);
    expect(visibleCardFields(definitions, { Points: null, Due: null, Note: 'Private' })).toEqual([]);
  });
});
