import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { ThemeMode } from '../../shared/stores/themeStore';
import { slickEffectOptions, type SlickEffect } from '../../shared/types/slickEffect';

export type ThemeDraft = { mode: ThemeMode; slickEffect: SlickEffect };

export const themeChoices = [
  {
    name: 'mode' as const,
    label: 'Appearance',
    options: [
      { value: 'system' as const, label: 'System' },
      { value: 'light' as const, label: 'Light' },
      { value: 'dark' as const, label: 'Dark' }
    ]
  },
  { name: 'slickEffect' as const, label: 'Slick effect', options: slickEffectOptions }
];

export function createUserThemeForm(mode: ThemeMode, slickEffect: SlickEffect, save: FormDefinition<ThemeDraft, ThemeDraft>['submit']) {
  return defineForm<ThemeDraft, ThemeDraft>({
    title: 'Theme',
    submitLabel: 'Save',
    initialValues: () => ({ mode, slickEffect }),
    fields: themeChoices.map(choice => ({ ...choice, type: 'radio' })),
    savedValues: saved => saved,
    submit: save
  });
}
