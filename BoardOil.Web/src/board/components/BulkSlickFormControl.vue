<template>
  <CardSlickPicker
    v-model:slick-name="model"
    :slicks="slicks.slicks"
    :disabled="disabled"
    :show-label="false"
    :allow-clear="false"
  />
</template>

<script setup lang="ts">
import CardSlickPicker from './CardSlickPicker.vue';
import { useSlickStore } from '../stores/slickStore';

defineProps<{ disabled: boolean }>();
const model = defineModel<string | null>({ required: true });
const slicks = useSlickStore();
</script>
