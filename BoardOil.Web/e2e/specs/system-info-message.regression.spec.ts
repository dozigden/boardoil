import { expect, test } from '../fixtures/boardOilTest';

test('saving and disabling a system message updates the header without opening a board', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system/system-info-message');
  await expect(page.getByRole('heading', { name: 'System Info Message', exact: true })).toBeVisible();
  const enabled = page.getByRole('checkbox', { name: 'Enabled', exact: true });
  const save = page.getByRole('button', { name: 'Save', exact: true });
  await expect(save).toBeDisabled();
  await enabled.check();
  await page.getByRole('textbox', { name: 'Title', exact: true }).fill('Scheduled maintenance');
  await page.getByRole('textbox', { name: 'System info description', exact: true }).fill('Service resumes shortly.');
  await save.click();
  const headerMessage = page.getByRole('button', { name: 'Scheduled maintenance', exact: true });
  await expect(headerMessage).toBeVisible();
  await expect(save).toBeDisabled();

  await enabled.uncheck();
  await save.click();
  await expect(headerMessage).toHaveCount(0);
  await expect(save).toBeDisabled();
});
