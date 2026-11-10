import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { OwnUserProfile, UserProfileEditModel } from '../../shared/types/authTypes';

export function createUserProfileForm(profile: OwnUserProfile, save: FormDefinition<UserProfileEditModel, OwnUserProfile>['submit']) {
  return defineForm<UserProfileEditModel, OwnUserProfile>({
    title: 'Profile',
    submitLabel: 'Save profile',
    initialValues: () => profileValues(profile),
    fields: [
      { name: 'displayName', label: 'Display name', type: 'text', autocomplete: 'name' },
      { name: 'email', label: 'Email', type: 'email', autocomplete: 'email' }
    ],
    savedValues: profileValues,
    submit: save
  });
}

function profileValues(profile: OwnUserProfile): UserProfileEditModel {
  return { displayName: profile.displayName, email: profile.email };
}
