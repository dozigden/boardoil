import type { CardFieldDefinition, CardFieldValues } from '../../shared/types/cardFieldTypes';
import { cardFieldNumberInput } from './cardFieldDraft';

export function buildCardFieldValues(definitions: CardFieldDefinition[], draft: CardFieldValues): CardFieldValues {
  const kinds = new Map(definitions.map(field => [field.fieldCode, field.type]));
  // Keep the draft's keys: a stale editor must fail server validation rather than silently drop values.
  return Object.fromEntries(Object.entries(draft).map(([code, value]) => {
    if (typeof value !== 'string') return [code, value];
    if (kinds.get(code) === 'integer') return [code, cardFieldNumberInput(value)];
    return [code, value.trim() || null];
  }));
}

export function visibleCardFields(definitions: CardFieldDefinition[], values: CardFieldValues) {
  return definitions.filter(field => field.showOnCard && values[field.fieldCode] !== null
    && values[field.fieldCode] !== undefined && values[field.fieldCode] !== '')
    .map(field => ({ ...field, value: String(values[field.fieldCode]) }));
}
