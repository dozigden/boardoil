import type { CustomFieldDefinition, CustomFieldDefinitionInput } from '../../shared/types/customFieldTypes';

export type CustomFieldDraft = {
  key: string;
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

let nextDraftKey = 0;

export function createCustomFieldDraft(field?: CustomFieldDefinition): CustomFieldDraft {
  return {
    key: `draft-${++nextDraftKey}`,
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
    field.code = name.replace(/[^A-Za-z0-9]/g, '').slice(0, 40);
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
export function buildCustomFieldDefinitions(drafts: CustomFieldDraft[]) {
  const fields: CustomFieldDefinitionInput[] = drafts.map(draft => {
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
  });
  return fields;
}

export function retainCustomFieldErrors(
  errors: Record<string, string[]>, previous: CustomFieldDraft[], current: CustomFieldDraft[]
) {
  const properties: Record<string, keyof CustomFieldDraft> = {
    code: 'code', name: 'name', emoji: 'emoji', dataType: 'dataType',
    defaultValue: 'defaultValue', minLength: 'minimum', maxLength: 'maximum', min: 'minimum', max: 'maximum'
  };
  const retained: Record<string, string[]> = {};
  for (const [path, messages] of Object.entries(errors)) {
    if (path === 'customFields' && previous.length !== current.length) continue;
    const match = /^customFields\[(\d+)\](.*)$/.exec(path);
    if (!match) {
      // Existing-card errors need another backend check; editing appearance cannot resolve them.
      retained[path] = messages;
      continue;
    }
    const before = previous[Number(match[1])];
    const index = current.findIndex(field => field.key === before?.key);
    if (index < 0) continue;
    const after = current[index];
    if (before.dataType !== after.dataType) continue;
    const property = properties[match[2].slice(1)];
    if (property && before[property] !== after[property]) continue;
    retained[`customFields[${index}]${match[2]}`] = messages;
  }
  return retained;
}

export function fieldErrorIndex(path: string, drafts: CustomFieldDraft[]): number | null {
  const definition = /^customFields\[(\d+)\]/.exec(path);
  if (definition) return Number(definition[1]);
  const card = /^cards\[\d+\]\.customFieldValues\.(.+)$/.exec(path);
  if (!card) return null;
  const index = drafts.findIndex(field => field.code.toLowerCase() === card[1].toLowerCase());
  return index < 0 ? null : index;
}

export function describeFieldError(path: string, drafts: CustomFieldDraft[]) {
  const index = fieldErrorIndex(path, drafts);
  const name = index === null ? '' : (drafts[index]?.name.trim() || `Field ${index + 1}`);
  const card = /^cards\[(\d+)\]/.exec(path);
  if (card) return name ? `Card #${card[1]} · ${name}` : `Card #${card[1]}`;
  if (name) {
    const labels: Record<string, string> = {
      code: 'Field code', name: 'Display name', emoji: 'Emoji', dataType: 'Type',
      defaultValue: 'Default value', minLength: 'Minimum length', maxLength: 'Maximum length',
      min: 'Minimum value', max: 'Maximum value'
    };
    const property = path.split('.').pop() ?? '';
    return labels[property] ? `${name} · ${labels[property]}` : name;
  }
  if (path === 'customFields') return 'Custom Fields';
  if (path.toLowerCase() === 'name') return 'Name';
  return '';
}

export function isInlineFieldError(path: string) {
  return /^customFields\[\d+\]\.(code|name|type|emoji|defaultValue|minLength|maxLength|min|max)$/.test(path);
}

// Preserve input that cannot be represented as an exact integer for backend validation.
export function customFieldNumberInput(value: string): number | string | null {
  const text = value.trim();
  if (!text) return null;
  if (!/^[+-]?\d+$/.test(text)) return text;
  const number = Number(text);
  return Number.isSafeInteger(number) ? number : text;
}
