import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { ConfigurationDto, UpdateConfigurationRequest } from '../types/configurationTypes';
import { getEnvelope, putData } from './http';

export function createConfigurationApi() {
  async function getConfiguration(): Promise<Result<ConfigurationDto, AppError>> {
    const result = await getEnvelope<ConfigurationDto>('/api/configuration');
    if (!result.ok) {
      return result;
    }

    if (!result.data.data) {
      return err({
        kind: 'api',
        message: result.data.message ?? 'Failed to load configuration.'
      });
    }

    return ok(result.data.data);
  }

  async function updateConfiguration(request: UpdateConfigurationRequest): Promise<Result<ConfigurationDto, AppError>> {
    return putData<ConfigurationDto>('/api/configuration', request);
  }

  return {
    getConfiguration,
    updateConfiguration
  };
}

export const configurationApi = createConfigurationApi();
