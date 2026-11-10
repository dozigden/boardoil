<template>
  <FormDialog v-if="form" :key="sessionKey" :definition="form" @close="closeDialog" @saved="closeDialog">
    <template #before-fields="{ values }">
      <CardTypePreview :draft="values" :fallback-name="editingCardType?.name ?? 'New card type'" />
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import { computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import FormDialog from '../../shared/forms/FormDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { createCardTypeForm } from '../forms/cardTypeForm';
import CardTypePreview from './CardTypePreview.vue';

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardTypeStore = useCardTypeStore();
const { confirm } = useConfirm();
const boardId = computed(() => boardStore.currentBoardId!);
const isCreateMode = computed(() => route.name === 'card-types-new');
const routeCardTypeId = computed(() => {
  const id = Number.parseInt(String(route.params.cardTypeId ?? ''), 10);
  return Number.isFinite(id) ? id : null;
});
const editingCardType = computed(() => cardTypeStore.getCardTypeById(routeCardTypeId.value));
const sessionKey = computed(() => `${boardId.value}:${isCreateMode.value ? 'create' : routeCardTypeId.value}`);
const form = computed(() => {
  const cardType = editingCardType.value;
  if (!isCreateMode.value && !cardType) return null;
  const targetBoardId = boardId.value;
  return createCardTypeForm(cardType, {
    save: model => {
      if (cardType) return cardTypeStore.updateCardType(cardType.id, model, targetBoardId);
      return cardTypeStore.createCardType(model, targetBoardId);
    },
    delete: cardType ? {
      run: () => cardTypeStore.deleteCardType(cardType.id, targetBoardId),
      confirm: () => confirm({
        title: 'Delete card type',
        message: `Delete card type "${cardType.name}"?\n\nCards using this type will be reassigned to the board default type.`,
        confirmLabel: 'Delete', danger: true
      })
    } : undefined,
    isCurrent: () => boardStore.currentBoardId === targetBoardId && cardTypeStore.activeBoardId === targetBoardId
  });
});

watch([boardId, isCreateMode, editingCardType], async ([id, creating, cardType]) => {
  if (!creating && !cardType) await router.replace({ name: 'card-types', params: { boardId: id } });
}, { immediate: true });

async function closeDialog() {
  await router.push({ name: 'card-types', params: { boardId: boardId.value } });
}
</script>
