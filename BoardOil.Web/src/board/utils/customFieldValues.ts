import type { CustomFieldDefinition, CustomFieldValues } from '../../shared/types/customFieldTypes';
import { customFieldNumberInput } from './customFieldDraft';

export type CustomFieldDisplay = {
  key: string;
  name: string;
  emoji: string | null;
  dataType: CustomFieldDefinition['dataType'];
  value: string;
};

export function formatCustomFieldValue(dataType: CustomFieldDefinition['dataType'], value: string | number) {
  if (dataType === 'date') {
    // ISO date-only values parse as UTC; keep that calendar day in every browser timezone.
    return new Date(String(value)).toLocaleDateString(undefined, { timeZone: 'UTC' });
  }
  return String(value);
}

export function buildCustomFieldValues(definitions: CustomFieldDefinition[], draft: CustomFieldValues): CustomFieldValues {
  const kinds = new Map(definitions.map(field => [field.code, field.dataType]));
  // Keep the draft's keys: a stale editor must fail server validation rather than silently drop values.
  return Object.fromEntries(Object.entries(draft).map(([code, value]) => {
    if (typeof value !== 'string') return [code, value];
    if (kinds.get(code) === 'integer') return [code, customFieldNumberInput(value)];
    return [code, value.trim() || null];
  }));
}

export function visibleCustomFields(definitions: CustomFieldDefinition[], values: CustomFieldValues) {
  return definitions.filter(field => field.showOnCard && values[field.code] !== null
    && values[field.code] !== undefined && values[field.code] !== '')
    .map(field => ({ ...field, key: field.code,
      value: formatCustomFieldValue(field.dataType, values[field.code]!) }));
}
