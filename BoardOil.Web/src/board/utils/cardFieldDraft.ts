import type { CardFieldDefinition, CardFieldDefinitionInput } from '../../shared/types/cardFieldTypes';

export type CardFieldDraft = {
  key: string;
  persisted: boolean;
  fieldCode: string;
  fieldCodeEdited: boolean;
  displayName: string;
  emoji: string | null;
  type: CardFieldDefinition['type'];
  required: boolean;
  showOnCard: boolean;
  defaultValue: string;
  minimum: string;
  maximum: string;
};

let nextDraftKey = 0;

export function createCardFieldDraft(field?: CardFieldDefinition): CardFieldDraft {
  return {
    key: `draft-${++nextDraftKey}`,
    persisted: field !== undefined,
    fieldCode: field?.fieldCode ?? '',
    fieldCodeEdited: false,
    displayName: field?.displayName ?? '',
    emoji: field?.emoji ?? null,
    type: field?.type ?? 'text',
    required: field ? !field.nullable : false,
    showOnCard: field?.showOnCard ?? false,
    defaultValue: String(field?.defaultValue ?? ''),
    minimum: getMinimum(field),
    maximum: getMaximum(field)
  };
}

export function updateFieldDisplayName(field: CardFieldDraft, name: string) {
  field.displayName = name;
  if (!field.persisted && !field.fieldCodeEdited) {
    field.fieldCode = name.replace(/[^A-Za-z0-9]/g, '').slice(0, 40);
  }
}

function getMinimum(field?: CardFieldDefinition) {
  if (field?.type === 'text') return String(field.minLength ?? '');
  if (field?.type === 'integer') return String(field.min ?? '');
  return '';
}

function getMaximum(field?: CardFieldDefinition) {
  if (field?.type === 'text') return String(field.maxLength ?? '');
  if (field?.type === 'integer') return String(field.max ?? '');
  return '';
}

export function resetFieldTypeOptions(field: CardFieldDraft) {
  field.defaultValue = '';
  field.minimum = '';
  field.maximum = '';
}

// Only encode exact decimal integers as numbers; preserve other input for API validation.
export function buildCardFieldDefinitions(drafts: CardFieldDraft[]) {
  function numberInput(value: string): number | string | null {
    const text = value.trim();
    if (!text) return null;
    if (!/^[+-]?\d+$/.test(text)) return text;
    const number = Number(text);
    return Number.isSafeInteger(number) ? number : text;
  }

  const fields: CardFieldDefinitionInput[] = drafts.map(draft => {
    const common = {
      fieldCode: draft.fieldCode.trim(),
      displayName: draft.displayName.trim(),
      emoji: draft.emoji,
      nullable: !draft.required,
      showOnCard: draft.showOnCard
    };
    switch (draft.type) {
      case 'integer':
        return { ...common, type: 'integer', defaultValue: numberInput(draft.defaultValue),
          min: numberInput(draft.minimum), max: numberInput(draft.maximum) };
      case 'text':
        return { ...common, type: 'text', defaultValue: draft.defaultValue.trim() || null,
          minLength: numberInput(draft.minimum), maxLength: numberInput(draft.maximum) };
      case 'date':
        return { ...common, type: 'date', defaultValue: draft.defaultValue || null };
    }
  });
  return fields;
}

export function retainCardFieldErrors(
  errors: Record<string, string[]>, previous: CardFieldDraft[], current: CardFieldDraft[]
) {
  const properties: Record<string, keyof CardFieldDraft> = {
    fieldCode: 'fieldCode', displayName: 'displayName', emoji: 'emoji', type: 'type',
    defaultValue: 'defaultValue', minLength: 'minimum', maxLength: 'maximum', min: 'minimum', max: 'maximum'
  };
  const retained: Record<string, string[]> = {};
  for (const [path, messages] of Object.entries(errors)) {
    if (path === 'fields' && previous.length !== current.length) continue;
    const match = /^fields\[(\d+)\](.*)$/.exec(path);
    if (!match) {
      // Existing-card errors need another backend check; editing appearance cannot resolve them.
      retained[path] = messages;
      continue;
    }
    const before = previous[Number(match[1])];
    const index = current.findIndex(field => field.key === before?.key);
    if (index < 0) continue;
    const after = current[index];
    if (before.type !== after.type) continue;
    const property = properties[match[2].slice(1)];
    if (property && before[property] !== after[property]) continue;
    retained[`fields[${index}]${match[2]}`] = messages;
  }
  return retained;
}

export function fieldErrorIndex(path: string, drafts: CardFieldDraft[]): number | null {
  const definition = /^fields\[(\d+)\]/.exec(path);
  if (definition) return Number(definition[1]);
  const card = /^cards\[\d+\]\.fields\.(.+)$/.exec(path);
  if (!card) return null;
  const index = drafts.findIndex(field => field.fieldCode.toLowerCase() === card[1].toLowerCase());
  return index < 0 ? null : index;
}

export function describeFieldError(path: string, drafts: CardFieldDraft[]) {
  const index = fieldErrorIndex(path, drafts);
  const name = index === null ? '' : (drafts[index]?.displayName.trim() || `Field ${index + 1}`);
  const card = /^cards\[(\d+)\]/.exec(path);
  if (card) return name ? `Card #${card[1]} · ${name}` : `Card #${card[1]}`;
  if (name) {
    const labels: Record<string, string> = {
      fieldCode: 'Field code', displayName: 'Display name', emoji: 'Emoji', type: 'Type',
      defaultValue: 'Default value', minLength: 'Minimum length', maxLength: 'Maximum length',
      min: 'Minimum value', max: 'Maximum value'
    };
    const property = path.split('.').pop() ?? '';
    return labels[property] ? `${name} · ${labels[property]}` : name;
  }
  if (path === 'fields') return 'Custom Fields';
  if (path.toLowerCase() === 'name') return 'Name';
  return '';
}

export function isInlineFieldError(path: string) {
  return /^fields\[\d+\]\.(fieldCode|displayName|type|emoji|defaultValue|minLength|maxLength|min|max)$/.test(path);
}
