export type ConfigurationDto = {
  allowInsecureCookies: boolean;
  mcpPublicBaseUrl: string | null;
};

export type UpdateConfigurationRequest = {
  mcpPublicBaseUrl: string | null;
};
