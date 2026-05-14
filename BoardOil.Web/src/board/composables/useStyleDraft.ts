import { computed, ref } from 'vue';
import { buildStylePropertiesJsonFromDraft } from '../../shared/utils/styleDraftAdapter';
import type { BorderMode, StyleDraft, TextColorMode } from '../../shared/utils/styleTypes';
import { DEFAULT_PRESET_INDEX } from '../../shared/utils/presetTheme';

const DEFAULT_BACKGROUND_COLOR = '#69C1CE';
const DEFAULT_TEXT_COLOR = '#111827';
const DEFAULT_BORDER_COLOR = '#D8CDEC';

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

  function setStyleName(styleName: StyleDraft['styleName']) {
    if (!draft.value) {
      return;
    }

    draft.value = {
      ...draft.value,
      styleName,
      textColorMode: 'auto',
      borderMode: 'auto',
      presetIndex: styleName === 'presets' ? DEFAULT_PRESET_INDEX : draft.value.presetIndex,
      backgroundColor: DEFAULT_BACKGROUND_COLOR,
      leftColor: DEFAULT_BACKGROUND_COLOR,
      rightColor: DEFAULT_BACKGROUND_COLOR,
      textColor: DEFAULT_TEXT_COLOR,
      borderColor: DEFAULT_BORDER_COLOR
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

    const borderMode = resolveBorderMode(value);

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

export function parseStyleNameInput(value: string): StyleDraft['styleName'] {
  return isTagStyleName(value) ? value : 'solid';
}

function isTagStyleName(value: string): value is StyleDraft['styleName'] {
  return value === 'solid' || value === 'gradient' || value === 'auto' || value === 'presets';
}

function resolveBorderMode(value: string): BorderMode {
  switch (value) {
    case 'custom':
      return 'custom';
    case 'none':
      return 'none';
    case 'auto':
    default:
      return 'auto';
  }
}
