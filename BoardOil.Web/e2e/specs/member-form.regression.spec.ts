import { expect, test } from '../fixtures/boardOilTest';

for (const mode of ['owner', 'system administrator'] as const) {
  test(`${mode} can correct an add-member rejection, retry and cancel a fresh draft`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Add member ${mode}`);
    const member = await api.createUser(`member-${board.id}`, 'MemberPassword1234!');
    const currentUser = await api.getOwnProfile();
    const path = mode === 'owner' ? `/boards/${board.id}/admin/members` : `/admin/system/boards/${board.id}/members`;
    await page.goto(path);
    await page.getByRole('button', { name: 'Add member', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Add Member', exact: true });
    const user = dialog.getByRole('combobox', { name: 'User', exact: true });
    const role = dialog.getByRole('combobox', { name: 'Role', exact: true });
    await user.selectOption(String(currentUser.id));
    await role.selectOption('Owner');
    await dialog.getByRole('button', { name: 'Add member', exact: true }).click();

    await expect(user).toHaveAccessibleDescription('User is already a board member.');
    await expect(user).toBeFocused();
    await expect(user).toHaveValue(String(currentUser.id));
    await expect(role).toHaveValue('Owner');
    await expect(page.getByText('User is already a board member.', { exact: true })).toHaveCount(1);
    await page.screenshot({ path: test.info().outputPath(`member-${mode}-validation.png`) });

    await user.selectOption(String(member.id));
    await expect(user).not.toHaveAttribute('aria-invalid', 'true');
    await dialog.getByRole('button', { name: 'Add member', exact: true }).click();
    await expect(dialog).toBeHidden();
    await expect(page.getByRole('button', { name: `Edit member ${member.userName}`, exact: true })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: `Edit member ${member.userName}`, exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Add member', exact: true }).click();
    await role.selectOption('Owner');
    await dialog.getByRole('button', { name: 'Cancel add member', exact: true }).and(page.getByTitle('Cancel', { exact: true })).click();
    await page.getByRole('button', { name: 'Add member', exact: true }).click();
    await expect(role).toHaveValue('Contributor');
    await expect(user).not.toHaveAttribute('aria-invalid', 'true');
  });
}
