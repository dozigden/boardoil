<template>
  <span v-if="resolvedTag" class="tag" :class="{ 'tag--with-emoji': tagEmoji }" :style="tagStyle" :aria-label="resolvedTag.name">
    <span v-if="tagEmoji" class="tag-emoji" aria-hidden="true">{{ tagEmoji }}</span>
    <span>{{ resolvedTag.name }}</span>
    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useTagStore } from '../stores/tagStore';
import { getTagPillStyle, normaliseTagEmojiForRender } from '../utils/tagStyles';

const props = defineProps<{
  tagName?: string;
  tagId?: number | null;
}>();

const tagStore = useTagStore();
const resolvedTag = computed(() => {
  if (props.tagId !== null && props.tagId !== undefined) {
    return tagStore.getTagById(props.tagId);
  }

  return tagStore.getTagByName(props.tagName ?? null);
});

const tagStyle = computed(() => getTagPillStyle(resolvedTag.value));
const tagEmoji = computed(() => normaliseTagEmojiForRender(resolvedTag.value?.emoji));
</script>
