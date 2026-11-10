<template>
  <div class="slick-preview">
    <span class="badge">Preview</span>
    <span class="tag" :class="classes" :style="style" :aria-label="name">{{ name }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { SlickDraft } from '../forms/slickForm';
import { buildStylePropertiesJsonFromDraft } from '../../shared/utils/styleDraftAdapter';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';

const props = defineProps<{ draft: SlickDraft; fallbackName: string }>();
const name = computed(() => props.draft.name.trim() || props.fallbackName);
const presentation = computed(() => ({
  styleName: props.draft.style.styleName,
  stylePropertiesJson: buildStylePropertiesJsonFromDraft(props.draft.style)
}));
const classes = computed(() => getSemanticStyleClasses(presentation.value, 'slick'));
const style = computed(() => getSurfaceStyle(presentation.value, {
  fallbackBackground: 'var(--panel)', fallbackColor: 'var(--text)', fallbackBorderColor: 'var(--line)'
}));
</script>

<style scoped>
.slick-preview { display: flex; align-items: center; gap: 0.6rem; }
</style>
