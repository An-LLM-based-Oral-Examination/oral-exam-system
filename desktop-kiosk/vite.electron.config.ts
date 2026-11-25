import { builtinModules } from 'node:module'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'

const projectRoot = path.dirname(fileURLToPath(import.meta.url))
const nodeBuiltins = new Set([
  ...builtinModules,
  ...builtinModules.map((moduleName) => `node:${moduleName}`),
])

export default defineConfig({
  base: './',
  build: {
    outDir: 'dist-electron',
    emptyOutDir: true,
    target: 'node22',
    sourcemap: true,
    minify: false,
    lib: {
      entry: {
        main: path.resolve(projectRoot, 'electron/main.ts'),
        preload: path.resolve(projectRoot, 'electron/preload.ts'),
      },
      formats: ['cjs'],
      fileName: (_format, entryName) => `${entryName}.cjs`,
    },
    rollupOptions: {
      external: (id) =>
        id === 'electron' ||
        id.startsWith('electron/') ||
        nodeBuiltins.has(id),
      output: {
        exports: 'named',
      },
    },
  },
})
