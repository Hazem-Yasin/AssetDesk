import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// The production build is emitted straight into the API's wwwroot so one ASP.NET Core
// container serves both the SPA and the REST API.
export default defineConfig({
  plugins: [react()],
  build: { outDir: '../src/AssetDesk.Api/wwwroot', emptyOutDir: true },
  server: { port: 5173, proxy: { '/api': 'http://localhost:5090' } },
  test: { environment: 'jsdom', globals: true, setupFiles: ['./src/test-setup.ts'] },
});
