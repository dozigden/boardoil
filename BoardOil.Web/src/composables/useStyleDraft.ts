import { computed, ref } from 'vue';
import { buildStylePropertiesJsonFromDraft, type BorderMode, type StyleDraft, type TextColorMode } from '../utils/stylePresentation';

export function useStyleDraft(initialDraft: StyleDraft | null = null) {
  const draft = ref<StyleDraft | null>(initialDraft);

  const stylePropertiesJson = computed(() => {
    if (!draft.value) {
      return null;
    }

    return buildStylePropertiesJsonFromDraft(draft.value);
  });

  function setDraft(nextDraft: StyleDraft | null) {
    draft.value = nextDraft;
  }

  function clearDraft() {
    draft.value = null;
  }

  function setStyleName(value: string) {
    if (!draft.value) {
      return;
    }

    draft.value = {
      ...draft.value,
      styleName: value === 'gradient' ? 'gradient' : 'solid'
    };
  }

  function setTextMode(value: string) {
    if (!draft.value) {
      return;
    }

    const textColorMode: TextColorMode = value === 'custom' ? 'custom' : 'auto';
    draft.value = {
      ...draft.value,
      textColorMode
    };
  }

  function setBorderMode(value: string) {
    if (!draft.value) {
      return;
    }

    const borderMode: BorderMode = value === 'custom'
      ? 'custom'
      : value === 'none'
        ? 'none'
        : 'auto';

    draft.value = {
      ...draft.value,
      borderMode
    };
  }

  function setField<Key extends keyof StyleDraft>(field: Key, value: StyleDraft[Key]) {
    if (!draft.value) {
      return;
    }

    draft.value = {
      ...draft.value,
      [field]: value
    };
  }

  return {
    draft,
    stylePropertiesJson,
    setDraft,
    clearDraft,
    setStyleName,
    setTextMode,
    setBorderMode,
    setField
  };
}
