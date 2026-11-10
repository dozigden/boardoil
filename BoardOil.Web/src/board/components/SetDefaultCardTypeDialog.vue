<template>
  <FormDialog :definition="form" @close="close" @saved="close">
    <template #before-fields>
      <p>Set <strong>{{ cardType.name }}</strong> as the default card type?</p>
    </template>
    <template #actions="{ busy, cancel }">
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn btn--secondary" :disabled="busy" @click="cancel">Cancel</button>
        <button type="submit" class="btn" :disabled="busy">OK</button>
      </div>
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import FormDialog from '../../shared/forms/FormDialog.vue';
import { defineForm } from '../../shared/forms/formDefinition';
import type { CardType } from '../../shared/types/boardTypes';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useBoardStore } from '../stores/boardStore';

const props = defineProps<{ cardType: CardType; boardId: number }>();
const emit = defineEmits<{ close: [] }>();
const cardTypeStore = useCardTypeStore();
const boardStore = useBoardStore();
const targetBoardId = props.boardId;
const targetCardTypeId = props.cardType.id;
const form = defineForm({
  title: 'Set default card type', submitLabel: 'OK', closeLabel: 'Cancel default change',
  initialValues: () => ({}), fields: [],
  isCurrent: () => boardStore.currentBoardId === targetBoardId && cardTypeStore.activeBoardId === targetBoardId,
  submit: () => cardTypeStore.setDefaultCardType(targetCardTypeId, targetBoardId)
});
function close() { emit('close'); }
</script>
