import { customFieldNumberInput, type CustomFieldDraft } from './customFieldDraft';
import { formatCustomFieldValue, type CustomFieldDisplay } from './customFieldValues';

export function previewCustomFields(drafts: CustomFieldDraft[], today = new Date()): CustomFieldDisplay[] {
  return drafts.filter(field => field.showOnCard).map(field => {
    let value: string | number = field.defaultValue.trim();
    switch (field.dataType) {
      case 'text':
        value = value || 'example';
        break;
      case 'integer':
        value = customFieldNumberInput(value) ?? 12345;
        break;
      case 'date':
        value = value || `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        break;
    }
    return { key: field.key, name: field.name, emoji: field.emoji,
      dataType: field.dataType, value: formatCustomFieldValue(field.dataType, value) };
  });
}
