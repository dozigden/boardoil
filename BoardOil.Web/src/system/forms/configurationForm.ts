import { defineForm, type FormDefinition } from '../../shared/forms/formDefinition';
import type { ConfigurationDto, UpdateConfigurationRequest } from '../../shared/types/configurationTypes';

type ConfigurationDraft = {
  mcpPublicBaseUrl: string;
  oauthLifecycleDiagnosticsEnabled: boolean;
  systemTimeZoneId: string;
};

function draftFromConfiguration(configuration: ConfigurationDto): ConfigurationDraft {
  return {
    mcpPublicBaseUrl: configuration.mcpPublicBaseUrl ?? '',
    oauthLifecycleDiagnosticsEnabled: configuration.oauthLifecycleDiagnosticsEnabled,
    systemTimeZoneId: configuration.systemTimeZoneId
  };
}

export function configurationForm(
  configuration: ConfigurationDto,
  timeZones: readonly { value: string; label: string }[],
  save: FormDefinition<UpdateConfigurationRequest, ConfigurationDto>['submit']
) {
  return defineForm<ConfigurationDraft, ConfigurationDto>({
    title: 'Configuration', submitLabel: 'Save',
    initialValues: () => draftFromConfiguration(configuration),
    savedValues: draftFromConfiguration,
    fields: [
      {
        name: 'oauthLifecycleDiagnosticsEnabled', label: 'Log OAuth requests', type: 'checkbox',
        help: 'When enabled, BoardOil retains OAuth identities, requested scopes, hashed token fingerprints, trace identifiers, and user-agent metadata.'
      },
      {
        name: 'mcpPublicBaseUrl', label: 'MCP public base URL', type: 'text', clearable: true,
        placeholder: 'https://boardoil.example.com', autocomplete: 'off', spellcheck: false,
        help: 'Leave blank to use automatic relative discovery URLs, recommended for Docker and proxy setups.'
      },
      {
        name: 'systemTimeZoneId', label: 'System timezone', type: 'searchableSelect', options: timeZones,
        help: 'Used for scheduled job run times. Changing it resets schedule catch-up from now.'
      }
    ],
    submit: values => save(values)
  });
}
