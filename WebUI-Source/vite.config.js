import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  build: {
    // Output directly into Unity's StreamingAssets/WebUI folder
    outDir: path.resolve(__dirname, '../Assets/StreamingAssets/WebUI'),
    emptyOutDir: true,
    // Inline assets below 600KB — forces GLB models into the JS bundle as base64,
    // bypassing Android WebView's file:// CORS block on binary assets.
    // Robot1.glb=385KB, Robot2.glb=511KB — both will be inlined.
    assetsInlineLimit: 600 * 1024,
    rollupOptions: {
      output: {
        // Flat asset paths — required for file:///android_asset/ protocol
        assetFileNames: 'assets/[name]-[hash][extname]',
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js',
      },
    },
  },
  base: './',  // Relative paths required for file:// protocol on Android/iOS
})
