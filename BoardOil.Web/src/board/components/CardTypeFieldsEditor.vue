<template>
  <section class="field-editor" aria-label="Custom Fields">
    <div class="field-toolbar">
      <div class="field-toolbar-title">
        <h4>Custom Fields</h4>
        <span class="field-note">{{ model.length }}/10</span>
      </div>
      <button ref="addButton" type="button" class="btn btn--secondary" :disabled="busy || model.length >= 10" @click="addField">
        <Plus :size="16" aria-hidden="true" /> Add field
      </button>
    </div>
    <div class="field-scroll">
      <div v-if="Object.keys(summaryErrors).length" ref="errorSummary" class="field-summary" role="alert" tabindex="-1">
        <p v-for="(messages, path) in summaryErrors" :key="path">
          <CircleAlert :size="14" aria-hidden="true" />
          <span><button v-if="fieldErrorIndex(path, model) !== null" type="button" class="btn btn--secondary field-error-link" @click="showSummaryError(path)">{{ describeFieldError(path, model) }}</button>
          <span v-else>{{ describeFieldError(path, model) }}</span>: {{ messages.join(' ') }}</span>
        </p>
      </div>
      <p v-if="model.length === 0" class="field-note field-empty">No custom fields yet.</p>
      <p v-if="hasConversions" class="field-note">Removing fields or changing their type can discard values. Discarded values will be recorded on each affected card when you save.</p>
      <ol class="entity-rows-list">
        <li v-for="(field, index) in model" :key="field.key" class="entity-row field-row">
          <div class="field-row-header">
            <button
              :id="`field-heading-${field.key}`"
              type="button" class="entity-row-main entity-row-main-button"
              :aria-expanded="expandedKey === field.key" :aria-controls="`field-panel-${field.key}`"
              :disabled="busy" @click="toggleField(field.key)"
            >
              <ChevronDown :size="16" class="field-chevron" :class="{ 'field-chevron--open': expandedKey === field.key }" aria-hidden="true" />
              <span class="field-heading-copy">
                <span class="entity-row-title">{{ field.emoji }} {{ field.displayName.trim() || 'New field' }}</span>
                <span class="field-note">{{ typeLabels[field.type] }} · {{ field.required ? 'Required' : 'Optional' }}</span>
                <span v-if="expandedKey !== field.key && fieldHasErrors(index)" class="error field-inline-error field-error-indicator"><CircleAlert :size="12" aria-hidden="true" /> Validation errors</span>
              </span>
            </button>
            <div class="entity-row-actions">
              <button type="button" class="btn btn--secondary entity-row-action-icon" :disabled="busy || index === 0" :aria-label="`Move ${field.displayName || 'field'} up`" title="Move up" @click="moveField(index, -1)"><ArrowUp :size="16" aria-hidden="true" /></button>
              <button type="button" class="btn btn--secondary entity-row-action-icon" :disabled="busy || index === model.length - 1" :aria-label="`Move ${field.displayName || 'field'} down`" title="Move down" @click="moveField(index, 1)"><ArrowDown :size="16" aria-hidden="true" /></button>
            </div>
          </div>
          <Transition name="field-expand" @after-enter="focusRevealedField(field.key)" @before-leave="onFieldPanelClosing">
            <div v-if="expandedKey === field.key" :id="`field-panel-${field.key}`" role="region" :aria-labelledby="`field-heading-${field.key}`" class="field-panel-expansion">
              <div class="field-panel-clip">
                <div class="field-panel">
                  <div class="field-pair">
                    <div class="field-control">
                      <label>
                        Display name
                        <input :value="field.displayName" @input="(event: Event) => onDisplayNameInput(field, event)" maxlength="40" :disabled="busy" autocomplete="off" :aria-invalid="hasError(index, 'displayName')" :aria-describedby="hasError(index, 'displayName') ? fieldErrorId(field, 'displayName') : undefined" />
                      </label>
                      <small v-if="hasError(index, 'displayName')" :id="fieldErrorId(field, 'displayName')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, 'displayName') }}</span></small>
                    </div>
                    <div class="field-control">
                      <label>
                        Field code
                        <input v-model="field.fieldCode" @input="markFieldCodeEdited(field)" maxlength="40" :readonly="field.persisted" :disabled="busy" autocomplete="off" spellcheck="false" :aria-invalid="hasError(index, 'fieldCode')" :aria-describedby="hasError(index, 'fieldCode') ? fieldErrorId(field, 'fieldCode') : undefined" />
                      </label>
                      <small v-if="hasError(index, 'fieldCode')" :id="fieldErrorId(field, 'fieldCode')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, 'fieldCode') }}</span></small>
                    </div>
                  </div>
                  <div class="field-pair">
                    <div class="field-control">
                      <label>
                        Type
                        <select v-model="field.type" :disabled="busy" :aria-invalid="hasError(index, 'type')" @change="resetFieldTypeOptions(field)" :aria-describedby="hasError(index, 'type') ? fieldErrorId(field, 'type') : undefined">
                          <option value="text">Text</option><option value="integer">Number</option><option value="date">Date</option>
                        </select>
                      </label>
                      <small v-if="hasError(index, 'type')" :id="fieldErrorId(field, 'type')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, 'type') }}</span></small>
                    </div>
                    <div class="field-control">
                      <label class="field-emoji">
                        Emoji
                        <EmojiPickerDropdown v-model="field.emoji" :disabled="busy" :aria-invalid="hasError(index, 'emoji')" :teleport="false" placeholder="Select emoji" :aria-describedby="hasError(index, 'emoji') ? fieldErrorId(field, 'emoji') : undefined" />
                      </label>
                      <small v-if="hasError(index, 'emoji')" :id="fieldErrorId(field, 'emoji')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, 'emoji') }}</span></small>
                    </div>
                  </div>
                  <div class="field-flags">
                    <label class="field-checkbox"><input v-model="field.required" type="checkbox" :disabled="busy" /> Required</label>
                    <label class="field-checkbox"><input v-model="field.showOnCard" type="checkbox" :disabled="busy" /> Show on cards</label>
                  </div>
                  <div class="field-control">
                    <label>
                      Default value{{ field.required ? ' (required)' : '' }}
                      <input v-if="field.type === 'date'" v-model="field.defaultValue" type="date" :disabled="busy" :aria-invalid="hasError(index, 'defaultValue')" :aria-describedby="hasError(index, 'defaultValue') ? fieldErrorId(field, 'defaultValue') : undefined" />
                      <input v-else v-model="field.defaultValue" type="text" :inputmode="field.type === 'integer' ? 'numeric' : 'text'" :maxlength="field.type === 'text' ? 250 : undefined" :disabled="busy" autocomplete="off" :aria-invalid="hasError(index, 'defaultValue')" :aria-describedby="hasError(index, 'defaultValue') ? fieldErrorId(field, 'defaultValue') : undefined" />
                    </label>
                    <small v-if="hasError(index, 'defaultValue')" :id="fieldErrorId(field, 'defaultValue')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, 'defaultValue') }}</span></small>
                  </div>
                  <div v-if="field.type !== 'date'" class="field-pair">
                    <div class="field-control">
                      <label>
                        {{ field.type === 'text' ? 'Minimum length' : 'Minimum value' }}
                        <input v-model="field.minimum" type="text" inputmode="numeric" :disabled="busy" placeholder="No minimum" :aria-invalid="hasError(index, field.type === 'text' ? 'minLength' : 'min')" :aria-describedby="hasError(index, field.type === 'text' ? 'minLength' : 'min') ? fieldErrorId(field, field.type === 'text' ? 'minLength' : 'min') : undefined" />
                      </label>
                      <small v-if="hasError(index, field.type === 'text' ? 'minLength' : 'min')" :id="fieldErrorId(field, field.type === 'text' ? 'minLength' : 'min')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, field.type === 'text' ? 'minLength' : 'min') }}</span></small>
                    </div>
                    <div class="field-control">
                      <label>
                        {{ field.type === 'text' ? 'Maximum length' : 'Maximum value' }}
                        <input v-model="field.maximum" type="text" inputmode="numeric" :disabled="busy" :placeholder="field.type === 'text' ? '250' : 'No maximum'" :aria-invalid="hasError(index, field.type === 'text' ? 'maxLength' : 'max')" :aria-describedby="hasError(index, field.type === 'text' ? 'maxLength' : 'max') ? fieldErrorId(field, field.type === 'text' ? 'maxLength' : 'max') : undefined" />
                      </label>
                      <small v-if="hasError(index, field.type === 'text' ? 'maxLength' : 'max')" :id="fieldErrorId(field, field.type === 'text' ? 'maxLength' : 'max')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(index, field.type === 'text' ? 'maxLength' : 'max') }}</span></small>
                    </div>
                  </div>
                  <p v-if="field.type === 'text'" class="field-note">Single line, up to 250 characters. Blank text counts as empty.</p>
                  <div class="field-remove">
                    <button type="button" class="btn btn--secondary" :disabled="busy" @click="removeField(index)"><Trash2 :size="16" aria-hidden="true" /> Remove field</button>
                  </div>
                </div>
              </div>
            </div>
          </Transition>
        </li>
      </ol>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ArrowDown, ArrowUp, ChevronDown, CircleAlert, Plus, Trash2 } from '@lucide/vue';
import { computed, nextTick, ref } from 'vue';
import EmojiPickerDropdown from '../../shared/components/EmojiPickerDropdown.vue';
import type { CardFieldDefinition } from '../../shared/types/cardFieldTypes';
import { createCardFieldDraft, describeFieldError, fieldErrorIndex, isInlineFieldError, resetFieldTypeOptions, updateFieldDisplayName, type CardFieldDraft } from '../utils/cardFieldDraft';

const model = defineModel<CardFieldDraft[]>({ required: true });
const props = defineProps<{
  busy: boolean;
  errors: Record<string, string[]>;
  originalFields: CardFieldDefinition[];
}>();
const expandedKey = ref<string | null>(null);
const pendingFocusKey = ref<string | null>(null);
const addButton = ref<HTMLButtonElement | null>(null);
const errorSummary = ref<HTMLElement | null>(null);
const summaryErrors = computed(() => Object.fromEntries(Object.entries(props.errors)
  .filter(([path]) => (path.startsWith('fields') || path.startsWith('cards[')) && !isInlineFieldError(path))));
const typeLabels = { text: 'Text', integer: 'Number', date: 'Date' };
const hasConversions = computed(() => props.originalFields.some(original => {
  const draft = model.value.find(field => field.fieldCode === original.fieldCode);
  return !draft || draft.type !== original.type;
}));

function onDisplayNameInput(field: CardFieldDraft, event: Event) {
  updateFieldDisplayName(field, (event.target as HTMLInputElement).value);
}

function markFieldCodeEdited(field: CardFieldDraft) {
  field.fieldCodeEdited = true;
}

function fieldError(index: number, property: string) {
  return props.errors[`fields[${index}].${property}`]?.join(' ') ?? '';
}

function fieldErrorId(field: CardFieldDraft, property: string) {
  return `field-error-${field.key}-${property}`;
}

function fieldHasErrors(index: number) {
  return Object.keys(props.errors).some(path => fieldErrorIndex(path, model.value) === index);
}

function focusSummary() {
  errorSummary.value?.focus();
}

async function showSummaryError(path: string) {
  const index = fieldErrorIndex(path, model.value);
  if (index !== null) await revealField(index);
}

function hasError(index: number, property: string) {
  return Boolean(props.errors[`fields[${index}].${property}`]);
}

function toggleField(key: string) {
  pendingFocusKey.value = null;
  expandedKey.value = expandedKey.value === key ? null : key;
}

async function revealField(index: number) {
  const field = model.value[index];
  if (!field) return;
  pendingFocusKey.value = field.key;
  if (expandedKey.value === field.key) {
    await nextTick();
    focusRevealedField(field.key);
    return;
  }
  expandedKey.value = field.key;
}

function onFieldPanelClosing(element: Element) {
  element.setAttribute('inert', '');
}

function focusRevealedField(key: string) {
  if (pendingFocusKey.value !== key) return;
  pendingFocusKey.value = null;
  const panel = document.getElementById(`field-panel-${key}`);
  const invalid = panel?.querySelector<HTMLElement>('[aria-invalid="true"]');
  const input = invalid?.querySelector<HTMLElement>('button, input, select') ?? invalid ?? panel?.querySelector<HTMLInputElement>('input');
  input?.focus();
}

async function addField() {
  if (model.value.length >= 10) return;
  model.value = [...model.value, createCardFieldDraft()];
  await nextTick();
  await revealField(model.value.length - 1);
}

function moveField(index: number, direction: number) {
  const next = [...model.value];
  const [field] = next.splice(index, 1);
  next.splice(index + direction, 0, field);
  model.value = next;
}

function removeField(index: number) {
  model.value = model.value.filter((_, position) => position !== index);
}

function focusAddButton() {
  addButton.value?.focus();
}

defineExpose({ revealField, focusAddButton, focusSummary });
</script>

<style scoped>
.field-control {
  display: grid;
  align-content: start;
  min-width: 0;
  gap: 0.2rem;
}

.field-editor .field-inline-error {
  display: flex;
  align-items: flex-start;
  gap: 0.25rem;
  margin: 0;
  color: var(--bo-colour-danger-ink);
  font-size: 0.75rem;
  font-weight: 500;
  line-height: 1.25;
}

.field-inline-error > svg, .field-summary p > svg {
  flex-shrink: 0;
  margin-top: 0.1em;
}

.field-editor .field-error-indicator {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
}

.field-summary {
  margin-bottom: 0.65rem;
  font-size: 0.85rem;
  color: var(--bo-colour-danger-ink);
}

.field-summary p {
  display: flex;
  align-items: flex-start;
  gap: 0.25rem;
  margin: 0 0 0.35rem;
}

.field-error-link {
  padding: 0;
  border: none;
  background: transparent;
  color: inherit;
  text-decoration: underline;
  text-align: left;
}

.field-editor {
  display: grid;
  grid-template-rows: auto minmax(0, 1fr);
  gap: 0.5rem;
  height: 100%;
  min-height: 0;
  min-width: 0;
}

.field-scroll {
  overflow-y: auto;
  overscroll-behavior: contain;
  min-height: 0;
  padding: 2px 4px 2px 2px;
}

.field-scroll > .field-note {
  margin-bottom: 0.5rem;
}

.field-toolbar-title {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  white-space: nowrap;
}

.field-toolbar-title h4 {
  margin: 0;
  font-size: 1rem;
}

.field-toolbar, .field-row-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.field-note {
  margin: 0;
  color: var(--bo-ink-subtle);
  font-size: 0.85rem;
}

.field-empty {
  padding: 1rem 0;
}

.field-row {
  display: block;
  min-width: 0;
}

.field-heading-copy {
  display: grid;
  gap: 0.2rem;
  min-width: 0;
  overflow-wrap: anywhere;
}

.field-chevron {
  flex-shrink: 0;
  transform: rotate(-90deg);
  transition: transform 220ms ease;
}

.field-chevron--open {
  transform: none;
}

.field-panel-expansion {
  display: grid;
  grid-template-rows: 1fr;
}

.field-panel-clip {
  min-height: 0;
  overflow: hidden;
}

.field-expand-enter-active,
.field-expand-leave-active {
  transition: grid-template-rows 220ms ease, opacity 220ms ease;
}

.field-expand-enter-from,
.field-expand-leave-to {
  grid-template-rows: 0fr;
  opacity: 0;
}

.field-panel {
  display: grid;
  gap: 0.65rem;
  margin-top: 0.8rem;
  padding-top: 0.8rem;
  border-top: 1px solid var(--bo-border-soft);
}

.field-pair {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 0.75rem;
}

.field-pair label, .field-panel input, .field-panel select {
  min-width: 0;
}

.field-emoji {
  display: grid;
  gap: 0.25rem;
  align-content: start;
  font-size: 0.85rem;
}

.field-flags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1rem;
}

.field-panel .field-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.field-checkbox input {
  margin: 0;
}

.field-remove {
  display: flex;
  justify-content: flex-end;
}

input[readonly] {
  color: var(--bo-ink-subtle);
  background: var(--bo-surface-panel);
}

input[aria-invalid="true"], select[aria-invalid="true"] {
  border-color: var(--bo-colour-danger);
}

@media (max-width: 420px) {
  .field-pair {
    grid-template-columns: minmax(0, 1fr);
  }

}
@media (prefers-reduced-motion: reduce) {
  .field-chevron,
  .field-expand-enter-active,
  .field-expand-leave-active {
    transition: none;
  }
}
</style>
