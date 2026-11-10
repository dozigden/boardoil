import { Trash2 } from '@lucide/vue';
import { defineForm, type FormError } from '../../shared/forms/formDefinition';
import type { Slick, SlickEditModel, StyleDefault } from '../../shared/types/boardTypes';
import type { Result } from '../../shared/types/result';
import { SLICK_PRESET_TOKENS } from '../../shared/utils/presetTheme';
import { buildStylePropertiesJsonFromDraft, createRestrictedStyleDraft } from '../../shared/utils/styleDraftAdapter';
import type { StyleDraft } from '../../shared/utils/styleTypes';

export type SlickDraft = { name: string; style: StyleDraft };
const allowedStyleNames = ['solid', 'presets'] as const;
const fallbackStyle: StyleDefault = { styleName: 'presets', stylePropertiesJson: '{"presetIndex":2}' };

type SlickOperations = {
  loadDefaultStyle: () => Promise<StyleDefault | null>;
  save: (model: SlickEditModel) => Promise<Result<Slick, FormError>>;
  delete?: {
    run: () => Promise<Result<void, FormError>>;
    confirm: () => Promise<boolean>;
  };
  isCurrent: () => boolean;
};

export function createSlickForm(slick: Slick | null, operations: SlickOperations) {
  return defineForm<SlickDraft, Slick>({
    title: slick ? `Edit Slick: ${slick.name}` : 'Add Slick',
    submitLabel: slick ? 'Save slick' : 'Create slick',
    submitText: slick ? 'Save' : 'Create',
    cancelLabel: 'Cancel editing',
    closeLabel: slick ? 'Cancel editing' : 'Cancel creating',
    initialValues: () => ({
      name: slick?.name ?? '',
      style: createRestrictedStyleDraft(slick ?? fallbackStyle, new Set(allowedStyleNames), fallbackStyle)
    }),
    fields: [
      { name: 'name', label: 'Name', type: 'text', autocomplete: 'off', placeholder: slick ? 'Slick name' : 'New slick name' },
      { name: 'style', label: 'Style', type: 'style', allowedStyleNames, presetColours: SLICK_PRESET_TOKENS,
        errorFields: ['styleName', 'stylePropertiesJson'] }
    ],
    reassess: slick ? undefined : async values => {
      const defaultStyle = await operations.loadDefaultStyle();
      return { values: {
        ...values,
        style: createRestrictedStyleDraft(defaultStyle ?? fallbackStyle, new Set(allowedStyleNames), fallbackStyle)
      } };
    },
    isCurrent: operations.isCurrent,
    submit: values => operations.save({
      name: values.name,
      styleName: values.style.styleName === 'solid' ? 'solid' : 'presets',
      stylePropertiesJson: buildStylePropertiesJsonFromDraft(values.style)
    }),
    actions: slick && operations.delete ? [{
      name: 'delete', label: 'Delete slick', icon: Trash2, danger: true,
      confirm: operations.delete.confirm, run: operations.delete.run
    }] : []
  });
}
