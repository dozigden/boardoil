export type SystemInfoMessageDto = {
  enabled: boolean;
  emoji: string | null;
  title: string;
  description: string;
  styleName: 'auto' | 'presets' | 'solid';
  stylePropertiesJson: string;
};

export type ConfigurationDto = {
  allowInsecureCookies: boolean;
  mcpPublicBaseUrl: string | null;
};

export type UpdateConfigurationRequest = {
  mcpPublicBaseUrl: string | null;
};
