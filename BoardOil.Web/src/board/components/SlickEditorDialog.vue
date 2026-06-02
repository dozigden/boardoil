<template>
  <ModalDialog
    :open="isCreateMode || editingSlick !== null"
    :title="dialogTitle"
    :close-label="isCreateMode ? 'Cancel creating' : 'Cancel editing'"
    @close="closeSlickEditor"
    @submit="saveSlick"
  >
    <template v-if="draft">
      <div class="slicks-dialog-preview">
        <span class="badge">Preview</span>
        <span class="tag" :class="previewStyleClasses" :style="previewStyle" :aria-label="previewSlickName">
          {{ previewSlickName }}
        </span>
      </div>

      <label>
        Name
        <input
          :value="draftSlickName"
          maxlength="40"
          :placeholder="isCreateMode ? 'New slick name' : 'Slick name'"
          :disabled="busy"
          autocomplete="off"
          data-lpignore="true"
          @input="setDraftSlickName(($event.target as HTMLInputElement).value)"
        />
      </label>

      <label>
        Style
        <select :value="draft.styleName" :disabled="busy" @change="setStyleName(parseSlickStyleNameInput(($event.target as HTMLSelectElement).value))">
          <option value="solid">Solid</option>
          <option value="presets">Presets</option>
        </select>
      </label>

      <template v-if="draft.styleName === 'presets'">
        <label>
          Preset
          <div class="slicks-preset-picker" role="radiogroup" aria-label="Slick preset colour">
            <button
              v-for="preset in presetColours"
              :key="preset.cssVar"
              type="button"
              class="slicks-preset-swatch"
              :class="{ 'slicks-preset-swatch--selected': draft.presetIndex === preset.index }"
              :style="{ backgroundColor: preset.cssValue }"
              :disabled="busy"
              :aria-pressed="draft.presetIndex === preset.index"
              :aria-label="`Preset ${preset.index + 1}`"
              @click="setDraftField('presetIndex', preset.index)"
            />
          </div>
        </label>
      </template>

      <template v-else-if="draft.styleName === 'solid'">
        <label>
          Background Color
          <input
            type="color"
            class="slicks-colour-input"
            :value="draft.backgroundColor"
            :disabled="busy"
            @input="setDraftField('backgroundColor', ($event.target as HTMLInputElement).value)"
          />
        </label>

        <label>
          Text Color Mode
          <select :value="draft.textColorMode" :disabled="busy" @change="setTextMode(($event.target as HTMLSelectElement).value)">
            <option value="auto">Auto Contrast</option>
            <option value="custom">Custom</option>
          </select>
        </label>

        <label v-if="draft.textColorMode === 'custom'">
          Text Color
          <input
            type="color"
            class="slicks-colour-input"
            :value="draft.textColor"
            :disabled="busy"
            @input="setDraftField('textColor', ($event.target as HTMLInputElement).value)"
          />
        </label>

        <label>
          Border
          <select :value="draft.borderMode" :disabled="busy" @change="setBorderMode(($event.target as HTMLSelectElement).value)">
            <option value="auto">Auto</option>
            <option value="custom">Custom</option>
            <option value="none">None</option>
          </select>
        </label>

        <label v-if="draft.borderMode === 'custom'">
          Border Color
          <input
            type="color"
            class="slicks-colour-input"
            :value="draft.borderColor"
            :disabled="busy"
            @input="setDraftField('borderColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>
    </template>

    <template #actions>
      <div v-if="draft" class="editor-actions card-modal-actions">
        <button
          v-if="!isCreateMode && editingSlick"
          type="button"
          class="btn btn--danger"
          :disabled="busy"
          aria-label="Delete slick"
          title="Delete slick"
          @click="deleteEditingSlick"
        >
          <Trash2 :size="16" aria-hidden="true" />
        </button>
        <span v-else />
        <div class="card-modal-actions-left">
          <button type="submit" class="btn" :disabled="busy || !hasValidDraftSlickName" :aria-label="saveButtonAriaLabel" :title="saveButtonAriaLabel">
            <Check :size="16" aria-hidden="true" />
            <span>{{ saveButtonLabel }}</span>
          </button>
          <button type="button" class="btn btn--secondary" :disabled="busy" aria-label="Cancel editing" title="Cancel" @click="closeSlickEditor">
            <X :size="16" aria-hidden="true" />
            <span>Cancel</span>
          </button>
        </div>
      </div>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { Check, Trash2, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useCardStore } from '../stores/cardStore';
import { useBoardStore } from '../stores/boardStore';
import { useSlickStore } from '../stores/slickStore';
import { useStyleDraft } from '../composables/useStyleDraft';
import { SLICK_PRESET_TOKENS } from '../../shared/utils/presetTheme';
import { createStyleDraft } from '../../shared/utils/styleDraftAdapter';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';
import type { Slick, SlickEditModel, SlickStyleName } from '../../shared/types/boardTypes';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { useConfirm } from '../../shared/composables/useConfirm';

const route = useRoute();
const router = useRouter();
const cardStore = useCardStore();
const boardStore = useBoardStore();
const slickStore = useSlickStore();
const { confirm } = useConfirm();
const { currentBoardId } = storeToRefs(boardStore);
const { busy } = storeToRefs(slickStore);
const { createSlick, updateSlick, deleteSlick, getSlickById, loadSlicks } = slickStore;
const { removeSlickFromCards } = cardStore;
const {
  draft,
  stylePropertiesJson,
  setDraft,
  clearDraft,
  setStyleName,
  setTextMode,
  setBorderMode,
  setField: setDraftField
} = useStyleDraft();
const draftSlickName = ref('');
const draftSourceKey = ref<string | null>(null);
const presetColours = SLICK_PRESET_TOKENS;

const isCreateMode = computed(() => route.name === 'slicks-new');
const routeSlickId = computed<number | null>(() => {
  const rawSlickId = route.params.slickId;
  const parsedSlickId = typeof rawSlickId === 'string'
    ? Number.parseInt(rawSlickId, 10)
    : Number.NaN;
  if (!Number.isFinite(parsedSlickId)) {
    return null;
  }

  return parsedSlickId;
});

const boardId = computed(() => currentBoardId.value!);

const editingSlick = computed(() => getSlickById(routeSlickId.value));
const dialogTitle = computed(() => {
  if (isCreateMode.value) {
    return 'Add Slick';
  }

  if (editingSlick.value) {
    return `Edit Slick: ${editingSlick.value.name}`;
  }

  return 'Edit Slick';
});
const previewSlickName = computed(() => {
  const value = draftSlickName.value.trim();
  if (value.length > 0) {
    return value;
  }

  if (isCreateMode.value) {
    return 'New slick';
  }

  return editingSlick.value?.name ?? 'Slick';
});
const hasValidDraftSlickName = computed(() => draftSlickName.value.trim().length > 0);
const saveButtonLabel = computed(() => (isCreateMode.value ? 'Create' : 'Save'));
const saveButtonAriaLabel = computed(() => (isCreateMode.value ? 'Create slick' : 'Save slick'));
const previewStyle = computed(() => {
  const style = draft.value
    ? {
        styleName: draft.value.styleName,
        stylePropertiesJson: stylePropertiesJson.value ?? '{}'
      }
    : editingSlick.value;
  return getSurfaceStyle(style, {
    fallbackBackground: 'var(--panel)',
    fallbackColor: 'var(--text)',
    fallbackBorderColor: 'var(--line)'
  });
});
const previewStyleClasses = computed(() => {
  const style = draft.value
    ? {
        styleName: draft.value.styleName,
        stylePropertiesJson: stylePropertiesJson.value ?? '{}'
      }
    : editingSlick.value;
  return getSemanticStyleClasses(style, 'slick');
});

async function closeSlickEditor() {
  clearDraft();
  draftSlickName.value = '';
  draftSourceKey.value = null;

  await router.push({ name: 'slicks', params: { boardId: boardId.value } });
}

function setDraftSlickName(value: string) {
  draftSlickName.value = value;
}

async function saveSlick() {
  if (!draft.value || !stylePropertiesJson.value) {
    return;
  }

  const name = draftSlickName.value.trim();
  if (!name) {
    return;
  }
  const saveModel: SlickEditModel = {
    name,
    styleName: resolveDraftSlickStyleName(draft.value.styleName),
    stylePropertiesJson: stylePropertiesJson.value
  };

  if (isCreateMode.value) {
    const created = await createSlick(saveModel, boardId.value);
    if (!created) {
      return;
    }

    await closeSlickEditor();
    return;
  }

  if (!editingSlick.value) {
    return;
  }

  const updated = await updateSlick(editingSlick.value.id, saveModel, boardId.value);
  if (!updated) {
    return;
  }

  await closeSlickEditor();
}

async function deleteEditingSlick() {
  if (!editingSlick.value) {
    return;
  }

  const shouldDelete = await confirm({
    title: 'Delete slick',
    message: `Delete slick "${editingSlick.value.name}"?\n\nCards in this slick will become unslicked.`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!shouldDelete) {
    return;
  }

  const slickId = editingSlick.value.id;
  const deleted = await deleteSlick(slickId, boardId.value);
  if (!deleted) {
    return;
  }

  removeSlickFromCards(slickId);
  await closeSlickEditor();
}

function initialiseCreateDraftState() {
  draftSlickName.value = '';
  draftSourceKey.value = 'create';
  setDraft(createStyleDraft({
    styleName: 'presets',
    stylePropertiesJson: '{"presetIndex":2}'
  }));
}

function initialiseEditDraftState(slick: Slick, slickId: number) {
  draftSourceKey.value = `edit:${slickId}`;
  setDraft(createStyleDraft({
    styleName: slick.styleName,
    stylePropertiesJson: slick.stylePropertiesJson
  }));
  draftSlickName.value = slick.name;
}

watch(
  [boardId, routeSlickId, isCreateMode, editingSlick],
  async ([nextBoardId, nextSlickId, nextIsCreate, nextSlick]) => {
    if (nextIsCreate) {
      if (draftSourceKey.value !== 'create') {
        initialiseCreateDraftState();
      }

      return;
    }

    if (!nextIsCreate && nextSlickId === null) {
      clearDraft();
      draftSlickName.value = '';
      draftSourceKey.value = null;
      return;
    }

    if (!nextSlick && (slickStore.activeBoardId !== nextBoardId || slickStore.slicks.length === 0)) {
      const loaded = await loadSlicks(nextBoardId);
      if (!loaded) {
        return;
      }

      nextSlick = getSlickById(nextSlickId);
    }

    if (!nextSlick || nextSlickId === null) {
      await router.replace({ name: 'slicks', params: { boardId: nextBoardId } });
      return;
    }

    const sourceKey = `edit:${nextSlickId}`;
    if (draftSourceKey.value !== sourceKey) {
      initialiseEditDraftState(nextSlick, nextSlickId);
    }
  },
  { immediate: true }
);

function parseSlickStyleNameInput(value: string): 'solid' | 'presets' {
  return value === 'solid' ? 'solid' : 'presets';
}

function resolveDraftSlickStyleName(styleName: string): SlickStyleName {
  if (styleName === 'solid') {
    return 'solid';
  }

  return 'presets';
}
</script>

<style scoped>
.slicks-dialog-preview {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.slicks-colour-input {
  width: 100%;
}

.slicks-preset-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.slicks-preset-swatch {
  width: 1.7rem;
  height: 1.7rem;
  border-radius: 999px;
  border: 1px solid var(--bo-border-default);
  cursor: pointer;
  padding: 0;
}

.slicks-preset-swatch--selected {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: 1px;
}
</style>
