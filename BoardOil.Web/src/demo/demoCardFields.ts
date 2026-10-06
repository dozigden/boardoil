import type { CardFieldDefinition, CardFieldValues, CardTypeChangePreview, CardTypeChangeRow, CardTypeChangeValue } from '../shared/types/cardFieldTypes';
import type { AppError } from '../shared/types/appError';
import { err, ok, type Result } from '../shared/types/result';

// The standalone demo emulates server rules; the live editor always uses backend validation.
export const demoFieldDefinitions: CardFieldDefinition[] = [
  { type: 'text', fieldCode: 'Reference', displayName: 'Reference', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: 250 },
  { type: 'integer', fieldCode: 'Points', displayName: 'Estimate', emoji: null, nullable: true, showOnCard: true, defaultValue: null, min: 0, max: 100 },
  { type: 'date', fieldCode: 'Due', displayName: 'Due date', emoji: null, nullable: true, showOnCard: true, defaultValue: null }
];

export function demoFieldDefaults(definitions: CardFieldDefinition[]): CardFieldValues {
  return Object.fromEntries(definitions.map(field => [field.fieldCode, field.defaultValue]));
}

function normalise(field: CardFieldDefinition, value: string | number | null) {
  if (field.type === 'text' && typeof value === 'string') return value.trim() || null;
  return value;
}

function valueError(field: CardFieldDefinition, value: string | number | null): string | null {
  if (value === null) return field.nullable ? null : 'Required';
  if (field.type === 'text') {
    if (typeof value !== 'string') return 'Text only';
    if (/[\r\n\u0085\u2028\u2029]/.test(value)) return 'Single line only';
    if (value.length < (field.minLength ?? 0)) return `${field.minLength} characters minimum`;
    if (value.length > (field.maxLength ?? 250)) return `${field.maxLength ?? 250} characters maximum`;
  } else if (field.type === 'integer') {
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

export type DemoFieldLoss = { displayName: string; value: string };

export function planDemoFields(
  source: CardFieldDefinition[], original: CardFieldValues, supplied: CardFieldValues | undefined,
  destination: CardFieldDefinition[], changingType: boolean, description: string, matchByCode = false
): Result<{ fields: CardFieldValues; description: string; losses: DemoFieldLoss[] }, AppError> {
  const errors: Record<string, string[]> = {};
  const values: CardFieldValues = {};
  const input = new Map(Object.entries(supplied ?? original).map(([code, value]) => [code.toLowerCase(), value]));
  for (const code of Object.keys(supplied ?? original)) {
    if (!source.some(field => field.fieldCode.toLowerCase() === code.toLowerCase())) errors[`fields.${code}`] = ['Field is not defined for this card type.'];
  }
  for (const field of source) {
    const value = input.get(field.fieldCode.toLowerCase());
    if (value === undefined) { errors[`fields.${field.fieldCode}`] = ['Field must be supplied, including an explicit null when unset.']; continue; }
    const canonical = normalise(field, value);
    const error = valueError(field, canonical);
    if (error) errors[`fields.${field.fieldCode}`] = [error];
    values[field.fieldCode] = canonical;
  }
  if (Object.keys(errors).length) return err({ kind: 'api', message: 'Validation failed.', validationErrors: errors });
  if (!changingType) return ok({ fields: values, description, losses: [] });

  const fields = demoFieldDefaults(destination);
  const preserved = new Set<string>();
  for (const target of destination) {
    const from = source.find(field => matchByCode
      ? field.fieldCode.toLowerCase() === target.fieldCode.toLowerCase()
      : field.displayName.toLowerCase() === target.displayName.toLowerCase());
    if (!from) continue;
    let candidate = values[from.fieldCode];
    if (candidate !== null && from.type !== target.type) {
      if (target.type === 'text') candidate = String(candidate);
      else if (from.type === 'text' && target.type === 'integer' && /^[+-]?\d+$/.test(String(candidate))) candidate = Number(candidate);
      else continue;
    }
    candidate = normalise(target, candidate);
    if (valueError(target, candidate)) continue;
    fields[target.fieldCode] = candidate;
    preserved.add(from.fieldCode);
  }
  const losses = source.filter(field => !preserved.has(field.fieldCode) && values[field.fieldCode] !== null)
    .map(field => ({ displayName: field.displayName, value: String(values[field.fieldCode]) }));
  return ok({ fields, description, losses });
}

export function previewDemoTypeChange(
  source: { name: string; fields: CardFieldDefinition[] }, original: CardFieldValues,
  supplied: CardFieldValues | undefined, destination: { name: string; fields: CardFieldDefinition[] }, changingType: boolean
): Result<CardTypeChangePreview, AppError> {
  const before = planDemoFields(source.fields, original, supplied, source.fields, false, '');
  if (!before.ok) return before;
  const after = planDemoFields(source.fields, original, supplied, destination.fields, changingType, '');
  if (!after.ok) return after;
  const value = (field: CardFieldDefinition, values: CardFieldValues): CardTypeChangeValue => ({
    displayName: field.displayName, emoji: field.emoji, type: field.type, value: values[field.fieldCode]!
  });
  const rows: CardTypeChangeRow[] = source.fields.map(field => {
    const match = destination.fields.find(target => target.displayName.toLowerCase() === field.displayName.toLowerCase());
    return {
      original: value(field, before.data.fields),
      destination: match ? value(match, after.data.fields) : null,
      losesValue: after.data.losses.some(loss => loss.displayName === field.displayName)
    };
  });
  for (const field of destination.fields) {
    if (!source.fields.some(from => from.displayName.toLowerCase() === field.displayName.toLowerCase())) {
      rows.push({ original: null, destination: value(field, after.data.fields), losesValue: false });
    }
  }
  return ok({
    originalTypeName: source.name, destinationTypeName: destination.name,
    requiresConfirmation: changingType && rows.some(row => !row.original || !row.destination || row.losesValue
      || row.original.type !== row.destination.type || row.original.value !== row.destination.value),
    hasLosses: after.data.losses.length > 0, rows
  });
}
