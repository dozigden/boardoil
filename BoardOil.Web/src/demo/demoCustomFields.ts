import type { CustomFieldDefinition, CustomFieldValues, CardTypeChangePreview, CardTypeChangeRow, CardTypeChangeValue } from '../shared/types/customFieldTypes';
import type { AppError } from '../shared/types/appError';
import { err, ok, type Result } from '../shared/types/result';

// The standalone demo emulates server rules; the live editor always uses backend validation.
export const demoFieldDefinitions: CustomFieldDefinition[] = [
  { dataType: 'text', code: 'Reference', name: 'Reference', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: 250 },
  { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null, nullable: true, showOnCard: true, defaultValue: null, min: 0, max: 100 },
  { dataType: 'date', code: 'Due', name: 'Due date', emoji: null, nullable: true, showOnCard: true, defaultValue: null }
];

export function demoFieldDefaults(definitions: CustomFieldDefinition[]): CustomFieldValues {
  return Object.fromEntries(definitions.map(field => [field.code, field.defaultValue]));
}

function normalise(field: CustomFieldDefinition, value: string | number | null) {
  if (field.dataType === 'text' && typeof value === 'string') return value.trim() || null;
  return value;
}

function valueError(field: CustomFieldDefinition, value: string | number | null): string | null {
  if (value === null) return field.nullable ? null : 'Required';
  if (field.dataType === 'text') {
    if (typeof value !== 'string') return 'Text only';
    if (/[\r\n\u0085\u2028\u2029]/.test(value)) return 'Single line only';
    if (value.length < (field.minLength ?? 0)) return `${field.minLength} characters minimum`;
    if (value.length > (field.maxLength ?? 250)) return `${field.maxLength ?? 250} characters maximum`;
  } else if (field.dataType === 'integer') {
    if (typeof value !== 'number' || !Number.isInteger(value) || value < -2147483648 || value > 2147483647) return 'Whole number required';
    if (value < (field.min ?? -2147483648)) return `${field.min} minimum`;
    if (value > (field.max ?? 2147483647)) return `${field.max} maximum`;
  } else {
    if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith('0000')) return 'Date in YYYY-MM-DD format required';
    const date = new Date(`${value}T00:00:00Z`);
    if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== value) return 'Date in YYYY-MM-DD format required';
  }
  return null;
}

export type DemoFieldLoss = { name: string; value: string };

export function planDemoFields(
  source: CustomFieldDefinition[], original: CustomFieldValues, supplied: CustomFieldValues | undefined,
  destination: CustomFieldDefinition[], changingType: boolean, description: string
): Result<{ customFieldValues: CustomFieldValues; description: string; losses: DemoFieldLoss[] }, AppError> {
  const errors: Record<string, string[]> = {};
  const values: CustomFieldValues = {};
  const input = new Map(Object.entries(supplied ?? original).map(([code, value]) => [code.toLowerCase(), value]));
  for (const code of Object.keys(supplied ?? original)) {
    if (!source.some(field => field.code.toLowerCase() === code.toLowerCase())) errors[`customFieldValues.${code}`] = ['Field is not defined for this card type.'];
  }
  for (const field of source) {
    const value = input.get(field.code.toLowerCase());
    if (value === undefined) { errors[`customFieldValues.${field.code}`] = ['Field must be supplied, including an explicit null when unset.']; continue; }
    const canonical = normalise(field, value);
    const error = valueError(field, canonical);
    if (error) errors[`customFieldValues.${field.code}`] = [error];
    values[field.code] = canonical;
  }
  if (Object.keys(errors).length) return err({ kind: 'api', message: 'Validation failed.', validationErrors: errors });
  if (!changingType) return ok({ customFieldValues: values, description, losses: [] });

  const fields = demoFieldDefaults(destination);
  const preserved = new Set<string>();
  for (const target of destination) {
    const from = source.find(field => field.code.toLowerCase() === target.code.toLowerCase());
    if (!from) continue;
    let candidate = values[from.code];
    if (candidate !== null && from.dataType !== target.dataType) {
      if (target.dataType === 'text') candidate = String(candidate);
      else if (from.dataType === 'text' && target.dataType === 'integer' && /^[+-]?\d+$/.test(String(candidate))) candidate = Number(candidate);
      else continue;
    }
    candidate = normalise(target, candidate);
    if (valueError(target, candidate)) continue;
    fields[target.code] = candidate;
    preserved.add(from.code);
  }
  const losses = source.filter(field => !preserved.has(field.code) && values[field.code] !== null)
    .map(field => ({ name: field.name, value: String(values[field.code]) }));
  return ok({ customFieldValues: fields, description, losses });
}

export function previewDemoTypeChange(
  source: { name: string; definitions: CustomFieldDefinition[] }, original: CustomFieldValues,
  supplied: CustomFieldValues | undefined, destination: { name: string; definitions: CustomFieldDefinition[] }, changingType: boolean
): Result<CardTypeChangePreview, AppError> {
  const before = planDemoFields(source.definitions, original, supplied, source.definitions, false, '');
  if (!before.ok) return before;
  const after = planDemoFields(source.definitions, original, supplied, destination.definitions, changingType, '');
  if (!after.ok) return after;
  const value = (field: CustomFieldDefinition, values: CustomFieldValues): CardTypeChangeValue => ({
    name: field.name, emoji: field.emoji, dataType: field.dataType, value: values[field.code]!
  });
  const rows: CardTypeChangeRow[] = source.definitions.map(field => {
    const match = destination.definitions.find(target => target.code.toLowerCase() === field.code.toLowerCase());
    return {
      original: value(field, before.data.customFieldValues),
      destination: match ? value(match, after.data.customFieldValues) : null,
      losesValue: after.data.losses.some(loss => loss.name === field.name)
    };
  });
  for (const field of destination.definitions) {
    if (!source.definitions.some(from => from.code.toLowerCase() === field.code.toLowerCase())) {
      rows.push({ original: null, destination: value(field, after.data.customFieldValues), losesValue: false });
    }
  }
  return ok({
    originalTypeName: source.name, destinationTypeName: destination.name,
    requiresConfirmation: changingType && rows.some(row => !row.original || !row.destination || row.losesValue
      || row.original.dataType !== row.destination.dataType || row.original.value !== row.destination.value),
    hasLosses: after.data.losses.length > 0, rows
  });
}
