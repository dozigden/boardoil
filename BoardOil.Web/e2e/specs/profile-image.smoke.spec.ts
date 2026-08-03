import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { expect, test } from '../fixtures/boardOilTest';

const sourceImagePath = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
  'public',
  'favicons',
  'favicon-32x32.png'
);

test('crop and upload a profile image that remains after reload', async ({ authenticatedPage: page }) => {
  await page.goto('/user-admin/profile');

  await page.getByRole('button', { name: 'Profile image options' }).click();
  const fileChooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Upload image' }).click();
  const fileChooser = await fileChooserPromise;
  await fileChooser.setFiles(sourceImagePath);

  const cropHeading = page.getByRole('heading', { name: 'Crop Profile Image' });
  const cropDialog = page.getByRole('dialog').filter({ has: cropHeading });
  await expect(cropHeading).toBeVisible();
  await cropDialog.getByRole('slider').fill('1.25');
  const uploadResponsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST'
    && new URL(response.url()).pathname === '/api/users/me/profile-image'
  );
  await cropDialog.getByRole('button', { name: 'Upload image' }).click();
  const uploadResponse = await uploadResponsePromise;
  expect(uploadResponse.ok(), await uploadResponse.text()).toBe(true);
  await expect(cropDialog).toBeHidden();

  await page.reload();
  await page.getByRole('button', { name: 'Profile image options' }).click();
  await expect(page.getByRole('button', { name: 'Remove image' })).toBeEnabled();
});
