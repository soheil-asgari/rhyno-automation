import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    // Output directly to wwwroot/js/workflow
    outDir: resolve(__dirname, '../wwwroot/js/workflow'),
    emptyOutDir: true,
    rollupOptions: {
      input: resolve(__dirname, 'src/main.jsx'),
      output: {
        // Enforce single JS file and single CSS file naming
        entryFileNames: 'workflow-builder.js',
        assetFileNames: (assetInfo) => {
          if (assetInfo.name && assetInfo.name.endsWith('.css')) {
            return 'workflow-builder.css';
          }
          return '[name].[ext]';
        },
        chunkFileNames: '[name].js'
      }
    }
  }
})
