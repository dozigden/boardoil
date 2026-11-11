import { Trash2 } from '@lucide/vue';
import { defineForm, type FormError } from '../../shared/forms/formDefinition';
import type { CardType, CardTypeEditModel } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';
import { buildStylePropertiesJsonFromDraft, createCardTypeStyleDraft } from '../../shared/utils/cardTypeStyles';
import type { StyleDraft } from '../../shared/utils/styleTypes';

export type CardTypeDraft = {
  name: string;
  emoji: string | null;
  style: StyleDraft
};

type CardTypeOperations = {
  save: (model: CardTypeEditModel) => Promise<Result<CardType, FormError>>;
  delete?: {
    run: () => Promise<Result<void, FormError>>;
    confirm: () => Promise<boolean>;
  };
  isCurrent: () => boolean;
};

export function createCardTypeForm(cardType: CardType | null, operations: CardTypeOperations) {
  return defineForm<CardTypeDraft, CardType>({
    title: cardType ? `Edit Card Type: ${cardType.name}` : 'Add Card Type',
    submitLabel: cardType ? 'Save card type' : 'Create card type',
    submitText: cardType ? 'Save' : 'Create',
    cancelLabel: 'Cancel editing',
    closeLabel: cardType ? 'Cancel editing' : 'Cancel creating',
    initialValues: () => ({
      name: cardType?.name ?? '',
      emoji: cardType?.emoji ?? null,
      style: createCardTypeStyleDraft(cardType ?? { styleName: 'auto', stylePropertiesJson: '{}' })
    }),
    fields: [
      {
        name: 'name',
        label: 'Name',
        type: 'text',
        autocomplete: 'off',
        placeholder: cardType ? 'Card type name' : 'New card type name'
      },
      {
        name: 'emoji',
        label: 'Emoji',
        type: 'emoji'
      },
      {
        name: 'style',
        label: 'Style',
        type: 'style',
        errorFields: ['styleName', 'stylePropertiesJson']
      }
    ],
    isCurrent: operations.isCurrent,
    submit: values => operations.save({
      name: values.name,
      emoji: values.emoji,
      styleName: values.style.styleName,
      stylePropertiesJson: buildStylePropertiesJsonFromDraft(values.style)
    }),
    actions: cardType && !cardType.isSystem && operations.delete ? [{
      name: 'delete',
      label: 'Delete card type',
      icon: Trash2,
      danger: true,
      confirm: operations.delete.confirm,
      run: operations.delete.run
    }] : []
  });
}
