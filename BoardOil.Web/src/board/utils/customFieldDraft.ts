import type { CustomFieldDefinition, CustomFieldDefinitionInput, CustomFieldInput } from '../../shared/types/customFieldTypes';

export type CustomFieldDraft = {
  persisted: boolean;
  code: string;
  codeEdited: boolean;
  name: string;
  emoji: string | null;
  dataType: CustomFieldDefinition['dataType'];
  required: boolean;
  showOnCard: boolean;
  defaultValue: string;
  minimum: string;
  maximum: string;
};

export function createCustomFieldDraft(field?: CustomFieldDefinition): CustomFieldDraft {
  return {
    persisted: field !== undefined,
    code: field?.code ?? '',
    codeEdited: false,
    name: field?.name ?? '',
    emoji: field?.emoji ?? null,
    dataType: field?.dataType ?? 'text',
    required: field ? !field.nullable : false,
    showOnCard: field?.showOnCard ?? false,
    defaultValue: String(field?.defaultValue ?? ''),
    minimum: getMinimum(field),
    maximum: getMaximum(field)
  };
}

export function updateFieldName(field: CustomFieldDraft, name: string) {
  field.name = name;
  if (!field.persisted && !field.codeEdited) {
    field.code = name.replace(/[^A-Za-z0-9]/g, '').toLowerCase().slice(0, 40);
  }
}

function getMinimum(field?: CustomFieldDefinition) {
  if (field?.dataType === 'text') return String(field.minLength ?? '');
  if (field?.dataType === 'integer') return String(field.min ?? '');
  return '';
}

function getMaximum(field?: CustomFieldDefinition) {
  if (field?.dataType === 'text') return String(field.maxLength ?? '');
  if (field?.dataType === 'integer') return String(field.max ?? '');
  return '';
}

export function resetFieldTypeOptions(field: CustomFieldDraft) {
  field.defaultValue = '';
  field.minimum = '';
  field.maximum = '';
}

// Only encode exact decimal integers as numbers; preserve other input for API validation.
export function buildCustomFieldDefinition(draft: CustomFieldDraft): CustomFieldDefinitionInput {
  const common = {
    code: draft.code.trim(),
    name: draft.name.trim(),
    emoji: draft.emoji,
    nullable: !draft.required,
    showOnCard: draft.showOnCard
  };
  switch (draft.dataType) {
    case 'integer':
      return { ...common, dataType: 'integer', defaultValue: customFieldNumberInput(draft.defaultValue),
        min: customFieldNumberInput(draft.minimum), max: customFieldNumberInput(draft.maximum) };
    case 'text':
      return { ...common, dataType: 'text', defaultValue: draft.defaultValue.trim() || null,
        minLength: customFieldNumberInput(draft.minimum), maxLength: customFieldNumberInput(draft.maximum) };
    case 'date':
      return { ...common, dataType: 'date', defaultValue: draft.defaultValue || null };
  }
}

export function retainCustomFieldInputErrors(
  errors: Record<string, string[]>, previous: CustomFieldInput, current: CustomFieldInput
) {
  const retained = { ...errors };
  const before = previous.definition as Record<string, unknown>;
  const after = current.definition as Record<string, unknown>;
  for (const path of Object.keys(retained)) {
    if (path.startsWith('definition.') && (before[path.slice(11)] !== after[path.slice(11)]
      || before.dataType !== after.dataType || before.nullable !== after.nullable)) delete retained[path];
    if (path.startsWith('appliesTo') && JSON.stringify(current.appliesTo) !== JSON.stringify(previous.appliesTo)) delete retained[path];
  }
  return retained;
}

// Preserve input that cannot be represented as an exact integer for backend validation.
export function customFieldNumberInput(value: string): number | string | null {
  const text = value.trim();
  if (!text) return null;
  if (!/^[+-]?\d+$/.test(text)) return text;
  const number = Number(text);
  return Number.isSafeInteger(number) ? number : text;
}
