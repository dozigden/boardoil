<template>
  <div class="tag-preview">
    <span class="badge">Preview</span>
    <span class="tag" :class="[classes, { 'tag--with-emoji': emoji }]" :style="style" :aria-label="name">
      <span v-if="emoji" class="tag-emoji bo-emoji" aria-hidden="true">{{ emoji }}</span>
      {{ name }}
    </span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { TagDraft } from '../forms/tagForm';
import { buildStylePropertiesJsonFromDraft, getTagPillClassList, getTagPillStyle, normaliseTagEmojiForRender } from '../../shared/utils/tagStyles';

const props = defineProps<{ draft: TagDraft; fallbackName: string }>();
const name = computed(() => props.draft.name.trim() || props.fallbackName);
const emoji = computed(() => normaliseTagEmojiForRender(props.draft.emoji));
const presentation = computed(() => ({
  styleName: props.draft.style.styleName,
  stylePropertiesJson: buildStylePropertiesJsonFromDraft(props.draft.style),
  emoji: props.draft.emoji
}));
const classes = computed(() => getTagPillClassList(presentation.value));
const style = computed(() => getTagPillStyle(presentation.value));
</script>

<style scoped>
.tag-preview { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
</style>
