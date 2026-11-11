import { Trash2 } from '@lucide/vue';
import { defineForm, type FormError } from '../../shared/forms/formDefinition';
import type { Tag, TagEditModel, StyleDefault } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';
import { buildStylePropertiesJsonFromDraft, createStyleDraft } from '../../shared/utils/styleDraftAdapter';
import { DEFAULT_TAG_STYLE_PROPERTIES_JSON } from '../../shared/utils/tagStyles';
import type { StyleDraft } from '../../shared/utils/styleTypes';

export type TagDraft = {
  name: string;
  emoji: string | null;
  style: StyleDraft
};
const fallbackStyle: StyleDefault = { styleName: 'presets', stylePropertiesJson: DEFAULT_TAG_STYLE_PROPERTIES_JSON };

type TagOperations = {
  loadDefaultStyle: () => Promise<StyleDefault | null>;
  save: (model: TagEditModel) => Promise<Result<Tag, FormError>>;
  delete?: {
    run: () => Promise<Result<void, FormError>>;
    confirm: () => Promise<boolean>;
  };
  isCurrent: () => boolean;
};

export function createTagForm(tag: Tag | null, operations: TagOperations) {
  return defineForm<TagDraft, Tag>({
    title: tag ? `Edit Tag: ${tag.name}` : 'Add Tag',
    submitLabel: tag ? 'Save tag style' : 'Create tag',
    submitText: tag ? 'Save' : 'Create',
    cancelLabel: 'Cancel editing',
    closeLabel: tag ? 'Cancel editing' : 'Cancel creating',
    initialValues: () => ({
      name: tag?.name ?? '',
      emoji: tag?.emoji ?? null,
      style: createStyleDraft(tag ?? fallbackStyle)
    }),
    fields: [
      {
        name: 'name',
        label: 'Name',
        type: 'text',
        autocomplete: 'off',
        placeholder: tag ? 'Tag name' : 'New tag name'
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
    reassess: tag ? undefined : async values => {
      const defaultStyle = await operations.loadDefaultStyle();
      return { values: { ...values, style: createStyleDraft(defaultStyle ?? fallbackStyle) } };
    },
    isCurrent: operations.isCurrent,
    submit: values => operations.save({
      name: values.name,
      emoji: values.emoji,
      styleName: values.style.styleName,
      stylePropertiesJson: buildStylePropertiesJsonFromDraft(values.style)
    }),
    actions: tag && operations.delete ? [{
      name: 'delete',
      label: 'Delete tag',
      icon: Trash2,
      danger: true,
      confirm: operations.delete.confirm,
      run: operations.delete.run
    }] : []
  });
}
