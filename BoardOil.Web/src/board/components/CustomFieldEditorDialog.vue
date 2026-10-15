<template>
  <FixedChromeDialog :open="draft !== null" :title="customFieldId === null ? 'Add custom field' : 'Edit custom field'" :busy="busy" @close="close" @submit="save">
    <template v-if="draft">
      <div v-if="error || previewError" ref="summary" class="field-errors" role="alert" tabindex="-1">
        <p v-if="error && !error.validationErrors">{{ error.message }}</p>
        <p v-if="previewError && !previewError.validationErrors">Unable to preview changes. {{ previewError.message }}</p>
        <p v-for="(messages, path) in summaryErrors" :key="path">{{ messages.join(' ') }}</p>
      </div>
      <CustomFieldDefinitionEditor ref="definitionEditor" v-model="draft" :busy="busy" :errors="errors" />
      <fieldset class="field-applicability">
        <legend>Applies to</legend>
        <label><input v-model="mode" type="radio" name="field-applicability" value="all" :disabled="busy" /> All cards</label>
        <label><input v-model="mode" type="radio" name="field-applicability" value="selected" :disabled="busy" /> Selected card types</label>
        <div v-if="mode === 'selected'" class="field-type-choices">
          <label v-for="type in cardTypes" :key="type.id"><input v-model="selectedTypes" type="checkbox" :value="type.id" :disabled="busy" /> <span>{{ type.emoji }} {{ type.name }}</span></label>
        </div>
      </fieldset>
      <div v-if="impact && impact.affectedCardCount > 0" class="field-impact" role="status">
        <TriangleAlert :size="16" aria-hidden="true" />
        <div>
          <p v-if="impact.removedValueCount">{{ impact.removedValueCount }} populated {{ impact.removedValueCount === 1 ? 'value' : 'values' }} will be removed.</p>
          <p v-if="impact.replacedValueCount">{{ impact.replacedValueCount }} populated {{ impact.replacedValueCount === 1 ? 'value' : 'values' }} will be replaced.</p>
          <p>Previous values will be saved to comments.</p>
        </div>
      </div>
    </template>
    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="submit" class="btn" :disabled="busy || pending" :aria-label="customFieldId === null ? 'Create field' : 'Save field'"><Check :size="16" aria-hidden="true" /> {{ customFieldId === null ? 'Create' : 'Save' }}</button>
        <button type="button" class="btn btn--secondary" :disabled="busy" @click="close">Cancel</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>
<script setup lang="ts">
import { Check, TriangleAlert } from '@lucide/vue';
import { computed, nextTick, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useRoute, useRouter } from 'vue-router';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import CustomFieldDefinitionEditor from './CustomFieldDefinitionEditor.vue';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useCustomFieldStore } from '../stores/customFieldStore';
import { useCustomFieldPreview } from '../composables/useCustomFieldPreview';
import { buildCustomFieldDefinition, createCustomFieldDraft, retainCustomFieldInputErrors, type CustomFieldDraft } from '../utils/customFieldDraft';
import type { CustomFieldInput } from '../../shared/types/customFieldTypes';
import type { AppError } from '../../shared/types/appError';
const route = useRoute();
const router = useRouter();
const board = useBoardStore();
const store = useCustomFieldStore();
const { busy } = storeToRefs(store);
const { cardTypes } = storeToRefs(useCardTypeStore());
const draft = ref<CustomFieldDraft | null>(null);
const mode = ref<'all' | 'selected'>('all');
const selectedTypes = ref<number[]>([]);
const error = ref<AppError | null>(null);
const summary = ref<HTMLElement | null>(null);
const definitionEditor = ref<InstanceType<typeof CustomFieldDefinitionEditor> | null>(null);
const customFieldId = computed(() => route.name === 'custom-fields-new' ? null : Number(route.params.customFieldId));
const boardId = computed(() => board.currentBoardId!);
const original = ref<string>('');
const input = computed<CustomFieldInput | null>(() => draft.value ? {
  definition: buildCustomFieldDefinition(draft.value),
  appliesTo: mode.value === 'all' ? { mode: 'all' } : { mode: 'selected', cardTypeIds: [...selectedTypes.value] }
} : null);
const { impact, error: previewError, pending } = useCustomFieldPreview(() => {
  if (customFieldId.value === null || !input.value || JSON.stringify(input.value) === original.value) return null;
  return { boardId: boardId.value, customFieldId: customFieldId.value, field: input.value };
});
const errors = computed(() => ({ ...previewError.value?.validationErrors, ...error.value?.validationErrors }));
const summaryErrors = computed(() => Object.fromEntries(Object.entries(errors.value).filter(([path]) => !/^definition\.(code|name|dataType|emoji|defaultValue|minLength|maxLength|min|max)$/.test(path))));
watch([boardId, customFieldId], async () => {
  if (board.currentUserRole !== 'Owner') { await router.replace({ name: 'board', params: { boardId: boardId.value } }); return; }
  const field = store.fields.find(field => field.id === customFieldId.value);
  if (customFieldId.value !== null && !field) { await close(); return; }
  error.value = null;
  draft.value = createCustomFieldDraft(field?.definition);
  mode.value = field?.appliesTo.mode ?? 'all';
  selectedTypes.value = field?.appliesTo.mode === 'selected' ? [...field.appliesTo.cardTypeIds] : [];
  original.value = JSON.stringify(input.value);
}, { immediate: true });
watch(input, (current, previous) => {
  if (!error.value?.validationErrors || !current || !previous) return;
  const retained = retainCustomFieldInputErrors(error.value.validationErrors, previous, current);
  error.value = Object.keys(retained).length ? { ...error.value, validationErrors: retained } : null;
});
async function close() { if (!busy.value) await router.push({ name: 'custom-fields', params: { boardId: boardId.value } }); }
async function save() {
  if (busy.value || pending.value || !input.value) return;
  const result = await store.save(boardId.value, customFieldId.value, input.value);
  if (result.ok) { await close(); return; }
  error.value = result.error;
  await nextTick();
  if (!definitionEditor.value?.focusError()) summary.value?.focus();
}
</script>
<style scoped>
.field-applicability { display: grid; gap: 0.5rem; margin: 0.75rem 0 0; padding: 0.75rem; border: 1px solid var(--bo-border-soft); border-radius: 0.5rem; }
.field-applicability label { display: flex; gap: 0.4rem; align-items: center; }
.field-type-choices { display: grid; gap: 0.5rem; padding: 0.5rem 0 0 1.4rem; }
.field-type-choices p, .field-impact p { margin: 0; }
.field-impact { display: flex; gap: 0.5rem; padding: 0.75rem; border-radius: 0.5rem; background: var(--bo-toast-warning-bg); color: var(--bo-toast-warning-text); }
.field-impact svg { flex-shrink: 0; }
.field-errors { color: var(--bo-colour-danger-ink); font-size: 0.85rem; }
.field-errors p { margin: 0 0 0.35rem; }
</style>
