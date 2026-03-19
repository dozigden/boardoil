import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { ConfigurationDto } from '../types/configurationTypes';
import { getEnvelope } from './http';

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

  return {
    getConfiguration
  };
}

export const configurationApi = createConfigurationApi();
