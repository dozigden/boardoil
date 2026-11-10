<template>
  <div class="system-info-preview" role="group" aria-label="Header preview">
    <span>Header preview</span>
    <p class="system-info-preview-chip" :class="classes" :style="style">
      <span v-if="emoji" class="system-info-preview-emoji bo-emoji">{{ emoji }}</span>
      <strong>{{ draft.title.trim() || 'System information' }}</strong>
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { SystemInfoMessageDraft } from '../forms/systemInfoMessageForm';
import { buildStylePropertiesJsonFromDraft } from '../../shared/utils/styleDraftAdapter';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';

const props = defineProps<{ draft: SystemInfoMessageDraft }>();
const emoji = computed(() => props.draft.emoji?.trim());
const presentation = computed(() => ({
  styleName: props.draft.style.styleName,
  stylePropertiesJson: buildStylePropertiesJsonFromDraft(props.draft.style)
}));
const classes = computed(() => getSemanticStyleClasses(presentation.value, 'card'));
const style = computed(() => getSurfaceStyle(presentation.value, {
  fallbackBackground: 'var(--bo-surface-chip)',
  fallbackColor: 'var(--bo-ink-default)',
  fallbackBorderColor: 'var(--bo-border-soft)'
}));
</script>

<style scoped>
.system-info-preview { display: grid; gap: 0.35rem; }
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
.system-info-preview-emoji { line-height: 1; }
</style>
