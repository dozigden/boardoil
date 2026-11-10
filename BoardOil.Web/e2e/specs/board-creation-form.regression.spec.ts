import { expect, test } from '../fixtures/boardOilTest';
import { rejectNextBoardImport } from '../support/boardCreationResponse';
import { BoardPage } from '../ui/BoardPage';

test('blank board creation displays backend validation, retains description and retries', async ({ authenticatedPage: page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Create board', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Create Board', exact: true });
  const name = dialog.getByLabel('Board name', { exact: true });
  const description = dialog.getByLabel('Description (optional)', { exact: true });
  await description.fill('Keep this description');
  await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  await expect(dialog.getByRole('alert')).toContainText('Board name is required.');
  await expect(name).toBeFocused();
  await expect(description).toHaveValue('Keep this description');
  await expect(page.getByText('Board name is required.', { exact: true })).toHaveCount(1);
  await name.fill('Board creation validation');
  await description.fill('x'.repeat(5001));
  await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  await expect(dialog.getByRole('alert')).toContainText('Board description must be 5000 characters or fewer.');
  await expect(description).toBeFocused();
  await description.fill('Corrected description');
  await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  await expect(page).toHaveURL(/\/boards\/\d+$/);
  await expect(dialog).toBeHidden();
});

for (const viewport of [{ width: 1280, height: 800 }, { width: 390, height: 750 }]) {
  test(`mode switching retains file and text inputs; missing files and replacing imports work at ${viewport.width}px`, async ({ api, authenticatedPage: page }) => {
    const source = await api.createBoard('Package source');
    const packageFile = await api.exportBoardPackage(source.id);
    await page.setViewportSize(viewport);
    await page.goto('/');
    await page.getByRole('button', { name: 'Create board', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Create Board', exact: true });
    const file = dialog.getByLabel('Package ZIP file', { exact: true });
    const override = dialog.getByLabel('Board name override (optional)', { exact: true });
    await dialog.getByLabel('Board name', { exact: true }).fill('Remember blank name');
    await dialog.getByLabel('Description (optional)', { exact: true }).fill('Remember description');
    await dialog.getByRole('tab', { name: 'Import package' }).click();
    await dialog.getByRole('button', { name: 'Import board', exact: true }).click();
    await expect(dialog.getByRole('alert')).toContainText('Board package ZIP file is required.');
    await expect(file).toBeFocused();
    await file.setInputFiles(packageFile);
    await override.fill('Remember import name');

    await test.step('switch modes and restore the actual file input', async () => {
      await dialog.getByRole('tab', { name: 'Clone board' }).click();
      await dialog.getByLabel('Source board', { exact: true }).selectOption(String(source.id));
      await expect(dialog.getByLabel('New board name', { exact: true })).toHaveValue('Remember blank name');
      await dialog.getByRole('tab', { name: 'Create Blank' }).click();
      await expect(dialog.getByLabel('Description (optional)', { exact: true })).toHaveValue('Remember description');
      await dialog.getByRole('tab', { name: 'Import package' }).click();
      await expect(override).toHaveValue('Remember import name');
      await expect(file).toHaveValue(/board\.boardoil\.zip$/);
      await expect.poll(() => file.evaluate(element => (element as HTMLInputElement).files?.[0]?.size)).toBe(packageFile.buffer.length);
    });

    await test.step('reject malformed packages on the backend', async () => {
      await file.setInputFiles({ name: 'broken.zip', mimeType: 'application/zip', buffer: Buffer.from('not a ZIP') });
      await dialog.getByRole('button', { name: 'Import board', exact: true }).click();
      await expect(file).toHaveAttribute('aria-invalid', 'true');
      await expect(file).toHaveValue(/broken\.zip$/);
      await expect(override).toHaveValue('Remember import name');
      await page.screenshot({ path: `/tmp/forms-engine-board-import-${viewport.width}.png` });
    });

    await test.step('replace the file, correct the override and import', async () => {
      await file.setInputFiles(packageFile);
      await override.fill('x'.repeat(121));
      await dialog.getByRole('button', { name: 'Import board', exact: true }).click();
      await expect(override).toHaveAttribute('aria-invalid', 'true');
      await expect(override).toBeFocused();
      await expect(file).toHaveValue(/board\.boardoil\.zip$/);
      await override.fill(`Imported board ${viewport.width}`);
      await dialog.getByRole('button', { name: 'Import board', exact: true }).click();
      await expect(page).toHaveURL(/\/boards\/\d+$/);
      await expect(dialog).toBeHidden();
    });
  });
}

test('import keeps a selected package through a failed request and blocks changes until it completes', async ({ api, authenticatedPage: page }) => {
  const source = await api.createBoard('Import retry source');
  const packageFile = await api.exportBoardPackage(source.id);
  await page.goto('/');
  await page.getByRole('button', { name: 'Create board', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Create Board', exact: true });
  await dialog.getByRole('tab', { name: 'Import package' }).click();
  const file = dialog.getByLabel('Package ZIP file', { exact: true });
  await file.setInputFiles(packageFile);
  await dialog.getByLabel('Board name override (optional)').fill('Import retry destination');
  const release = await rejectNextBoardImport(page);
  const submit = dialog.getByRole('button', { name: 'Import board', exact: true });
  await submit.click();
  await expect(submit).toBeDisabled();
  await expect(file).toBeDisabled();
  await expect(dialog.getByRole('tab', { name: 'Create Blank' })).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  release();
  await expect(dialog.getByRole('alert')).toHaveText('Import is temporarily unavailable.');
  await expect(page.getByText('Import is temporarily unavailable.', { exact: true })).toHaveCount(1);
  await expect(file).toHaveValue(/board\.boardoil\.zip$/);
  await submit.click();
  await expect(page).toHaveURL(/\/boards\/\d+$/);
});

test('clone validates its name on the backend and preserves the selected source for retry', async ({ api, authenticatedPage: page }) => {
  const source = await api.createBoard('Clone source');
  await api.createCard(source, 'Todo', 'Must not be cloned');
  await page.goto('/');
  await page.getByRole('button', { name: 'Create board', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Create Board', exact: true });
  await dialog.getByRole('tab', { name: 'Clone board' }).click();
  await dialog.getByLabel('Source board', { exact: true }).selectOption(String(source.id));
  await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  await expect(dialog.getByLabel('New board name', { exact: true })).toBeFocused();
  await expect(dialog.getByRole('alert')).toContainText('Board name is required.');
  await expect(dialog.getByLabel('Source board', { exact: true })).toHaveValue(String(source.id));
  await dialog.getByRole('tab', { name: 'Create Blank' }).click();
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await dialog.getByRole('tab', { name: 'Clone board' }).click();
  await dialog.getByLabel('New board name', { exact: true }).fill('Cloned board');
  await dialog.getByRole('button', { name: 'Create board', exact: true }).click();
  await expect(page).toHaveURL(/\/boards\/\d+$/);
  await expect(new BoardPage(page).card('Todo', 'Must not be cloned')).toHaveCount(0);
});
