import { expect, test } from '../fixtures/boardOilTest';

type JobStatus = 'running' | 'completed' | 'failed';
function job(id: number, status: JobStatus) {
  return {
    id, type: id === 42 ? 'history.purge' : 'error-log.purge', status,
    runAfterUtc: '2026-09-27T10:00:00Z', payloadJson: '{"note":"<img src=x onerror=alert(1)>"}',
    resultJson: '{"deletedCount":2}', errorMessage: status === 'failed' ? 'Controlled failure' : null,
    startedAtUtc: '2026-09-27T10:00:00Z', completedAtUtc: status === 'running' ? null : '2026-09-27T10:02:00Z',
    userId: null, correlationId: null, createdAtUtc: '2026-09-27T10:00:00Z', updatedAtUtc: '2026-09-27T10:02:00Z'
  };
}
const envelope = (data: unknown) => ({ success: true, statusCode: 200, data });

test('real maintenance jobs appear on the administrator history page', async ({ authenticatedPage: page }, testInfo) => {
  let historyJobId: number | undefined;
  await expect.poll(async () => {
    const response = await page.request.get('/api/system/jobs?offset=0&limit=100');
    if (!response.ok()) return 0;
    const body = await response.json() as { data?: { items: Array<{ id: number; type: string; status: string }> } };
    historyJobId = body.data?.items.find(item => item.type === 'history.purge' && item.status === 'completed')?.id;
    if (historyJobId === undefined) return 0;
    return body.data?.items.filter(item => item.status === 'completed').length ?? 0;
  }, { timeout: 30_000 }).toBeGreaterThanOrEqual(4);

  await page.goto('/admin/system/jobs');
  await expect(page.getByRole('table')).toContainText('History purge');
  await expect(page.getByRole('table')).toContainText('Error log purge');
  await page.screenshot({ path: testInfo.outputPath('job-history.png') });
  await page.getByRole('row').filter({ has: page.getByText(`#${historyJobId}`, { exact: true }) }).click();
  await expect(page.getByRole('dialog').getByText('Job completed.')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('job-details.png') });
});

test('system job history shows lifecycle states and safe routed details', async ({ authenticatedPage: page }, testInfo) => {
  let listFailure = false;
  await page.route('**/api/system/jobs?*', route => {
    if (listFailure) {
      return route.fulfill({ status: 500, json: { success: false, statusCode: 500, message: 'History unavailable.' } });
    }
    return route.fulfill({ json: envelope({ items: [job(44, 'running'), job(43, 'failed'), job(42, 'completed')],
      offset: 0, limit: 100, totalCount: 3 }) });
  });
  await page.route('**/api/system/jobs/42', route => route.fulfill({ json: envelope({ ...job(42, 'completed'),
    logs: [
      { id: 2, level: 'info', message: 'Job completed.', dataJson: '{}', loggedAtUtc: '2026-09-27T10:02:00Z' },
      { id: 1, level: 'info', message: 'Job started.', dataJson: null, loggedAtUtc: '2026-09-27T10:00:00Z' }
    ] }) }));
  await page.route('**/api/system/jobs/43', route => route.fulfill({ json: envelope({ ...job(43, 'failed'), logs: [] }) }));

  await page.goto('/admin/system/jobs');
  await expect(page.getByRole('table')).toContainText('running');
  await expect(page.getByRole('table')).toContainText('failed');
  await expect(page.getByRole('table')).toContainText('completed');
  await page.getByRole('row').filter({ hasText: '#42' }).click();
  await expect(page).toHaveURL(/\/admin\/system\/jobs\/42$/);
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Job started.')).toBeVisible();
  await expect(dialog.getByText('Job completed.')).toBeVisible();
  const messages = await dialog.locator('tbody tr td:nth-child(3)').allTextContents();
  expect(messages).toEqual(['Job started.', 'Job completed.']);
  await expect(dialog.getByText('<img src=x onerror=alert(1)>')).toBeVisible();
  await expect(dialog.locator('img')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Close', exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/system\/jobs$/);

  await page.getByRole('row').filter({ hasText: '#43' }).click();
  await expect(page.getByRole('dialog').getByText('Controlled failure')).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('job-failed.png') });
  await page.getByRole('button', { name: 'Close', exact: true }).click();
  listFailure = true;
  await page.getByRole('button', { name: 'Refresh' }).click();
  await expect(page.getByRole('alert').filter({ hasText: /^History unavailable\.$/ })).toBeVisible();
});

test('error-log job link opens the normal missing-job view when history was purged', async ({ authenticatedPage: page }, testInfo) => {
  const errorLog = {
    id: 5, occurredAtUtc: '2026-09-27T10:00:00Z', source: 'Backend', area: 'JobRunner',
    exceptionType: 'System.InvalidOperationException', message: 'Controlled failure',
    stackTrace: 'at Example.Run()', contextJson: '{}', traceIdentifier: null,
    requestMethod: null, requestPath: null, actorUserId: null, jobId: 42,
    createdAtUtc: '2026-09-27T10:00:00Z', updatedAtUtc: '2026-09-27T10:00:00Z'
  };
  await page.route('**/api/system/error-logs?*', route => route.fulfill({ json: envelope({
    items: [errorLog], offset: 0, limit: 100, totalCount: 1
  }) }));
  await page.route('**/api/system/error-logs/5', route => route.fulfill({ json: envelope(errorLog) }));
  await page.route('**/api/system/jobs?*', route => route.fulfill({ json: envelope({ items: [], offset: 0, limit: 100, totalCount: 0 }) }));
  await page.route('**/api/system/jobs/42', route => route.fulfill({ status: 404,
    json: { success: false, statusCode: 404, message: 'Job not found.' } }));

  await page.goto('/admin/system/error-logs/5');
  await expect(page.getByRole('dialog').getByRole('link', { name: '#42' })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('error-log-job-link.png') });
  await page.getByRole('dialog').getByRole('link', { name: '#42' }).click();
  await expect(page).toHaveURL(/\/admin\/system\/jobs\/42$/);
  await expect(page.getByText('No jobs found.')).toBeVisible();
  await expect(page.getByRole('dialog').getByText('Job not found.')).toBeVisible();
});

test('leaving job history through logout protects the route', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system/jobs');
  await expect(page.getByRole('table')).toBeVisible();
  await page.getByRole('button', { name: 'User menu' }).click();
  await page.getByRole('button', { name: 'Logout' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto('/admin/system/jobs');
  await expect(page).toHaveURL(/\/login\?redirect=/);
});
