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
  oauthLifecycleDiagnosticsEnabled: boolean;
  systemTimeZoneId: string;
  oauthLifecycleDiagnosticsRetentionDays: number;
};

export type UpdateConfigurationRequest = {
  mcpPublicBaseUrl: string | null;
  oauthLifecycleDiagnosticsEnabled: boolean;
  systemTimeZoneId: string;
};
