import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { SystemInfoMessageDto } from '../../shared/types/configurationTypes';
import { buildStylePropertiesJsonFromDraft, createRestrictedStyleDraft } from '../../shared/utils/styleDraftAdapter';
import type { StyleDraft } from '../../shared/utils/styleTypes';

export type SystemInfoMessageDraft = {
  enabled: boolean;
  emoji: string | null;
  title: string;
  description: string;
  style: StyleDraft;
};

const allowedStyleNames = ['auto', 'presets', 'solid'] as const;

function draftFromMessage(message: SystemInfoMessageDto | null): SystemInfoMessageDraft {
  return {
    enabled: message?.enabled ?? false,
    emoji: message?.emoji ?? null,
    title: message?.title ?? '',
    description: message?.description ?? '',
    style: createRestrictedStyleDraft(
      message ?? { styleName: 'presets', stylePropertiesJson: '{"presetIndex":2}' },
      new Set(allowedStyleNames),
      { styleName: 'auto', stylePropertiesJson: '{}' }
    )
  };
}

export function systemInfoMessageForm(
  message: SystemInfoMessageDto | null,
  save: FormDefinition<SystemInfoMessageDto, SystemInfoMessageDto | null>['submit']
) {
  return defineForm<SystemInfoMessageDraft, SystemInfoMessageDto | null>({
    title: 'System Info Message',
    submitLabel: 'Save',
    initialValues: () => draftFromMessage(message),
    savedValues: draftFromMessage,
    fields: [
      {
        name: 'enabled',
        label: 'Enabled',
        type: 'checkbox',
        clearFields: ['title'],
        errorFields: ['systemInfoMessage.enabled']
      },
      {
        name: 'emoji',
        label: 'Emoji',
        type: 'emoji',
        errorFields: ['systemInfoMessage.emoji']
      },
      {
        name: 'title',
        label: 'Title',
        type: 'text',
        placeholder: 'Maintenance update',
        autocomplete: 'off',
        spellcheck: false,
        errorFields: ['systemInfoMessage.title']
      },
      {
        name: 'description',
        label: 'Description',
        type: 'textarea',
        rows: 8,
        errorFields: ['systemInfoMessage.description']
      },
      {
        name: 'style',
        label: 'Style',
        type: 'style',
        allowedStyleNames,
        errorFields: ['systemInfoMessage.styleName', 'systemInfoMessage.stylePropertiesJson']
      }
    ],
    submit: values => save({
      enabled: values.enabled,
      emoji: values.emoji?.trim() || null,
      title: values.title.trim(),
      description: values.description,
      styleName: values.style.styleName === 'gradient' ? 'presets' : values.style.styleName,
      stylePropertiesJson: buildStylePropertiesJsonFromDraft(values.style)
    })
  });
}
