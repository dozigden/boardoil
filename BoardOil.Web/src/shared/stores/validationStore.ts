import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

type ValidationFeedback = { category: string; error: AppError };

export const useValidationStore = defineStore('validation', () => {
  const validation = ref<ValidationFeedback | null>(null);

  function SET_VALIDATION(error: AppError | null, category = '') {
    validation.value = error ? { category, error } : null;
  }

  // One current submission result. Categories filter its display, not its storage.
  function setValidationResult<T>(result: Result<T, AppError>, category = '') {
    if (result.ok) {
      SET_VALIDATION(null);
      return;
    }

    SET_VALIDATION(result.error, category);
  }

  function clearValidation() {
    SET_VALIDATION(null);
  }

  function getValidationError(category: string): AppError | undefined {
    if (category && validation.value?.category !== category) {
      return undefined;
    }
    return validation.value?.error;
  }

  function getFieldErrors(field: string, category = ''): string[] {
    return getValidationError(category)?.validationErrors?.[field] ?? [];
  }

  function getValidationMessages(renderedFields: readonly string[] = [], category = ''): string[] {
    const error = getValidationError(category);
    if (!error) {
      return [];
    }

    const fields = Object.entries(error.validationErrors ?? {}).filter(([, messages]) => messages.length > 0);
    if (fields.length === 0) {
      return [error.message];
    }

    return [...new Set(fields
      .filter(([field]) => !renderedFields.includes(field))
      .flatMap(([, messages]) => messages))];
  }

  function clearFieldErrors(field: string, category = '') {
    const error = getValidationError(category);
    if (!error?.validationErrors?.[field]?.length) {
      return;
    }

    const remaining = { ...error.validationErrors };
    delete remaining[field];
    if (Object.values(remaining).every(messages => messages.length === 0)) {
      SET_VALIDATION(null);
      return;
    }

    SET_VALIDATION({ ...error, validationErrors: remaining }, validation.value?.category);
  }

  return {
    validation,
    setValidationResult,
    clearValidation,
    getFieldErrors,
    getValidationMessages,
    clearFieldErrors
  };
});
