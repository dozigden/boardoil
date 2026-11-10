import type { ComputedRef, InjectionKey } from 'vue';

export const formValidationContext: InjectionKey<{
  category: ComputedRef<string>;
  registerField: (field: string) => () => void;
}> = Symbol('formValidation');
