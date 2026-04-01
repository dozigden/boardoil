<template>
  <span class="tag" :class="{ 'tag--with-emoji': tagEmoji }" :style="tagStyle" :aria-label="tagName">
    <span v-if="tagEmoji" class="tag-emoji" aria-hidden="true">{{ tagEmoji }}</span>
    <span>{{ tagName }}</span>
    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useTagStore } from '../stores/tagStore';
import { getTagPillStyle, normaliseTagEmojiForRender } from '../utils/tagStyles';

const props = defineProps<{
  tagName: string;
}>();

const tagStore = useTagStore();
const tag = computed(() => tagStore.getTagByName(props.tagName));

const tagStyle = computed(() => getTagPillStyle(tag.value));
const tagEmoji = computed(() => normaliseTagEmojiForRender(tag.value?.emoji));
</script>
