<template>
  <FormDialog v-if="form" :key="sessionKey" :definition="form" @close="closeDialog" @saved="closeDialog">
    <template #before-fields="{ values }">
      <TagPreview :draft="values" :fallback-name="editingTag?.name ?? 'New tag'" />
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import { computed, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import FormDialog from '../../shared/forms/FormDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';
import { createTagForm } from '../forms/tagForm';
import TagPreview from './TagPreview.vue';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { confirm } = useConfirm();
const boardId = computed(() => boardStore.currentBoardId!);
const isCreateMode = computed(() => route.name === 'tags-new');
const routeTagId = computed(() => {
  const id = Number.parseInt(String(route.params.tagId ?? ''), 10);
  return Number.isFinite(id) ? id : null;
});
const editingTag = computed(() => tagStore.getTagById(routeTagId.value));
const sessionKey = computed(() => `${boardId.value}:${isCreateMode.value ? 'create' : routeTagId.value}`);
const form = shallowRef<ReturnType<typeof createTagForm> | null>(null);

watch(sessionKey, async () => {
  form.value = null;
  const targetBoardId = boardId.value;
  const tag = isCreateMode.value ? null : editingTag.value;
  if (!isCreateMode.value && !tag) {
    await router.replace({ name: 'tags', params: { boardId: targetBoardId } });
    return;
  }
  form.value = createTagForm(tag, {
    loadDefaultStyle: () => tagStore.getCreateDefaultStyle(targetBoardId),
    save: model => tagStore.saveTag(targetBoardId, tag?.id ?? null, model),
    delete: tag ? {
      run: () => boardStore.deleteTag(targetBoardId, tag.id),
      confirm: () => confirm({
        title: 'Delete tag',
        message: `Delete tag "${tag.name}"?\n\nThis removes the tag from all cards and cannot be undone.`,
        confirmLabel: 'Delete', danger: true
      })
    } : undefined,
    isCurrent: () => boardStore.currentBoardId === targetBoardId && tagStore.activeBoardId === targetBoardId
  });
}, { immediate: true });

async function closeDialog() {
  await router.push({ name: 'tags', params: { boardId: boardId.value } });
}
</script>
