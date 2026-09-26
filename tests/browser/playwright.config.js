import { defineConfig } from '@playwright/test';
import path from 'node:path';

const publishDirectory = process.env.INVOICEFLOW_PUBLISH_DIR
  ? path.resolve(process.env.INVOICEFLOW_PUBLISH_DIR)
  : path.resolve(import.meta.dirname, '../../.tmp-publish');
const port = 5099;
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: './specs',
  workers: 1,
  use: {
    baseURL,
    browserName: 'chromium',
    ...(process.env.PLAYWRIGHT_BROWSER_CHANNEL
      ? { channel: process.env.PLAYWRIGHT_BROWSER_CHANNEL }
      : {}),
  },
  webServer: {
    command: `dotnet InvoiceFlow.Web.dll --urls ${baseURL}`,
    cwd: publishDirectory,
    url: `${baseURL}/invoices`,
    env: { ASPNETCORE_ENVIRONMENT: 'Development' },
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
