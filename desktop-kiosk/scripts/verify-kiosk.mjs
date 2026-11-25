import { spawnSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm'

function runStep(label, command, args, options = {}) {
  console.log(`\n==> ${label}`)
  const result = spawnSync(command, args, {
    cwd: rootDir,
    stdio: 'inherit',
    shell: process.platform === 'win32',
    ...options,
  })
  if (result.error) {
    throw result.error
  }
  if (result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}

runStep('TypeScript typecheck (app + node)', npmCommand, ['run', 'typecheck'])
runStep('Build Electron shell & Renderer', npmCommand, ['run', 'build'])
runStep('Contract & unit tests', npmCommand, ['test'])

const agentProjectRelative = path.join('native', 'Oes.WindowsAgent', 'Oes.WindowsAgent.csproj')
if (existsSync(path.join(rootDir, agentProjectRelative))) {
  const dotnetCheck = spawnSync('dotnet', ['--version'], {
    cwd: rootDir,
    stdio: 'pipe',
    shell: false,
  })
  if (dotnetCheck.status === 0) {
    runStep(
      'Build native/Oes.WindowsAgent (.NET 8)',
      'dotnet',
      ['build', agentProjectRelative, '-c', 'Release', '--nologo'],
      { shell: false },
    )
  } else {
    console.log('\n==> Bỏ qua build native/Oes.WindowsAgent (không có dotnet SDK trong PATH).')
  }
}

console.log('\nDesktop Kiosk verification PASSED.')
