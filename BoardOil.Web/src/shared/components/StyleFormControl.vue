<template>
  <div v-if="draftStyle" class="style-control">
    <select aria-label="Style" :value="draftStyle.styleName" :disabled="disabled" @change="onStyleNameChange">
      <option v-for="name in allowedStyleNames" :key="name" :value="name">{{ styleLabels[name] }}</option>
    </select>

    <template v-if="draftStyle.styleName === 'presets'">
      <label>
        Preset
        <div class="style-control-preset-picker" role="radiogroup" aria-label="Preset colour">
          <button
            v-for="preset in presetColours"
            :key="preset.cssVar"
            type="button"
            class="style-control-preset-swatch"
            :class="{ 'style-control-preset-swatch--selected': draftStyle.presetIndex === preset.index }"
            :style="{ backgroundColor: preset.cssValue }"
            :disabled="disabled"
            :aria-pressed="draftStyle.presetIndex === preset.index"
            :aria-label="`Preset ${preset.index + 1}`"
            @click="setDraftStyleField('presetIndex', preset.index)"
          />
        </div>
      </label>
    </template>

    <template v-else-if="draftStyle.styleName === 'solid'">
      <label>
        Background Color
        <input
          type="color"
          class="style-control-colour-input"
          :value="draftStyle.backgroundColor" @input="event => setColour('backgroundColor', event)"
          :disabled="disabled"
        />
      </label>
    </template>

    <div v-else-if="draftStyle.styleName === 'gradient'" class="style-control-gradient-colours">
      <label>
        Left Color
        <input
          type="color"
          class="style-control-colour-input"
          :value="draftStyle.leftColor" @input="event => setColour('leftColor', event)"
          :disabled="disabled"
        />
      </label>
      <label>
        Right Color
        <input
          type="color"
          class="style-control-colour-input"
          :value="draftStyle.rightColor" @input="event => setColour('rightColor', event)"
          :disabled="disabled"
        />
      </label>
    </div>

    <label v-if="draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient'">
      Text Color Mode
      <select :value="draftStyle.textColorMode" :disabled="disabled" @change="onTextModeChange">
        <option value="auto">Auto Contrast</option>
        <option value="custom">Custom</option>
      </select>
    </label>

    <label v-if="(draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient') && draftStyle.textColorMode === 'custom'">
      Text Color
      <input
        type="color"
        class="style-control-colour-input"
        :value="draftStyle.textColor" @input="event => setColour('textColor', event)"
        :disabled="disabled"
      />
    </label>

    <label v-if="draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient'">
      Border
      <select :value="draftStyle.borderMode" :disabled="disabled" @change="onBorderModeChange">
        <option value="auto">Auto</option>
        <option value="custom">Custom</option>
        <option value="none">None</option>
      </select>
    </label>

    <label v-if="(draftStyle.styleName === 'solid' || draftStyle.styleName === 'gradient') && draftStyle.borderMode === 'custom'">
      Border Color
      <input
        type="color"
        class="style-control-colour-input"
        :value="draftStyle.borderColor" @input="event => setColour('borderColor', event)"
        :disabled="disabled"
      />
    </label>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { parseStyleNameInput, useStyleDraft } from '../composables/useStyleDraft';
import { PRESET_TOKENS, type PresetToken } from '../utils/presetTheme';
import type { StyleDraft } from '../utils/styleTypes';

const props = withDefaults(defineProps<{
  modelValue: StyleDraft;
  disabled: boolean;
  allowedStyleNames?: readonly StyleDraft['styleName'][];
  presetColours?: readonly PresetToken[];
}>(), {
  allowedStyleNames: () => ['auto', 'presets', 'solid', 'gradient'],
  presetColours: () => PRESET_TOKENS
});
const emit = defineEmits<{ 'update:modelValue': [value: StyleDraft] }>();
const model = computed<StyleDraft | null>({
  get: () => props.modelValue,
  set: value => { if (value) emit('update:modelValue', value); }
});
const { draft: draftStyle, setStyleName, setTextMode, setBorderMode, setField: setDraftStyleField } = useStyleDraft(model);
const styleLabels = { auto: 'Auto', presets: 'Presets', solid: 'Solid', gradient: 'Gradient' };

function onStyleNameChange(event: Event) {
  setStyleName(parseStyleNameInput((event.target as HTMLSelectElement).value));
}
function onTextModeChange(event: Event) {
  setTextMode((event.target as HTMLSelectElement).value);
}
function onBorderModeChange(event: Event) {
  setBorderMode((event.target as HTMLSelectElement).value);
}
function setColour(field: 'backgroundColor' | 'leftColor' | 'rightColor' | 'textColor' | 'borderColor', event: Event) {
  setDraftStyleField(field, (event.target as HTMLInputElement).value);
}
</script>

<style scoped>
.style-control { display: grid; gap: 0.5rem; min-width: 0; }
.style-control-gradient-colours { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.5rem; }
.style-control-colour-input { min-height: 2.25rem; padding: 0.2rem; }
.style-control-preset-picker { display: flex; flex-wrap: wrap; gap: 0.4rem; }
.style-control-preset-swatch {
  width: 1.7rem; height: 1.7rem; border-radius: 999px;
  border: 1px solid var(--bo-border-default); cursor: pointer; padding: 0;
}
.style-control-preset-swatch--selected { outline: 2px solid var(--bo-focus-ring); outline-offset: 1px; }
</style>
