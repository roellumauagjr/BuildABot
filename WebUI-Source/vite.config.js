import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],

  // Tell Vite to treat GLB/GLTF as binary assets (not JS modules).
  // Combined with assetsInlineLimit below, files ≤ 600 KB become base64
  // data URIs inside the JS bundle — no file:// fetch needed on Android.
  assetsInclude: ['**/*.glb', '**/*.gltf'],

  build: {
    // Output directly into Unity’s StreamingAssets/WebUI folder
    outDir: path.resolve(__dirname, '../Assets/StreamingAssets/WebUI'),
    emptyOutDir: true,
    // Inline assets below 600 KB as base64 data URIs.
    // Robot1.glb ≈ 385 KB and Robot2.glb ≈ 511 KB — both will be inlined.
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
