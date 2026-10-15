<template>
  <div ref="root" class="field-definition-editor">
    <div class="field-pair">
      <div class="field-control">
        <label>
          Display name
          <input :value="field.name" @input="onNameInput" maxlength="40" :disabled="busy" autocomplete="off" :aria-invalid="hasError('name')" :aria-describedby="hasError('name') ? fieldErrorId('name') : undefined" />
        </label>
        <small v-if="hasError('name')" :id="fieldErrorId('name')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError('name') }}</span></small>
      </div>
      <div class="field-control">
        <label>
          Field code
          <input v-model="field.code" @input="markFieldCodeEdited" maxlength="40" :readonly="field.persisted" :disabled="busy" autocomplete="off" spellcheck="false" :aria-invalid="hasError('code')" :aria-describedby="hasError('code') ? fieldErrorId('code') : undefined" />
        </label>
        <small v-if="hasError('code')" :id="fieldErrorId('code')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError('code') }}</span></small>
      </div>
    </div>
    <div class="field-pair">
      <div class="field-control">
        <label>
          Type
          <select v-model="field.dataType" :disabled="busy" :aria-invalid="hasError('dataType')" @change="onFieldTypeChange" :aria-describedby="hasError('dataType') ? fieldErrorId('dataType') : undefined">
            <option value="text">Text</option><option value="integer">Number</option><option value="date">Date</option>
          </select>
        </label>
        <small v-if="hasError('dataType')" :id="fieldErrorId('dataType')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError('dataType') }}</span></small>
      </div>
      <div class="field-control">
        <label class="field-emoji">
          Emoji
          <EmojiPickerDropdown v-model="field.emoji" :disabled="busy" :aria-invalid="hasError('emoji')" :teleport="false" placeholder="Select emoji" :aria-describedby="hasError('emoji') ? fieldErrorId('emoji') : undefined" />
        </label>
        <small v-if="hasError('emoji')" :id="fieldErrorId('emoji')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError('emoji') }}</span></small>
      </div>
    </div>
    <div class="field-flags">
      <label class="field-checkbox"><input v-model="field.required" type="checkbox" :disabled="busy" /> Required</label>
      <label class="field-checkbox"><input v-model="field.showOnCard" type="checkbox" :disabled="busy" /> Show on cards</label>
    </div>
    <div class="field-control">
      <label>
        Default value{{ field.required ? ' (required)' : '' }}
        <input v-if="field.dataType === 'date'" :value="field.defaultValue" type="date" :disabled="busy" :aria-invalid="hasError('defaultValue')" :aria-describedby="hasError('defaultValue') ? fieldErrorId('defaultValue') : undefined" @input="onDateDefaultInput" />
        <input v-else v-model="field.defaultValue" type="text" :inputmode="field.dataType === 'integer' ? 'numeric' : 'text'" :maxlength="field.dataType === 'text' ? 250 : undefined" :disabled="busy" autocomplete="off" :aria-invalid="hasError('defaultValue')" :aria-describedby="hasError('defaultValue') ? fieldErrorId('defaultValue') : undefined" />
      </label>
      <small v-if="hasError('defaultValue')" :id="fieldErrorId('defaultValue')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError('defaultValue') }}</span></small>
    </div>
    <div v-if="field.dataType !== 'date'" class="field-pair">
      <div class="field-control">
        <label>
          {{ field.dataType === 'text' ? 'Minimum length' : 'Minimum value' }}
          <input v-model="field.minimum" type="text" inputmode="numeric" :disabled="busy" placeholder="No minimum" :aria-invalid="hasError(field.dataType === 'text' ? 'minLength' : 'min')" :aria-describedby="hasError(field.dataType === 'text' ? 'minLength' : 'min') ? fieldErrorId(field.dataType === 'text' ? 'minLength' : 'min') : undefined" />
        </label>
        <small v-if="hasError(field.dataType === 'text' ? 'minLength' : 'min')" :id="fieldErrorId(field.dataType === 'text' ? 'minLength' : 'min')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(field.dataType === 'text' ? 'minLength' : 'min') }}</span></small>
      </div>
      <div class="field-control">
        <label>
          {{ field.dataType === 'text' ? 'Maximum length' : 'Maximum value' }}
          <input v-model="field.maximum" type="text" inputmode="numeric" :disabled="busy" :placeholder="field.dataType === 'text' ? '250' : 'No maximum'" :aria-invalid="hasError(field.dataType === 'text' ? 'maxLength' : 'max')" :aria-describedby="hasError(field.dataType === 'text' ? 'maxLength' : 'max') ? fieldErrorId(field.dataType === 'text' ? 'maxLength' : 'max') : undefined" />
        </label>
        <small v-if="hasError(field.dataType === 'text' ? 'maxLength' : 'max')" :id="fieldErrorId(field.dataType === 'text' ? 'maxLength' : 'max')" class="error field-inline-error"><CircleAlert :size="12" aria-hidden="true" /><span>{{ fieldError(field.dataType === 'text' ? 'maxLength' : 'max') }}</span></small>
      </div>
    </div>
    <p v-if="field.dataType === 'text'" class="field-note">Single line, up to 250 characters. Blank text counts as empty.</p>
  </div>
</template>
<script setup lang="ts">
import { ref, useId } from 'vue';
import { CircleAlert } from '@lucide/vue';
import EmojiPickerDropdown from '../../shared/components/EmojiPickerDropdown.vue';
import { resetFieldTypeOptions, updateFieldName, type CustomFieldDraft } from '../utils/customFieldDraft';
const field = defineModel<CustomFieldDraft>({ required: true });
const props = defineProps<{ busy: boolean; errors: Record<string, string[]> }>();
const root = ref<HTMLElement | null>(null);
const editorId = useId();
function hasError(property: string) { return Boolean(props.errors[`definition.${property}`]); }
function fieldError(property: string) { return props.errors[`definition.${property}`]?.join(' ') ?? ''; }
function fieldErrorId(property: string) { return `field-${editorId}-${property}`; }
function onNameInput(event: Event) { updateFieldName(field.value, (event.target as HTMLInputElement).value); }
function markFieldCodeEdited() { field.value.codeEdited = true; }
function onFieldTypeChange() { resetFieldTypeOptions(field.value); }
function onDateDefaultInput(event: Event) { field.value.defaultValue = (event.target as HTMLInputElement).value; }
function focusError() {
  const invalid = root.value?.querySelector<HTMLElement>('[aria-invalid="true"]');
  (invalid?.querySelector<HTMLElement>('input, button, select') ?? invalid)?.focus();
  return Boolean(invalid);
}
defineExpose({ focusError });
</script>
<style scoped>
.field-definition-editor { display: grid; gap: 0.85rem; }
.field-pair { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 0.85rem; }
.field-control, .field-control label { display: grid; gap: 0.25rem; min-width: 0; }
.field-control input, .field-control select { width: 100%; min-width: 0; }
.field-flags { display: flex; flex-wrap: wrap; gap: 1rem; }
.field-checkbox { display: flex; align-items: center; gap: 0.4rem; }
.field-inline-error { display: flex; align-items: flex-start; gap: 0.25rem; color: var(--bo-colour-danger-ink); font-size: 0.8rem; }
.field-inline-error svg { flex-shrink: 0; }
.field-note { margin: 0; font-size: 0.85rem; color: var(--bo-ink-subtle); }
input[readonly] { color: var(--bo-ink-subtle); background: var(--bo-surface-panel); }
input[aria-invalid="true"], select[aria-invalid="true"] { border-color: var(--bo-colour-danger); }
@media (max-width: 420px) { .field-pair { grid-template-columns: minmax(0, 1fr); } }
</style>
