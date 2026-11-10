import { computed, ref, shallowRef, toRaw } from 'vue';
import { useValidationStore } from '../stores/validationStore';
import { err, type Result } from '../types/result';
import type { FormAction, FormDefinition, FormError } from './formDefinition';

export function useForm<T extends object, R>(definition: FormDefinition<T, R>) {
  const validation = useValidationStore();
  const clone = (value: T): T => structuredClone(toRaw(value));
  const initial = shallowRef(clone(definition.initialValues()));
  const draft = shallowRef<T>(clone(initial.value));
  const busy = ref(false);
  const dirty = computed(() => JSON.stringify(draft.value) !== JSON.stringify(initial.value));
  const canSubmit = computed(() => !busy.value && (definition.canSubmit?.(draft.value) ?? true));
  const isCurrent = () => definition.isCurrent?.() ?? true;

  function setValue<K extends Extract<keyof T, string>>(field: K, value: T[K]) {
    if (busy.value) return;
    draft.value = { ...draft.value, [field]: value };
    const config = definition.fields.find(candidate => candidate.name === field);
    const corrected = [field, ...(config?.clearFields ?? [])];
    for (const name of corrected) {
      const correctedField = definition.fields.find(candidate => candidate.name === name);
      for (const errorField of [name, ...(correctedField?.errorFields ?? [])]) {
        validation.clearFieldErrors(errorField, definition.category);
      }
    }
  }

  function reset() {
    if (busy.value) return;
    initial.value = clone(definition.initialValues());
    draft.value = clone(initial.value);
    validation.clearValidation();
  }

  async function runOperation<V>(operation: () => Promise<Result<V, FormError>>, confirm?: () => Promise<boolean>) {
    if (busy.value || !isCurrent()) return;
    busy.value = true;
    try {
      if (confirm && !await confirm()) return;
      if (!isCurrent()) return;
      validation.clearValidation();
      const result = await operation();
      if (!isCurrent()) return;
      validation.setValidationResult(result, definition.category);
      return result;
    } finally {
      busy.value = false;
    }
  }

  async function submit() {
    if (!canSubmit.value) return;
    return runOperation(async () => {
      const values = clone(draft.value);
      draft.value = definition.normalize ? definition.normalize(values) : values;
      const localErrors: Partial<Record<string, string[]>> = definition.validate?.(draft.value) ?? {};
      const errors: Record<string, string[]> = {};
      for (const [field, messages] of Object.entries(localErrors)) {
        if (messages?.length) errors[field] = messages;
      }
      if (Object.keys(errors).length > 0) {
        return err({ message: 'Validation failed.', validationErrors: errors });
      }
      const result = await definition.submit(clone(draft.value));
      if (result.ok) initial.value = clone(draft.value);
      return result;
    });
  }

  function runAction(action: FormAction<T>) {
    return runOperation(() => action.run(clone(draft.value)), action.confirm);
  }

  return { draft, busy, dirty, canSubmit, setValue, reset, submit, runAction };
}
