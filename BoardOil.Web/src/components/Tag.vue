<template>
  <span class="tag" :style="tagStyle">
    <span>{{ tagName }}</span>
    <slot />
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useTagStore } from '../stores/tagStore';
import { getTagPillStyle } from '../utils/tagStyles';

const props = defineProps<{
  tagName: string;
}>();

const tagStore = useTagStore();

const tagStyle = computed(() => getTagPillStyle(tagStore.getTagByName(props.tagName)));
</script>
