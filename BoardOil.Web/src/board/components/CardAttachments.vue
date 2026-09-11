<template>
  <section v-if="store.supported" class="card-attachments" aria-label="Attachments">
    <div class="attachment-heading">
      <span>Attachments</span>
      <button v-if="!readOnly" type="button" class="btn btn--secondary" :title="`Up to ${formatSize(store.maxUploadByteLength)} per file`" :disabled="store.busy || store.loading || store.maxUploadByteLength === 0" @click="selectFiles">Upload</button>
    </div>
    <input ref="picker" type="file" multiple hidden aria-label="Choose attachments" @change="filesSelected" />
    <small v-if="!readOnly">Uploads and deletions are saved immediately.</small>
    <small v-if="duplicating">Attachments will be copied when you create the duplicate.</small>
    <span v-if="store.loading" role="status">Loading attachments…</span>
    <span v-else-if="store.items.length === 0" class="attachment-empty">No attachments.</span>
    <ul v-if="store.items.length" class="attachment-list">
      <li v-for="item in store.items" :key="item.id" class="attachment-row">
        <div class="attachment-file">
          <a class="attachment-download" :href="buildApiUrl(`/api/boards/${boardId}/attachments/${item.id}/download`)" :download="item.originalFileName" :aria-label="`Download ${item.originalFileName}`" :title="formatSize(item.byteLength)" @click.left.exact.prevent="store.download(item.id)">{{ item.originalFileName }}</a>
        </div>
        <button v-if="!readOnly" type="button" class="btn btn--secondary" :disabled="store.busy" :aria-label="`Delete ${item.originalFileName}`" @click="deleteAttachment(item)"><Trash2 :size="14" aria-hidden="true" /></button>
      </li>
    </ul>
    <div v-for="(item, index) in readOnly ? [] : store.activeUploads" :key="index" class="attachment-upload" role="status">
      <span>{{ item.file.name }}</span>
      <progress v-if="item.status === 'uploading'" :value="item.progress" max="100" :aria-label="`Uploading ${item.file.name}`" />
      <small v-if="item.status === 'uploading'">{{ item.progress === 100 ? 'Finishing…' : `${item.progress}%` }}</small>
    </div>
  </section>
  <Teleport to="body">
    <FixedChromeDialog :open="store.warningMessages.length > 0" title="Attachment warning"
      close-label="Close attachment warning" @close="store.clearWarnings" @submit="store.clearWarnings">
      <ul>
        <li v-for="(message, index) in store.warningMessages" :key="index">{{ message }}</li>
      </ul>
      <template #actions>
        <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
          <button type="submit" class="btn">OK</button>
        </div>
      </template>
    </FixedChromeDialog>
  </Teleport>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue';
import { Trash2 } from '@lucide/vue';
import { useAttachmentStore } from '../stores/attachmentStore';
import { buildApiUrl } from '../../shared/api/config';
import { formatAttachmentSize as formatSize } from '../utils/formatAttachmentSize';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import type { CardAttachment } from '../../shared/types/attachmentTypes';

const props = withDefaults(defineProps<{ boardId: number; cardId: number; archived?: boolean; readOnly?: boolean; duplicating?: boolean }>(),
  { archived: false, readOnly: false, duplicating: false });
const store = useAttachmentStore();
const { confirm } = useConfirm();
const picker = ref<HTMLInputElement | null>(null);
watch(() => [props.boardId, props.cardId, props.archived] as const,
  ([boardId, cardId, archived]) => { void store.open(boardId, cardId, archived); }, { immediate: true });
onBeforeUnmount(store.clear);

function selectFiles() { picker.value?.click(); }
async function filesSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);
  input.value = '';
  await store.upload(files);
}
async function deleteAttachment(item: CardAttachment) {
  if (await confirm({ title: 'Delete attachment', message: `Delete "${item.originalFileName}"?`, confirmLabel: 'Delete', danger: true })) {
    await store.remove(item.id);
  }
}
</script>

<style scoped>
.card-attachments {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  min-width: 0;
  padding-block: 0.75rem;
  border-top: 1px solid var(--bo-border-soft);
  border-bottom: 1px solid var(--bo-border-soft);
}
.attachment-heading, .attachment-row { display: flex; align-items: center; justify-content: space-between; gap: 0.4rem; }
.attachment-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.45rem; }
.attachment-file, .attachment-upload { display: flex; flex-direction: column; gap: 0.2rem; min-width: 0; overflow-wrap: anywhere; }
.attachment-download { padding: 0; border: 0; background: transparent; color: var(--bo-ink); text-align: left; text-decoration: underline; cursor: pointer; overflow-wrap: anywhere; }
.attachment-empty, small { color: var(--bo-ink-muted); font-size: 0.8rem; }
progress { width: 100%; }
</style>
