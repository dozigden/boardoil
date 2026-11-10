import { computed, getCurrentScope, onScopeDispose, ref, shallowRef, toRaw } from 'vue';
import { useValidationStore } from '../stores/validationStore';
import { err, type Result } from '../types/result';
import type { FormAction, FormDefinition, FormError } from './formDefinition';

export function useForm<T extends object, R>(definition: FormDefinition<T, R>) {
  const validation = useValidationStore();
  const clone = (value: T): T => structuredClone(toRaw(value));
  const initial = shallowRef(clone(definition.initialValues()));
  const draft = shallowRef<T>(clone(initial.value));
  const busy = ref(false);
  const fields = shallowRef(definition.fields);
  const reassessing = ref(false);
  const reassessmentError = ref('');
  const locked = computed(() => busy.value || reassessing.value);
  let disposed = false;
  if (getCurrentScope()) onScopeDispose(() => { disposed = true; });
  const dirty = computed(() => JSON.stringify(draft.value) !== JSON.stringify(initial.value));
  const canSubmit = computed(() => !locked.value && !reassessmentError.value && (definition.canSubmit?.(draft.value) ?? true));
  const isCurrent = () => definition.isCurrent?.() ?? true;

  function setValue<K extends Extract<keyof T, string>>(field: K, value: T[K]) {
    if (locked.value) return;
    if (Object.is(draft.value[field], value)) return;
    draft.value = { ...draft.value, [field]: value };
    const config = fields.value.find(candidate => candidate.name === field);
    const corrected = [field, ...(config?.clearFields ?? [])];
    for (const name of corrected) {
      const correctedField = fields.value.find(candidate => candidate.name === name);
      for (const errorField of [name, ...(correctedField?.errorFields ?? [])]) {
        validation.clearFieldErrors(errorField, definition.category);
      }
    }
    if (config?.reassessOnChange) void reassess();
  }

  function reset() {
    if (locked.value) return;
    draft.value = clone(initial.value);
    validation.clearValidation();
    void runReassessment(true);
  }

  async function runReassessment(updateBaseline = false) {
    if (!definition.reassess || locked.value || disposed || !isCurrent()) return;
    reassessing.value = true;
    reassessmentError.value = '';
    try {
      const assessment = await definition.reassess(clone(draft.value));
      if (disposed || !isCurrent()) return;
      draft.value = clone(assessment.values);
      if (assessment.fields) fields.value = assessment.fields;
      reassessmentError.value = assessment.error ?? '';
      if (updateBaseline) initial.value = clone(draft.value);
    } finally {
      reassessing.value = false;
    }
  }

  function reassess() {
    return runReassessment();
  }

  async function runOperation<V>(operation: () => Promise<Result<V, FormError>>, confirm?: () => Promise<boolean>) {
    if (locked.value || !isCurrent()) return;
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
      if (result.ok && isCurrent()) {
        if (definition.savedValues) draft.value = clone(definition.savedValues(result.data));
        initial.value = clone(draft.value);
      }
      return result;
    });
  }

  function runAction(action: FormAction<T>) {
    return runOperation(() => action.run(clone(draft.value)), action.confirm);
  }

  void runReassessment(true);

  return { draft, fields, busy, locked, reassessing, reassessmentError, dirty, canSubmit, setValue, reset, reassess, submit, runAction };
}
