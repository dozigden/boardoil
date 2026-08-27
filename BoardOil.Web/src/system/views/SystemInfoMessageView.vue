<template>
  <section class="system-info-message-view">
    <header class="system-info-message-header">
      <h2>System Info Message</h2>
      <p>Shown in the app header for authenticated users.</p>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

    <section v-else class="panel panel-stack panel-stack--cozy">
      <div class="system-info-message-row system-info-message-row--start">
        <span class="system-info-message-label">Status</span>
        <span class="system-info-message-value">
          <span class="badge">{{ systemInfoEnabledDraft ? 'Enabled' : 'Disabled' }}</span>
        </span>
      </div>

      <label class="system-info-message-checkbox-row">
        <input
          v-model="systemInfoEnabledDraft"
          :disabled="saving"
          type="checkbox"
        />
        <span>Enabled</span>
      </label>

      <label class="system-info-message-input-group">
        <span class="system-info-message-input-label">Emoji</span>
        <div class="system-info-message-emoji-picker-wrap">
          <EmojiPickerDropdown v-model="systemInfoEmojiDraft" :disabled="saving" :teleport="false" placeholder="Select emoji" />
        </div>
      </label>

      <label class="system-info-message-input-group">
        <span class="system-info-message-input-label">Title</span>
        <input
          v-model="systemInfoTitleDraft"
          :disabled="saving"
          class="system-info-message-input"
          placeholder="Maintenance update"
          autocomplete="off"
          spellcheck="false"
        />
      </label>

      <label class="system-info-message-input-group">
        <span class="system-info-message-input-label">Description</span>
        <textarea
          v-model="systemInfoDescriptionDraft"
          :disabled="saving"
          class="system-info-message-textarea"
          aria-label="System info description"
          rows="8"
        />
      </label>

      <template v-if="systemInfoStyleDraft">
        <label class="system-info-message-input-group">
          <span class="system-info-message-input-label">Style</span>
          <select
            :disabled="saving"
            :value="systemInfoStyleDraft.styleName"
            @change="setSystemInfoStyleName(parseSystemInfoStyleNameInput(($event.target as HTMLSelectElement).value))"
          >
            <option value="auto">Auto</option>
            <option value="presets">Presets</option>
            <option value="solid">Solid</option>
          </select>
        </label>

        <template v-if="systemInfoStyleDraft.styleName === 'presets'">
          <div class="system-info-message-input-group">
            <span class="system-info-message-input-label">Preset colour</span>
            <div class="system-info-preset-picker" role="radiogroup" aria-label="System info preset colour">
              <button
                v-for="preset in presetColours"
                :key="preset.cssVar"
                type="button"
                class="system-info-preset-swatch"
                :class="{ 'system-info-preset-swatch--selected': systemInfoStyleDraft.presetIndex === preset.index }"
                :style="{ backgroundColor: preset.cssValue }"
                :aria-pressed="systemInfoStyleDraft.presetIndex === preset.index"
                :aria-label="`Preset ${preset.index + 1}`"
                @click="setSystemInfoStyleField('presetIndex', preset.index)"
              />
            </div>
          </div>
        </template>

        <template v-else-if="systemInfoStyleDraft.styleName === 'solid'">
          <label class="system-info-message-input-group">
            <span class="system-info-message-input-label">Background colour</span>
            <input
              :disabled="saving"
              :value="systemInfoStyleDraft.backgroundColor"
              type="color"
              @input="setSystemInfoStyleField('backgroundColor', ($event.target as HTMLInputElement).value)"
            />
          </label>

          <label class="system-info-message-input-group">
            <span class="system-info-message-input-label">Text colour mode</span>
            <select :disabled="saving" :value="systemInfoStyleDraft.textColorMode" @change="setSystemInfoTextMode(($event.target as HTMLSelectElement).value)">
              <option value="auto">Auto</option>
              <option value="custom">Custom</option>
            </select>
          </label>

          <label v-if="systemInfoStyleDraft.textColorMode === 'custom'" class="system-info-message-input-group">
            <span class="system-info-message-input-label">Text colour</span>
            <input
              :disabled="saving"
              :value="systemInfoStyleDraft.textColor"
              type="color"
              @input="setSystemInfoStyleField('textColor', ($event.target as HTMLInputElement).value)"
            />
          </label>

          <label class="system-info-message-input-group">
            <span class="system-info-message-input-label">Border mode</span>
            <select :disabled="saving" :value="systemInfoStyleDraft.borderMode" @change="setSystemInfoBorderMode(($event.target as HTMLSelectElement).value)">
              <option value="auto">Auto</option>
              <option value="custom">Custom</option>
              <option value="none">None</option>
            </select>
          </label>

          <label v-if="systemInfoStyleDraft.borderMode === 'custom'" class="system-info-message-input-group">
            <span class="system-info-message-input-label">Border colour</span>
            <input
              :disabled="saving"
              :value="systemInfoStyleDraft.borderColor"
              type="color"
              @input="setSystemInfoStyleField('borderColor', ($event.target as HTMLInputElement).value)"
            />
          </label>
        </template>
      </template>

      <div v-if="systemInfoPreviewStylePresentation" class="system-info-message-input-group">
        <span class="system-info-message-input-label">Header preview</span>
        <p class="system-info-preview-chip" :class="systemInfoPreviewClasses" :style="systemInfoPreviewStyle">
          <span v-if="trimmedSystemInfoEmoji" class="system-info-preview-emoji bo-emoji">{{ trimmedSystemInfoEmoji }}</span>
          <strong>{{ trimmedSystemInfoTitle.length > 0 ? trimmedSystemInfoTitle : 'System information' }}</strong>
        </p>
      </div>

      <div class="system-info-message-actions">
        <button type="button" class="btn" :disabled="saving || !isDirty" @click="saveSystemInfoMessage">
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useStyleDraft } from '../../board/composables/useStyleDraft';
import { PRESET_TOKENS } from '../../shared/utils/presetTheme';
import { createRestrictedStyleDraft } from '../../shared/utils/styleDraftAdapter';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';
import EmojiPickerDropdown from '../../shared/components/EmojiPickerDropdown.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { SystemInfoMessageDto } from '../../shared/types/configurationTypes';
import type { StylePresentation } from '../../shared/utils/styleTypes';

const DEFAULT_SYSTEM_INFO_STYLE_NAME = 'presets';
const DEFAULT_SYSTEM_INFO_STYLE_PROPERTIES_JSON = '{"presetIndex":2}';
const allowedSystemInfoStyleNames = new Set<StylePresentation['styleName']>(['auto', 'presets', 'solid']);

const systemApi = createSystemApi();
const feedback = useUiFeedbackStore();
const errorMessage = ref<string | null>(null);
const saving = ref(false);
const savedSnapshot = ref('');
const systemInfoEnabledDraft = ref(false);
const systemInfoEmojiDraft = ref<string | null>(null);
const systemInfoTitleDraft = ref('');
const systemInfoDescriptionDraft = ref('');
const presetColours = PRESET_TOKENS;
const {
  draft: systemInfoStyleDraft,
  stylePropertiesJson: systemInfoStylePropertiesJson,
  setDraft: setSystemInfoStyleDraft,
  setStyleName: setSystemInfoStyleName,
  setTextMode: setSystemInfoTextMode,
  setBorderMode: setSystemInfoBorderMode,
  setField: setSystemInfoStyleField
} = useStyleDraft();
const trimmedSystemInfoTitle = computed(() => systemInfoTitleDraft.value.trim());
const trimmedSystemInfoEmoji = computed(() => {
  const trimmed = (systemInfoEmojiDraft.value ?? '').trim();
  return trimmed.length > 0 ? trimmed : null;
});
const systemInfoPreviewStylePresentation = computed<StylePresentation | null>(() => {
  if (!systemInfoStyleDraft.value || !systemInfoStylePropertiesJson.value) {
    return null;
  }

  return {
    styleName: resolveSystemInfoStyleName(systemInfoStyleDraft.value.styleName),
    stylePropertiesJson: systemInfoStylePropertiesJson.value
  };
});
const systemInfoPreviewStyle = computed(() => getSurfaceStyle(systemInfoPreviewStylePresentation.value, {
  fallbackBackground: 'var(--bo-surface-chip)',
  fallbackColor: 'var(--bo-ink-default)',
  fallbackBorderColor: 'var(--bo-border-soft)'
}));
const systemInfoPreviewClasses = computed(() => getSemanticStyleClasses(systemInfoPreviewStylePresentation.value, 'card'));
const isDirty = computed(() => {
  return serialiseSystemInfoMessage(buildCurrentSystemInfoMessage()) !== savedSnapshot.value;
});

onMounted(async () => {
  const systemInfoResult = await systemApi.getSystemInfoMessage();
  if (!systemInfoResult.ok) {
    errorMessage.value = systemInfoResult.error.message;
    return;
  }

  applySystemInfoDraft(systemInfoResult.data);
  savedSnapshot.value = systemInfoResult.data === null
    ? serialiseSystemInfoMessage(buildCurrentSystemInfoMessage())
    : serialiseSystemInfoMessage(systemInfoResult.data);
});

async function saveSystemInfoMessage() {
  if (!isDirty.value) {
    return;
  }

  saving.value = true;
  try {
    const systemInfoMessage = buildCurrentSystemInfoMessage();

    const systemInfoResult = await systemApi.updateSystemInfoMessage(systemInfoMessage);
    if (!systemInfoResult.ok) {
      feedback.showToast(systemInfoResult.error.message, 'error');
      return;
    }

    const persistedMessage = systemInfoResult.data ?? systemInfoMessage;
    savedSnapshot.value = serialiseSystemInfoMessage(persistedMessage);
    feedback.showToast('Saved successfully.');
  } finally {
    saving.value = false;
  }
}

function applySystemInfoDraft(systemInfoMessage: SystemInfoMessageDto | null) {
  systemInfoEnabledDraft.value = systemInfoMessage?.enabled ?? false;
  systemInfoEmojiDraft.value = systemInfoMessage?.emoji ?? null;
  systemInfoTitleDraft.value = systemInfoMessage?.title ?? '';
  systemInfoDescriptionDraft.value = systemInfoMessage?.description ?? '';

  const styleName = systemInfoMessage?.styleName ?? DEFAULT_SYSTEM_INFO_STYLE_NAME;
  const stylePropertiesJson = systemInfoMessage?.stylePropertiesJson ?? DEFAULT_SYSTEM_INFO_STYLE_PROPERTIES_JSON;
  setSystemInfoStyleDraft(createRestrictedStyleDraft(
    {
      styleName,
      stylePropertiesJson
    },
    allowedSystemInfoStyleNames,
    {
      styleName: 'auto',
      stylePropertiesJson: '{}'
    }
  ));
}

function parseSystemInfoStyleNameInput(value: string): 'auto' | 'presets' | 'solid' {
  if (value === 'auto') {
    return 'auto';
  }

  if (value === 'solid') {
    return 'solid';
  }

  return 'presets';
}

function resolveSystemInfoStyleName(styleName: string | undefined): 'auto' | 'presets' | 'solid' {
  if (styleName === 'auto') {
    return 'auto';
  }

  if (styleName === 'solid') {
    return 'solid';
  }

  return 'presets';
}

function buildCurrentSystemInfoMessage(): SystemInfoMessageDto {
  return {
    enabled: systemInfoEnabledDraft.value,
    emoji: trimmedSystemInfoEmoji.value,
    title: trimmedSystemInfoTitle.value,
    description: systemInfoDescriptionDraft.value,
    styleName: resolveSystemInfoStyleName(systemInfoStyleDraft.value?.styleName),
    stylePropertiesJson: systemInfoStylePropertiesJson.value ?? DEFAULT_SYSTEM_INFO_STYLE_PROPERTIES_JSON
  };
}

function serialiseSystemInfoMessage(value: SystemInfoMessageDto): string {
  return JSON.stringify(value);
}
</script>

<style scoped>
.system-info-message-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
  max-width: 760px;
}

.system-info-message-header h2 {
  margin: 0;
}

.system-info-message-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.system-info-message-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.system-info-message-row--start {
  align-items: flex-start;
}

.system-info-message-label {
  font-weight: 600;
  color: var(--bo-ink-strong);
}

.system-info-message-input-group {
  display: grid;
  gap: 0.35rem;
}

.system-info-message-input-label {
  font-weight: 600;
  color: var(--bo-ink-default);
}

.system-info-message-checkbox-row {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--bo-ink-default);
}

.system-info-message-emoji-picker-wrap {
  display: grid;
  justify-content: start;
}

.system-info-message-textarea {
  width: 100%;
  min-height: 9rem;
  resize: vertical;
}

.system-info-preset-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.system-info-preset-swatch {
  width: 2.05rem;
  height: 2.05rem;
  border: 2px solid transparent;
  border-radius: 0.5rem;
  cursor: pointer;
}

.system-info-preset-swatch--selected {
  border-color: var(--bo-link);
  box-shadow: 0 0 0 1px var(--bo-link);
}

.system-info-preview-chip {
  margin: 0;
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 0.35rem;
  padding: 0.55rem 0.85rem;
  font-size: 0.9rem;
  width: fit-content;
}

.system-info-preview-emoji {
  line-height: 1;
}

.system-info-message-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}
</style>
