import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { ThemeMode } from '../../shared/stores/themeStore';

type ThemeDraft = { mode: ThemeMode };

export function createUserThemeForm(mode: ThemeMode, save: FormDefinition<ThemeDraft, ThemeDraft>['submit']) {
  return defineForm<ThemeDraft, ThemeDraft>({
    title: 'Theme',
    submitLabel: 'Save',
    initialValues: () => ({ mode }),
    fields: [{
      name: 'mode', label: 'Appearance mode', type: 'radio',
      options: [
        { value: 'system', label: 'System' },
        { value: 'light', label: 'Light' },
        { value: 'dark', label: 'Dark' }
      ]
    }],
    savedValues: saved => saved,
    submit: save
  });
}
