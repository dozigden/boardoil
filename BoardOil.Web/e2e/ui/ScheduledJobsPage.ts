import type { Page } from '@playwright/test';
import type { RunScheduledJobResult } from '../../src/shared/types/scheduledJobTypes';

export class ScheduledJobsPage {
  public constructor(private readonly page: Page) {}
  public async goto() { await this.page.goto('/admin/system/jobs/scheduled'); }
  public region() { return this.page.getByRole('region', { name: 'Scheduled jobs' }); }
  public row(name: string) { return this.region().getByRole('row').filter({ hasText: name }); }
  public async runNow(name: string) {
    const pending = this.page.waitForResponse(response => response.request().method() === 'POST' &&
      /\/api\/system\/scheduled-jobs\/[^/]+\/runs$/.test(new URL(response.url()).pathname));
    await this.row(name).getByRole('button', { name: 'Run now', exact: true }).click();
    const response = await pending;
    const body = await response.json() as { data?: RunScheduledJobResult };
    return body.data;
  }
  public nextDue(name: string) { return this.row(name).getByRole('cell').nth(3).locator('time'); }
}
