import { spawnSync } from 'node:child_process'
import { mkdirSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const testDistDir = path.join(rootDir, 'node_modules', '.tmp', 'test-dist')
const tscEntry = path.join(rootDir, 'node_modules', 'typescript', 'bin', 'tsc')

rmSync(testDistDir, { recursive: true, force: true })
mkdirSync(testDistDir, { recursive: true })

// Compile core TypeScript modules to ESM JS so Node unit tests execute real production code.
const compile = spawnSync(
  process.execPath,
  [
    tscEntry,
    '--ignoreConfig',
    '--target',
    'ES2023',
    '--module',
    'ESNext',
    '--moduleResolution',
    'Bundler',
    '--outDir',
    testDistDir,
    '--rootDir',
    'src',
    '--skipLibCheck',
    '--strict',
    'src/utils/transcript-diff.ts',
    'src/services/api.ts',
    'src/types/mf04.types.ts',
  ],
  {
    cwd: rootDir,
    stdio: 'inherit',
    shell: false,
  },
)

if (compile.status !== 0) {
  process.exit(compile.status ?? 1)
}

writeFileSync(path.join(testDistDir, 'package.json'), JSON.stringify({ type: 'module' }), 'utf8')

const testRun = spawnSync(process.execPath, ['--test'], {
  cwd: rootDir,
  stdio: 'inherit',
})

process.exit(testRun.status ?? 1)
