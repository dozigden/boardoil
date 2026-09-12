import { expect, type Page } from '@playwright/test';

export class AttachmentPanel {
  public constructor(private readonly page: Page) {}

  public region() { return this.page.getByRole('region', { name: 'Attachments', exact: true }); }
  public downloadLink(name: string) { return this.region().getByRole('link', { name: `Download ${name}`, exact: true }); }
  public thumbnail(name: string) { return this.region().getByRole('img', { name: `Preview of ${name}`, exact: true }); }
  public defaultIcon(name: string) {
    return this.region().locator('.attachment-row').filter({ hasText: name }).locator('.attachment-thumbnail-placeholder');
  }

  public async cancelFileSelection() {
    await expect(this.region().getByRole('button', { name: 'Upload', exact: true })).toBeEnabled();
    // Native file choosers are outside Playwright's DOM; reproduce their bubbling cancel event.
    await this.region().getByLabel('Choose attachments').dispatchEvent('cancel', { bubbles: true, cancelable: false });
  }

  public async upload(name: string, buffer: Buffer, mimeType = 'application/octet-stream') {
    await expect(this.region().getByRole('button', { name: 'Upload', exact: true })).toBeEnabled();
    await this.region().getByLabel('Choose attachments').setInputFiles({ name, mimeType, buffer });
    await expect(this.downloadLink(name)).toBeVisible();
    await expect(this.region().getByRole('button', { name: 'Upload', exact: true })).toBeEnabled();
  }

  public async download(name: string) {
    const pending = this.page.waitForEvent('download');
    await this.downloadLink(name).click();
    const download = await pending;
    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream!) { chunks.push(Buffer.from(chunk)); }
    return { name: download.suggestedFilename(), bytes: Buffer.concat(chunks) };
  }

  public async delete(name: string) {
    await this.region().getByRole('button', { name: `Delete ${name}`, exact: true }).click();
    await this.page.getByRole('dialog', { name: 'Delete attachment', exact: true }).getByRole('button', { name: 'Delete', exact: true }).click();
    await expect(this.downloadLink(name)).toBeHidden();
  }
}
