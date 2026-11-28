import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const dockerfile = readFileSync(path.join(frontendRoot, 'Dockerfile'), 'utf8')
const nginxDev = readFileSync(path.join(frontendRoot, 'nginx.conf'), 'utf8')
const nginxProd = readFileSync(path.join(frontendRoot, 'nginx.prod.conf'), 'utf8')

function extractBlock(source, header) {
  const startIndex = source.indexOf(header)
  assert.notEqual(startIndex, -1, `Missing block: ${header}`)
  const openBraceIndex = source.indexOf('{', startIndex)
  assert.notEqual(openBraceIndex, -1, `Missing opening brace for: ${header}`)

  let depth = 0
  for (let i = openBraceIndex; i < source.length; i += 1) {
    if (source[i] === '{') depth += 1
    else if (source[i] === '}') {
      depth -= 1
      if (depth === 0) return source.slice(openBraceIndex + 1, i)
    }
  }
  assert.fail(`Unbalanced braces in block: ${header}`)
}

function assertBalancedBraces(source, label) {
  let depth = 0
  for (const ch of source) {
    if (ch === '{') depth += 1
    if (ch === '}') depth -= 1
    assert.ok(depth >= 0, `Unexpected closing brace in ${label}`)
  }
  assert.equal(depth, 0, `Unclosed brace in ${label}`)
}

const commonSecurityHeaders = [
  'add_header X-Frame-Options "SAMEORIGIN" always;',
  'add_header X-Content-Type-Options "nosniff" always;',
  'add_header Referrer-Policy "strict-origin-when-cross-origin" always;',
  'add_header Permissions-Policy "microphone=(self), camera=(), geolocation=()" always;',
  'add_header Cross-Origin-Opener-Policy "same-origin-allow-popups" always;',
]
const hstsHeader =
  'add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;'
const apiErrorLocations = [
  'location @api_timeout',
  'location @api_too_large',
  'location @api_bad_gateway',
  'location @api_gateway_timeout',
]

test('nginx configs have balanced braces and non-root Dockerfile runtime', () => {
  assertBalancedBraces(nginxDev, 'nginx.conf')
  assertBalancedBraces(nginxProd, 'nginx.prod.conf')
  assert.match(dockerfile, /^USER nginx$/m)
  assert.match(dockerfile, /\/(?:tmp|run\/nginx)\/nginx\.pid/)
  assert.match(dockerfile, /EXPOSE 8080 8443/)
  assert.match(dockerfile, /http:\/\/127\.0\.0\.1:8080\/healthz/)
})

test('nginx.prod.conf exposes ACME challenge on HTTP server before HTTPS 301 redirect', () => {
  const httpServerStart = nginxProd.indexOf('listen 8080;')
  const httpsServerStart = nginxProd.indexOf('listen 8443 ssl;')
  assert.ok(httpServerStart !== -1 && httpsServerStart > httpServerStart)

  const httpSection = nginxProd.slice(httpServerStart, httpsServerStart)
  const acmeIndex = httpSection.indexOf('location ^~ /.well-known/acme-challenge/')
  const redirectIndex = httpSection.indexOf('return 301 https://oralexam.io.vn$request_uri;')
  assert.ok(acmeIndex !== -1, 'Missing /.well-known/acme-challenge/ location in HTTP block')
  assert.ok(acmeIndex < redirectIndex, 'ACME challenge location must appear before return 301')

  const acmeBlock = extractBlock(httpSection, 'location ^~ /.well-known/acme-challenge/')
  assert.match(acmeBlock, /root\s+\/var\/www\/certbot;/)
  assert.match(acmeBlock, /try_files\s+\$uri\s+=404;/)
})

test('all @api_* error blocks retain full security headers alongside no-store', () => {
  for (const locationHeader of apiErrorLocations) {
    const prodBlock = extractBlock(nginxProd, locationHeader)
    assert.match(prodBlock, /add_header Cache-Control "no-store" always;/)
    assert.ok(prodBlock.includes(hstsHeader), `Missing HSTS in prod ${locationHeader}`)
    for (const header of commonSecurityHeaders) {
      assert.ok(prodBlock.includes(header), `Missing ${header} in prod ${locationHeader}`)
    }

    const devBlock = extractBlock(nginxDev, locationHeader)
    assert.match(devBlock, /add_header Cache-Control "no-store" always;/)
    for (const header of commonSecurityHeaders) {
      assert.ok(devBlock.includes(header), `Missing ${header} in dev ${locationHeader}`)
    }
  }
})

test('location /hubs/ disables proxy buffering and avoids logging ?access_token=', () => {
  for (const [label, config] of [
    ['nginx.prod.conf', nginxProd],
    ['nginx.conf', nginxDev],
  ]) {
    const hubsBlock = extractBlock(config, 'location /hubs/')
    assert.ok(
      /access_log off;/.test(hubsBlock) ||
        /access_log \/var\/log\/nginx\/access\.log hub_sanitized;/.test(hubsBlock),
      `Missing access_log suppression/redaction in ${label}`,
    )
    assert.match(hubsBlock, /error_log \/var\/log\/nginx\/error\.log crit;/)
    assert.match(hubsBlock, /proxy_buffering off;/)
    assert.match(hubsBlock, /proxy_pass http:\/\/backend:5000\/hubs\/;/)
  }
})

test('location ^~ /uploads/ removes unused proxy_cache_valid directive', () => {
  for (const [label, config] of [
    ['nginx.prod.conf', nginxProd],
    ['nginx.conf', nginxDev],
  ]) {
    const uploadsBlock = extractBlock(config, 'location ^~ /uploads/')
    assert.doesNotMatch(
      uploadsBlock,
      /proxy_cache_valid/,
      `Unexpected proxy_cache_valid in ${label}`,
    )
    assert.match(uploadsBlock, /proxy_pass http:\/\/backend:5000\/uploads\/;/)
  }
})
