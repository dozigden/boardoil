<template>
  <div class="btn-tab-list" role="group" aria-label="Slick effect">
    <button
      v-for="option in slickEffectOptions"
      :key="option.value"
      type="button"
      class="btn btn--tab"
      :class="{ 'is-active': slickEffectStore.effect === option.value }"
      :aria-pressed="slickEffectStore.effect === option.value"
      @click="slickEffectStore.setEffect(option.value)"
    >{{ option.label }}</button>
  </div>
</template>

<script setup lang="ts">
import { useSlickEffectStore } from '../../shared/stores/slickEffectStore';
import { slickEffectOptions } from '../../shared/types/slickEffect';

const slickEffectStore = useSlickEffectStore();
</script>
