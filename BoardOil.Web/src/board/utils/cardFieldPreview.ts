import { cardFieldNumberInput, type CardFieldDraft } from './cardFieldDraft';
import { formatCardFieldValue, type CardFieldDisplay } from './cardFieldValues';

export function previewCardFields(drafts: CardFieldDraft[], today = new Date()): CardFieldDisplay[] {
  return drafts.filter(field => field.showOnCard).map(field => {
    let value: string | number = field.defaultValue.trim();
    switch (field.type) {
      case 'text':
        value = value || 'example';
        break;
      case 'integer':
        value = cardFieldNumberInput(value) ?? 12345;
        break;
      case 'date':
        value = value || `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        break;
    }
    return { key: field.key, displayName: field.displayName, emoji: field.emoji,
      type: field.type, value: formatCardFieldValue(field.type, value) };
  });
}
