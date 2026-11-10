import { toRaw } from 'vue';

export function cloneFormDraft<T extends object>(values: T): T {
  const raw = toRaw(values);
  const copy = structuredClone(raw);
  // File fields are immutable; retain their identity across snapshots and resets.
  for (const key of Object.keys(raw) as (keyof T)[]) {
    if (raw[key] instanceof File) copy[key] = raw[key];
  }
  return copy;
}

export function equalFormDrafts<T extends object>(left: T, right: T) {
  for (const key of Object.keys(left) as (keyof T)[]) {
    if ((left[key] instanceof File || right[key] instanceof File) && left[key] !== right[key]) return false;
  }
  return JSON.stringify(left) === JSON.stringify(right);
}
