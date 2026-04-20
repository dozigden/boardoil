<template>
  <span v-if="displayTagName" class="tag" :class="{ 'tag--with-emoji': tagEmoji }" :style="tagStyle" :aria-label="displayTagName">
    <span v-if="tagEmoji" class="tag-emoji" aria-hidden="true">{{ tagEmoji }}</span>
    <span>{{ displayTagName }}</span>
    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useTagStore } from '../stores/tagStore';
import { getTagPillStyle, normaliseTagEmojiForRender } from '../utils/tagStyles';

const props = withDefaults(defineProps<{
  tagName?: string;
  tagId?: number | null;
  enableFallback?: boolean;
}>(), {
  enableFallback: false
});

const tagStore = useTagStore();
const resolvedTagFromStore = computed(() => {
  if (props.tagId !== null && props.tagId !== undefined) {
    const byId = tagStore.getTagById(props.tagId);
    if (byId) {
      return byId;
    }
  }

  return tagStore.getTagByName(props.tagName ?? null);
});
const fallbackTagName = computed(() => {
  if (!props.enableFallback) {
    return null;
  }

  const tagNameFromProp = props.tagName?.trim();
  if (tagNameFromProp) {
    return tagNameFromProp;
  }

  if (props.tagId !== null && props.tagId !== undefined) {
    return 'Tag';
  }

  return null;
});
const displayTagName = computed(() => resolvedTagFromStore.value?.name ?? fallbackTagName.value);
const tagStyleSource = computed(() => resolvedTagFromStore.value);

const tagStyle = computed(() => getTagPillStyle(tagStyleSource.value));
const tagEmoji = computed(() => normaliseTagEmojiForRender(tagStyleSource.value?.emoji));
</script>
