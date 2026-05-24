<template>
  <section class="configuration-view">
    <header class="configuration-header">
      <h2>Configuration</h2>
      <p>Runtime settings visible to administrators.</p>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section v-else class="panel panel-stack panel-stack--cozy">
      <div class="configuration-row">
        <span class="configuration-label">Allow insecure cookies</span>
        <span class="configuration-value">
          <span class="badge">{{ configuration?.allowInsecureCookies ? 'Enabled' : 'Disabled' }}</span>
        </span>
      </div>
      <p class="configuration-hint">
        {{ configuration?.allowInsecureCookies
          ? 'HTTP sessions are allowed. Not recommended.'
          : 'Secure cookies are enforced. HTTPS required (except localhost behavior).' }}
      </p>

      <div class="configuration-row configuration-row--start">
        <span class="configuration-label">MCP public base URL override</span>
        <span class="configuration-value">
          <span class="badge">{{ configuration?.mcpPublicBaseUrl ? 'Override set' : 'Auto (relative)' }}</span>
        </span>
      </div>
      <p class="configuration-hint">
        Leave blank to keep MCP discovery URLs relative (recommended default for Docker and proxy setups).
      </p>
      <label class="configuration-input-group">
        <span class="configuration-input-label">Public base URL</span>
        <input
          v-model="mcpPublicBaseUrlDraft"
          :disabled="saving"
          class="configuration-input"
          placeholder="https://boardoil.example.com"
          autocomplete="off"
          spellcheck="false"
        />
      </label>

      <hr class="configuration-divider">

      <div class="configuration-row configuration-row--start">
        <span class="configuration-label">System info message</span>
        <span class="configuration-value">
          <span class="badge">{{ systemInfoEnabledDraft ? 'Enabled' : 'Disabled' }}</span>
        </span>
      </div>
      <p class="configuration-hint">
        Shows a clickable message in the app header for all authenticated users.
      </p>
      <label class="configuration-checkbox-row">
        <input
          v-model="systemInfoEnabledDraft"
          :disabled="saving"
          type="checkbox"
        />
        <span>Enabled</span>
      </label>
      <label class="configuration-input-group">
        <span class="configuration-input-label">Emoji</span>
        <input
          v-model="systemInfoEmojiDraft"
          :disabled="saving"
          class="configuration-input"
          placeholder="⚠️"
          autocomplete="off"
          spellcheck="false"
        />
      </label>
      <label class="configuration-input-group">
        <span class="configuration-input-label">Title</span>
        <input
          v-model="systemInfoTitleDraft"
          :disabled="saving"
          class="configuration-input"
          placeholder="Maintenance update"
          autocomplete="off"
          spellcheck="false"
        />
      </label>
      <label class="configuration-input-group">
        <span class="configuration-input-label">Description</span>
        <MdEditor
          v-model="systemInfoDescriptionDraft"
          aria-label="System info description"
          :disabled="saving"
          min-height="8rem"
        />
      </label>

      <template v-if="systemInfoStyleDraft">
        <label class="configuration-input-group">
          <span class="configuration-input-label">Style</span>
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
          <div class="configuration-input-group">
            <span class="configuration-input-label">Preset colour</span>
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
          <label class="configuration-input-group">
            <span class="configuration-input-label">Background colour</span>
            <input
              :disabled="saving"
              :value="systemInfoStyleDraft.backgroundColor"
              type="color"
              @input="setSystemInfoStyleField('backgroundColor', ($event.target as HTMLInputElement).value)"
            />
          </label>

          <label class="configuration-input-group">
            <span class="configuration-input-label">Text colour mode</span>
            <select :disabled="saving" :value="systemInfoStyleDraft.textColorMode" @change="setSystemInfoTextMode(($event.target as HTMLSelectElement).value)">
              <option value="auto">Auto</option>
              <option value="custom">Custom</option>
            </select>
          </label>

          <label v-if="systemInfoStyleDraft.textColorMode === 'custom'" class="configuration-input-group">
            <span class="configuration-input-label">Text colour</span>
            <input
              :disabled="saving"
              :value="systemInfoStyleDraft.textColor"
              type="color"
              @input="setSystemInfoStyleField('textColor', ($event.target as HTMLInputElement).value)"
            />
          </label>

          <label class="configuration-input-group">
            <span class="configuration-input-label">Border mode</span>
            <select :disabled="saving" :value="systemInfoStyleDraft.borderMode" @change="setSystemInfoBorderMode(($event.target as HTMLSelectElement).value)">
              <option value="auto">Auto</option>
              <option value="custom">Custom</option>
              <option value="none">None</option>
            </select>
          </label>

          <label v-if="systemInfoStyleDraft.borderMode === 'custom'" class="configuration-input-group">
            <span class="configuration-input-label">Border colour</span>
            <input
              :disabled="saving"
              :value="systemInfoStyleDraft.borderColor"
              type="color"
              @input="setSystemInfoStyleField('borderColor', ($event.target as HTMLInputElement).value)"
            />
          </label>
        </template>
      </template>

      <div v-if="systemInfoPreviewStylePresentation" class="configuration-input-group">
        <span class="configuration-input-label">Header preview</span>
        <p class="system-info-preview-chip" :class="systemInfoPreviewClasses" :style="systemInfoPreviewStyle">
          <span v-if="trimmedSystemInfoEmoji" class="system-info-preview-emoji">{{ trimmedSystemInfoEmoji }}</span>
          <strong>{{ trimmedSystemInfoTitle.length > 0 ? trimmedSystemInfoTitle : 'System information' }}</strong>
        </p>
      </div>

      <div class="configuration-actions">
        <button type="button" class="btn" :disabled="saving" @click="saveConfiguration">
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
        <button type="button" class="btn btn--secondary" :disabled="saving" @click="resetToAuto">
          Use auto (relative)
        </button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import MdEditor from '../../shared/components/MdEditor.vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useStyleDraft } from '../../board/composables/useStyleDraft';
import { PRESET_TOKENS } from '../../shared/utils/presetTheme';
import { createStyleDraft } from '../../shared/utils/styleDraftAdapter';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';
import type { ConfigurationDto, SystemInfoMessageDto } from '../../shared/types/configurationTypes';
import type { StylePresentation } from '../../shared/utils/styleTypes';

const DEFAULT_SYSTEM_INFO_STYLE_NAME = 'presets';
const DEFAULT_SYSTEM_INFO_STYLE_PROPERTIES_JSON = '{"presetIndex":2}';

const systemApi = createSystemApi();
const configuration = ref<ConfigurationDto | null>(null);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const saving = ref(false);
const mcpPublicBaseUrlDraft = ref('');
const systemInfoEnabledDraft = ref(false);
const systemInfoEmojiDraft = ref('');
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
  const trimmed = systemInfoEmojiDraft.value.trim();
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
const systemInfoPreviewClasses = computed(() => getSemanticStyleClasses(systemInfoPreviewStylePresentation.value, 'tag'));

onMounted(async () => {
  const [configurationResult, systemInfoResult] = await Promise.all([
    systemApi.getConfiguration(),
    systemApi.getSystemInfoMessage()
  ]);

  if (!configurationResult.ok) {
    errorMessage.value = configurationResult.error.message;
    return;
  }

  if (!systemInfoResult.ok) {
    errorMessage.value = systemInfoResult.error.message;
    return;
  }

  applyConfigurationDraft(configurationResult.data);
  applySystemInfoDraft(systemInfoResult.data);
});

async function saveConfiguration() {
  saving.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const requestValue = mcpPublicBaseUrlDraft.value.trim();
    const systemInfoStyleJson = systemInfoStylePropertiesJson.value ?? '{}';
    const systemInfoMessage: SystemInfoMessageDto = {
      enabled: systemInfoEnabledDraft.value,
      emoji: trimmedSystemInfoEmoji.value,
      title: trimmedSystemInfoTitle.value,
      description: systemInfoDescriptionDraft.value,
      styleName: resolveSystemInfoStyleName(systemInfoStyleDraft.value?.styleName),
      stylePropertiesJson: systemInfoStyleJson
    };
    const configurationResult = await systemApi.updateConfiguration({
      mcpPublicBaseUrl: requestValue.length > 0 ? requestValue : null
    });
    if (!configurationResult.ok) {
      errorMessage.value = configurationResult.error.message;
      return;
    }

    const systemInfoResult = await systemApi.updateSystemInfoMessage(systemInfoMessage);
    if (!systemInfoResult.ok) {
      errorMessage.value = systemInfoResult.error.message;
      return;
    }

    applyConfigurationDraft(configurationResult.data);
    applySystemInfoDraft(systemInfoResult.data);
    successMessage.value = 'Saved configuration.';
  } finally {
    saving.value = false;
  }
}

async function resetToAuto() {
  mcpPublicBaseUrlDraft.value = '';
  await saveConfiguration();
}

function applyConfigurationDraft(nextConfiguration: ConfigurationDto) {
  configuration.value = nextConfiguration;
  mcpPublicBaseUrlDraft.value = nextConfiguration.mcpPublicBaseUrl ?? '';
}

function applySystemInfoDraft(systemInfoMessage: SystemInfoMessageDto | null) {
  systemInfoEnabledDraft.value = systemInfoMessage?.enabled ?? false;
  systemInfoEmojiDraft.value = systemInfoMessage?.emoji ?? '';
  systemInfoTitleDraft.value = systemInfoMessage?.title ?? '';
  systemInfoDescriptionDraft.value = systemInfoMessage?.description ?? '';

  const styleName = systemInfoMessage?.styleName ?? DEFAULT_SYSTEM_INFO_STYLE_NAME;
  const stylePropertiesJson = systemInfoMessage?.stylePropertiesJson ?? DEFAULT_SYSTEM_INFO_STYLE_PROPERTIES_JSON;
  setSystemInfoStyleDraft(createStyleDraft({
    styleName,
    stylePropertiesJson
  }));
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
</script>

<style scoped>
.configuration-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
  max-width: 760px;
}

.configuration-header h2 {
  margin: 0;
}

.configuration-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.configuration-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.configuration-row--start {
  align-items: flex-start;
}

.configuration-label {
  font-weight: 600;
  color: var(--bo-ink-strong);
}

.configuration-input-group {
  display: grid;
  gap: 0.35rem;
}

.configuration-input-label {
  font-weight: 600;
  color: var(--bo-ink-default);
}

.configuration-input {
  width: 100%;
}

.configuration-divider {
  width: 100%;
  margin: 0.35rem 0;
  border: 0;
  border-top: 1px solid var(--bo-border-soft);
}

.configuration-checkbox-row {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--bo-ink-default);
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
  border-radius: 999px;
  padding: 0.35rem 0.65rem;
  font-size: 0.9rem;
  width: fit-content;
}

.system-info-preview-emoji {
  line-height: 1;
}

.configuration-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.configuration-hint {
  margin: 0;
  color: var(--bo-ink-muted);
}
</style>
