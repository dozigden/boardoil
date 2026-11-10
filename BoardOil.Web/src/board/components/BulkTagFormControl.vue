<template>
  <p v-if="tagNames.length === 0" class="bulk-tag-empty">No tags available.</p>
  <TagTriStateMatrix
    v-else
    class="bulk-tag-control"
    :available-tag-names="tagNames"
    :states="modelValue"
    :labels="{ left: 'Remove', middle: 'No change', right: 'Add' }"
    ariaLabel="Tag bulk edit matrix"
    left-action-prefix="Mark for remove"
    middle-action-prefix="Mark unchanged"
    right-action-prefix="Mark for add"
    :disabled="disabled"
    fluid
    :show-directional-cursor="false"
    @update:states="update"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import { useTagStore } from '../stores/tagStore';
import TagTriStateMatrix from './TagTriStateMatrix.vue';

defineProps<{ modelValue: TagFilterStateMap; disabled: boolean }>();
const emit = defineEmits<{ 'update:modelValue': [value: TagFilterStateMap] }>();
const tags = useTagStore();
const tagNames = computed(() => tags.tags.map(tag => tag.name).sort((left, right) => left.localeCompare(right)));
function update(value: TagFilterStateMap) { emit('update:modelValue', value); }
</script>

<style scoped>
.bulk-tag-control { max-height: min(46vh, 360px); }
.bulk-tag-empty { margin: 0; color: var(--bo-ink-muted); }
</style>
