<template>
  <section class="schedules-page" aria-label="Scheduled jobs">
    <p v-if="store.error" class="error" role="alert">{{ store.error }}</p>
    <BoGrid :columns="columns" :items="rows" :is-loading="store.loading && !store.schedules.length" empty-text="No schedules found."
      :total-count="rows.length" :offset="0" :limit="Math.max(1, rows.length)" :show-pagination-controls="false">
      <template #cell(name)="{ row }">
        <strong>{{ row.displayName }}</strong>
      </template>
      <template #cell(dailyTime)="{ row }">
        <span>{{ describeSchedule(String(row.dailyTime)) }}</span>
      </template>
      <template #cell(lastEvaluatedAtUtc)="{ row }">
        <time v-if="row.lastEvaluatedAtUtc" :datetime="String(row.lastEvaluatedAtUtc)" :title="utcDate(String(row.lastEvaluatedAtUtc))">
          {{ formatScheduleDate(String(row.lastEvaluatedAtUtc), String(row.timeZoneId)) }}
        </time>
        <span v-else>Not yet evaluated</span>
      </template>
      <template #cell(nextOccurrenceUtc)="{ row }">
        <time v-if="row.nextOccurrenceUtc" :datetime="String(row.nextOccurrenceUtc)" :title="utcDate(String(row.nextOccurrenceUtc))">
          {{ formatScheduleDate(String(row.nextOccurrenceUtc), String(row.timeZoneId)) }}
        </time>
        <span v-else>Not scheduled</span>
      </template>
      <template #cell(latestStartedJob)="{ row }">
        <RouterLink v-if="schedule(row).latestStartedJob" class="latest-job-link" :to="jobLink(schedule(row).latestStartedJob!.id)">
          #{{ schedule(row).latestStartedJob!.id }} · {{ schedule(row).latestStartedJob!.status }}
        </RouterLink>
        <span v-else>Not yet started</span>
      </template>
      <template #cell(actions)="{ row }">
        <button type="button" class="btn btn--secondary" :disabled="store.running[String(row.name)]" @click="store.runNow(String(row.name))">
          {{ store.running[String(row.name)] ? 'Queueing…' : 'Run now' }}
        </button>
      </template>
    </BoGrid>
    <p class="schedule-note">Times use the system timezone<span v-if="timeZoneId"> ({{ timeZoneId }})</span>; hover for UTC. Run now queues work without changing the next scheduled run.</p>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from 'vue';
import { RouterLink } from 'vue-router';
import BoGrid from '../../shared/components/BoGrid.vue';
import type { ScheduledJob } from '../../shared/types/scheduledJobTypes';
import { useAuthStore } from '../../shared/stores/authStore';
import { useScheduledJobsStore } from '../stores/scheduledJobsStore';
import { describeSchedule, formatScheduleDate } from '../utils/scheduledJobPresentation';

const store = useScheduledJobsStore();
const auth = useAuthStore();
const timeZoneId = computed(() => store.schedules[0]?.timeZoneId);
const rows = computed(() => store.schedules as unknown as Record<string, unknown>[]);
const columns = [
  { key: 'name', label: 'Schedule', rowKeyColumn: true, width: 'minmax(12rem, 1.4fr)' },
  { key: 'dailyTime', label: 'Timing', width: 'minmax(10rem, 1fr)' },
  { key: 'lastEvaluatedAtUtc', label: 'Last evaluated', width: 'minmax(9rem, 1fr)' },
  { key: 'nextOccurrenceUtc', label: 'Next due', width: 'minmax(9rem, 1fr)' },
  { key: 'latestStartedJob', label: 'Latest job', width: 'max-content' },
  { key: 'actions', label: 'Run', width: '8rem' }
];
function schedule(row: Record<string, unknown>) { return row as ScheduledJob; }
function jobLink(id: number) { return { name: 'system-job-details', params: { jobId: id } }; }
function utcDate(value: string) { return new Date(value).toUTCString(); }
watch(() => auth.isAdmin, admin => {
  if (admin) void store.loadSchedules();
  else store.dispose();
}, { immediate: true });
onBeforeUnmount(store.dispose);
</script>

<style scoped>
.schedules-page { display: flex; flex-direction: column; gap: 1rem; min-width: 0; }
.schedule-note { margin: 0; font-size: 0.85rem; }
.schedule-note { color: var(--bo-ink-muted); }
.latest-job-link { white-space: nowrap; }
</style>
