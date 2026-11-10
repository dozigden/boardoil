<template>
  <FormDialog v-if="form" :key="sessionKey" :definition="form" @close="closeDialog" @saved="closeDialog">
    <template #before-fields="{ values }">
      <SlickPreview :draft="values" :fallback-name="editingSlick?.name ?? 'New slick'" />
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import { computed, shallowRef, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import FormDialog from '../../shared/forms/FormDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useBoardStore } from '../stores/boardStore';
import { useSlickStore } from '../stores/slickStore';
import { createSlickForm } from '../forms/slickForm';
import SlickPreview from './SlickPreview.vue';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const slickStore = useSlickStore();
const { confirm } = useConfirm();
const boardId = computed(() => boardStore.currentBoardId!);
const isCreateMode = computed(() => route.name === 'slicks-new');
const routeSlickId = computed(() => {
  const id = Number.parseInt(String(route.params.slickId ?? ''), 10);
  return Number.isFinite(id) ? id : null;
});
const editingSlick = computed(() => slickStore.getSlickById(routeSlickId.value));
const sessionKey = computed(() => `${boardId.value}:${isCreateMode.value ? 'create' : routeSlickId.value}`);
const form = shallowRef<ReturnType<typeof createSlickForm> | null>(null);

watch(sessionKey, async () => {
  form.value = null;
  const targetBoardId = boardId.value;
  const slick = isCreateMode.value ? null : editingSlick.value;
  if (!isCreateMode.value && !slick) {
    await router.replace({ name: 'slicks', params: { boardId: targetBoardId } });
    return;
  }
  form.value = createSlickForm(slick, {
    loadDefaultStyle: () => slickStore.getCreateDefaultStyle(targetBoardId),
    save: model => {
      if (slick) return slickStore.updateSlick(slick.id, model, targetBoardId);
      return slickStore.createSlick(model, targetBoardId);
    },
    delete: slick ? {
      run: () => boardStore.deleteSlick(slick.id, targetBoardId),
      confirm: () => confirm({
        title: 'Delete slick',
        message: `Delete slick "${slick.name}"?\n\nCards in this slick will become unslicked.`,
        confirmLabel: 'Delete', danger: true
      })
    } : undefined,
    isCurrent: () => boardStore.currentBoardId === targetBoardId && slickStore.activeBoardId === targetBoardId
  });
}, { immediate: true });

async function closeDialog() {
  await router.push({ name: 'slicks', params: { boardId: boardId.value } });
}
</script>
