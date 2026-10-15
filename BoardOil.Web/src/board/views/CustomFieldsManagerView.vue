<template>
  <section v-if="isOwner" class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Custom fields <span class="field-count">{{ fields.length }}/10</span></h2>
      <button type="button" class="btn" :disabled="busy || fields.length >= 10" @click="create"><Plus :size="16" aria-hidden="true" /> Add field</button>
    </header>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <BoGrid class="fields-grid" :columns="columns" :items="rows" :row-attributes="rowAttributes"
      :total-count="fields.length" :offset="0" :limit="10" :show-pagination-controls="false" empty-text="No custom fields yet.">
      <template #cell(id)="{ row }">
        <button type="button" class="bo-drag-handle" :draggable="!busy" :disabled="busy" :aria-label="`Drag to reorder ${row.name}`"
          @dragstart="(event: DragEvent) => startDrag(Number(row.id), event)" @dragend="endDrag"><GripVertical :size="16" aria-hidden="true" /></button>
      </template>
      <template #cell(name)="{ row }">
        <button type="button" class="entity-row-main-button field-name" :disabled="busy" :aria-label="`Edit field ${row.name}`" @click="edit(Number(row.id))">
          <span class="bo-emoji">{{ row.emoji }}</span> {{ row.name }}
        </button>
      </template>
      <template #cell(code)="{ row }"><code class="field-cell-text">{{ row.code }}</code></template>
      <template #cell(type)="{ row }">{{ row.type }}</template>
      <template #cell(applicability)="{ row }"><span class="field-cell-text">{{ row.applicability }}</span></template>
      <template #cell(actions)="{ row }">
        <button type="button" class="btn btn--danger entity-row-action-icon" :disabled="busy" :aria-label="`Remove ${row.name}`" @click="remove(row.field as CustomField)"><Trash2 :size="16" aria-hidden="true" /></button>
      </template>
    </BoGrid>
  </section>
</template>
<script setup lang="ts">
import { GripVertical, Plus, Trash2 } from '@lucide/vue';
import { computed, onMounted, ref, type HTMLAttributes } from 'vue';
import { storeToRefs } from 'pinia';
import { useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useCustomFieldStore } from '../stores/customFieldStore';
import { useConfirm } from '../../shared/composables/useConfirm';
import BoGrid from '../../shared/components/BoGrid.vue';
import type { CustomField } from '../../shared/types/customFieldTypes';
const router = useRouter();
const board = useBoardStore();
const store = useCustomFieldStore();
const types = useCardTypeStore();
const { fields } = storeToRefs(store);
const { confirm } = useConfirm();
const removing = ref(false);
const busy = computed(() => store.busy || removing.value);
const isOwner = computed(() => board.currentUserRole === 'Owner');
const error = ref('');
const dragging = ref<number | null>(null);
const dragOver = ref<number | null>(null);
const typeLabels = { text: 'Text', integer: 'Number', date: 'Date' };
const columns = [
  { key: 'id', label: '', width: '3.5rem', rowKeyColumn: true },
  { key: 'name', label: 'Name', width: 'minmax(0, 1.2fr)' },
  { key: 'code', label: 'Code' },
  { key: 'type', label: 'Type', width: 'minmax(0, 0.7fr)' },
  { key: 'applicability', label: 'Applies to', width: 'minmax(0, 1.5fr)' },
  { key: 'actions', label: '', width: '3.5rem' }
];
const rows = computed(() => fields.value.map(field => ({
  id: field.id,
  code: field.definition.code,
  name: field.definition.name,
  emoji: field.definition.emoji,
  type: typeLabels[field.definition.dataType],
  applicability: applicability(field),
  field
})));
function rowAttributes(row: Record<string, unknown>): HTMLAttributes {
  const customFieldId = Number(row.id);
  return {
    class: { 'field-drop-target': dragOver.value === customFieldId },
    onDragover: (event: DragEvent) => dragOverField(customFieldId, event),
    onDragleave: () => { dragOver.value = null; },
    onDrop: (event: DragEvent) => drop(customFieldId, event)
  };
}
onMounted(() => { if (!isOwner.value) void router.replace({ name: 'board', params: { boardId: board.currentBoardId } }); });
function create() { return router.push({ name: 'custom-fields-new', params: { boardId: board.currentBoardId } }); }
function edit(customFieldId: number) { return router.push({ name: 'custom-fields-field', params: { boardId: board.currentBoardId, customFieldId } }); }
function applicability(field: CustomField) {
  if (field.appliesTo.mode === 'all') return 'All cards';
  if (!field.appliesTo.cardTypeIds.length) return 'Not assigned';
  return field.appliesTo.cardTypeIds.map(id => types.getCardTypeById(id)?.name).filter(Boolean).join(', ');
}
async function move(from: number, to: number) {
  if (busy.value || from === to) return;
  const boardId = board.currentBoardId!;
  const ids = fields.value.map(field => field.id);
  const [customFieldId] = ids.splice(from, 1);
  ids.splice(to, 0, customFieldId!);
  error.value = '';
  const result = await store.reorder(boardId, ids);
  if (board.currentBoardId === boardId && !result.ok) error.value = result.error.message;
}
function startDrag(customFieldId: number, event: DragEvent) {
  if (busy.value) { event.preventDefault(); return; }
  dragging.value = customFieldId;
  if (event.dataTransfer) { event.dataTransfer.effectAllowed = 'move'; event.dataTransfer.setData('text/plain', String(customFieldId)); }
}
function dragOverField(customFieldId: number, event: DragEvent) {
  if (!dragging.value || busy.value) return;
  event.preventDefault();
  dragOver.value = customFieldId;
}
function endDrag() { dragging.value = null; dragOver.value = null; }
async function drop(customFieldId: number, event: DragEvent) {
  if (!dragging.value) return;
  event.preventDefault();
  const from = fields.value.findIndex(field => field.id === dragging.value);
  const to = fields.value.findIndex(field => field.id === customFieldId);
  endDrag();
  if (from >= 0 && to >= 0) await move(from, to);
}
async function remove(field: CustomField) {
  if (busy.value) return;
  const boardId = board.currentBoardId!;
  removing.value = true;
  error.value = '';
  try {
    const preview = await store.preview(boardId, field.id, null);
    if (board.currentBoardId !== boardId) return;
    if (!preview.ok) { error.value = preview.error.message; return; }
    const count = preview.data.removedValueCount;
    const accepted = await confirm({ title: 'Remove custom field', confirmLabel: 'Remove', danger: true,
      message: `Remove "${field.definition.name}"?\n\n${count} populated ${count === 1 ? 'value' : 'values'} will be removed. Previous values will be saved to comments.` });
    if (!accepted || board.currentBoardId !== boardId) return;
    const result = await store.remove(boardId, field.id);
    if (!result.ok && board.currentBoardId === boardId) error.value = result.error.message;
  } finally { removing.value = false; }
}
</script>
<style scoped>
.fields-grid { height: auto; }
.field-count { color: var(--bo-ink-subtle); font-size: 0.85rem; font-weight: 400; }
.field-name { text-align: left; font-weight: 600; overflow-wrap: anywhere; }
.field-cell-text { overflow-wrap: anywhere; }
.fields-grid :deep(.field-drop-target) { outline: 2px solid var(--bo-focus-ring); outline-offset: -2px; }
</style>
