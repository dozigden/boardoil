import { expect, test, authenticatePage } from '../fixtures/boardOilTest';
import { ScheduledJobsPage } from '../ui/ScheduledJobsPage';
import { SystemTimeZoneSetting } from '../ui/SystemTimeZoneSetting';

test('admin runs maintenance, inspects completion and changes the schedule timezone', async ({ api, authenticatedPage: page }, testInfo) => {
  const originalZone = await api.getSystemTimeZone();
  const schedules = new ScheduledJobsPage(page);
  let liveConnections = 0;
  const settingsWrites: string[] = [];
  page.on('request', request => {
    const path = new URL(request.url()).pathname;
    if (request.method() === 'PUT' && ['/api/system/configuration', '/api/system/timezone'].includes(path)) settingsWrites.push(path);
  });
  page.on('request', request => { if (request.url().includes('/hubs/system-jobs')) liveConnections++; });
  try {
    await api.setSystemTimeZone('UTC');
    await schedules.goto();
    await expect(schedules.region().getByRole('row')).toHaveCount(6);
    await expect(schedules.region()).toContainText('OAuth client registration cleanup');
    await expect(schedules.region()).toContainText('OAuth token audit purge');
    await expect(schedules.region()).toContainText('Error log purge');
    await expect(schedules.region()).toContainText('Job history purge');
    await expect(schedules.row('Resave all boards').getByText('Once', { exact: true })).toBeVisible();
    await expect(schedules.row('Resave all boards').getByText('Not scheduled', { exact: true })).toBeVisible();
    await expect(schedules.region().getByText('Daily at 03:00', { exact: true })).toHaveCount(4);
    await expect(schedules.region()).toContainText('Times use the system timezone (UTC)');
    const nextDue = await schedules.nextDue('Job history purge').getAttribute('datetime');
    await page.screenshot({ path: testInfo.outputPath('scheduled-jobs.png') });

    const queued = await schedules.runNow('Job history purge');
    expect(queued?.enqueuedCount).toBe(1);
    await expect(page.getByRole('status')).toContainText('Job queued.');
    await expect(schedules.nextDue('Job history purge')).toHaveAttribute('datetime', nextDue!);
    expect(liveConnections).toBe(0);
    await page.getByRole('tab', { name: 'Job History', exact: true }).click();
    await page.getByRole('row').filter({ has: page.getByText(`#${queued!.jobIds[0]}`, { exact: true }) }).click();
    await expect(page).toHaveURL(/\/admin\/system\/jobs\/\d+$/);
    await expect(page.getByRole('dialog').getByText('Job completed.', { exact: true })).toBeVisible({ timeout: 30_000 });

    await page.getByRole('button', { name: 'Close', exact: true }).click();
    await page.getByRole('tab', { name: 'Scheduled Jobs', exact: true }).click();
    const nextBefore = await schedules.nextDue('Job history purge').getAttribute('datetime');
    await page.getByRole('link', { name: 'Configuration', exact: true }).click();
    await new SystemTimeZoneSetting(page).saveTimeZone('Pacific/Honolulu');
    await expect(page.getByRole('status')).toContainText('Saved successfully.');
    expect(settingsWrites).toEqual(['/api/system/configuration']);
    await expect(page.getByRole('combobox', { name: 'System timezone', exact: true })).toHaveValue(/Pacific\/Honolulu/);
    await page.getByRole('link', { name: 'Jobs', exact: true }).click();
    await page.getByRole('tab', { name: 'Scheduled Jobs', exact: true }).click();
    await expect(schedules.region()).toContainText('Times use the system timezone (Pacific/Honolulu)');
    await expect(schedules.nextDue('Job history purge')).not.toHaveAttribute('datetime', nextBefore!);
    await page.screenshot({ path: testInfo.outputPath('scheduled-jobs-timezone.png') });
    await page.reload();
    await expect(schedules.region()).toContainText('Times use the system timezone (Pacific/Honolulu)');
  } finally {
    await api.setSystemTimeZone(originalZone.systemTimeZoneId);
  }
});

test('Once schedules show immediate requests and completed requests without a daily time', async ({ authenticatedPage: page }) => {
  const schedules = new ScheduledJobsPage(page);
  let requested = true;
  await page.route('**/api/system/scheduled-jobs', async route => {
    const response = await route.fetch();
    const body = await response.json();
    const once = body.data.find((schedule: { kind: string }) => schedule.kind === 'once');
    once.nextOccurrenceUtc = requested ? new Date().toISOString() : null;
    await route.fulfill({ response, json: body });
  });

  await schedules.goto();
  await expect(schedules.row('Resave all boards').getByText('Once', { exact: true })).toBeVisible();
  await expect(schedules.row('Resave all boards').getByText('Now', { exact: true })).toBeVisible();
  requested = false;
  await page.reload();
  await expect(schedules.row('Resave all boards').getByText('Not scheduled', { exact: true })).toBeVisible();
  await expect(schedules.row('Resave all boards').getByRole('button', { name: 'Run now', exact: true })).toBeEnabled();
});

test('schedule actions distinguish empty and multiple results and show failures', async ({ authenticatedPage: page }) => {
  const schedules = new ScheduledJobsPage(page);
  let count = 0;
  let fail = false;
  await page.route('**/api/system/scheduled-jobs/history-purge/runs', route => {
    if (fail) return route.fulfill({ status: 500, json: { success: false, message: 'Could not queue this schedule.' } });
    return route.fulfill({ json: { success: true, data: { enqueuedCount: count, jobIds: count ? [101, 102] : [] } } });
  });
  await schedules.goto();
  await schedules.runNow('Job history purge');
  await expect(page.getByRole('status')).toContainText('No jobs to queue.');
  count = 2;
  await schedules.runNow('Job history purge');
  await expect(page.getByRole('status')).toContainText('2 jobs queued.');
  fail = true;
  await schedules.runNow('Job history purge');
  await expect(page.getByRole('alert')).toContainText('Could not queue this schedule.');
  await expect(schedules.region().getByRole('status')).toHaveCount(0);
});

test('a standard user cannot open schedule administration', async ({ api, page }) => {
  const name = `schedule-user-${Date.now()}`;
  await api.createUser(name, 'Password1234!');
  await authenticatePage(page, name, 'Password1234!');
  await page.goto('/admin/system/jobs/scheduled');
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('region', { name: 'Scheduled jobs' })).toHaveCount(0);
});
