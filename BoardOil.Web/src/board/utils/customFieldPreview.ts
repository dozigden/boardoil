import type { CustomFieldDefinition } from '../../shared/types/customFieldTypes';
import { formatCustomFieldValue, type CustomFieldDisplay } from './customFieldValues';

export function previewCustomFields(definitions: CustomFieldDefinition[], today = new Date()): CustomFieldDisplay[] {
  return definitions.filter(field => field.showOnCard).map(field => {
    let value: string | number;
    switch (field.dataType) {
      case 'text':
        value = field.defaultValue ?? 'example';
        break;
      case 'integer':
        value = field.defaultValue ?? 12345;
        break;
      case 'date':
        value = field.defaultValue ?? `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        break;
    }
    return { key: field.code, name: field.name, emoji: field.emoji,
      dataType: field.dataType, value: formatCustomFieldValue(field.dataType, value) };
  });
}
