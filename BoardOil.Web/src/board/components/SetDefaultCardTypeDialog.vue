<template>
  <FixedChromeDialog
    :open="true"
    title="Set default card type"
    close-label="Cancel default change"
    @close="closeDialog"
    @submit="submit"
  >
    <FormValidation>
      <p>Set <strong>{{ cardType.name }}</strong> as the default card type?</p>
    </FormValidation>
    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn btn--secondary" :disabled="busy" @click="closeDialog">Cancel</button>
        <button type="submit" class="btn" :disabled="busy">OK</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import FormValidation from '../../shared/components/FormValidation.vue';
import type { CardType } from '../../shared/types/boardTypes';
import { useCardTypeStore } from '../stores/cardTypeStore';

const props = defineProps<{ cardType: CardType; boardId: number }>();
const emit = defineEmits<{ close: [] }>();
const cardTypeStore = useCardTypeStore();
const { busy } = storeToRefs(cardTypeStore);

function closeDialog() {
  if (!busy.value) {
    emit('close');
  }
}

async function submit() {
  if (busy.value) {
    return;
  }

  const saved = await cardTypeStore.setDefaultCardType(props.cardType.id, props.boardId);
  if (saved) {
    emit('close');
  }
}
</script>
