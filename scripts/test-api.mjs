// Integration test of the compiled API (dotnet build first). Uses port 5082 and an isolated folder in
// .test-data/, so it never touches App_Data. GitHub is disabled to keep the run independent of the network.
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdirSync, mkdtempSync } from 'node:fs';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
import { request as httpRequest } from 'node:http';

const root = resolve(import.meta.dirname, '..');
mkdirSync(resolve(root, '.test-data'), { recursive: true });
const dataPath = mkdtempSync(resolve(root, '.test-data/run-'));
const base = 'http://127.0.0.1:5082';
const headers = { 'Content-Type': 'application/json', 'X-Portfolio-Editor': 'local' };
let server;

async function start(mode) {
  let output = '';
  server = spawn('dotnet', [resolve(root, 'Portfolio.Api/bin/Debug/net10.0/Portfolio.Api.dll')], {
    cwd: resolve(root, 'Portfolio.Api'),
    windowsHide: true,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: mode,
      ASPNETCORE_URLS: base,
      Portfolio__DataPath: dataPath,
      GitHub__Enabled: 'false',
    },
  });
  server.stdout.on('data', (b) => { output += b; });
  server.stderr.on('data', (b) => { output += b; });
  for (let i = 0; i < 60; i++) {
    if (server.exitCode !== null) throw new Error(output);
    try { if ((await fetch(`${base}/api/health`)).ok) return; } catch {}
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error(`No se inició el servidor: ${output}`);
}
async function stop() {
  if (server && server.exitCode === null) { const exited = once(server, 'exit'); server.kill(); await exited; }
}
const put = (data, extra = {}) =>
  fetch(`${base}/api/portfolio`, { method: 'PUT', headers: { ...headers, ...extra }, body: JSON.stringify(data) });

try {
  await start('Development');

  // Public reads, conditional requests and headers.
  const first = await fetch(`${base}/api/portfolio`);
  const original = await first.json();
  const etag = first.headers.get('etag');
  assert.equal(etag, `"${original.revision}"`);
  assert.equal(first.headers.get('cache-control'), 'no-cache');
  assert.match(first.headers.get('content-security-policy'), /default-src 'none'/);
  assert.equal((await fetch(`${base}/api/portfolio`, { headers: { 'If-None-Match': etag } })).status, 304);
  assert.ok(original.experience.length > 0 && original.education.length > 0);
  const github = await fetch(`${base}/api/github`);
  assert.equal(github.status, 200);
  assert.equal((await github.json()).available, false);
  const sitemap = await fetch(`${base}/sitemap.xml`);
  assert.equal(sitemap.status, 200);
  assert.match(sitemap.headers.get('content-type'), /^application\/xml/);
  const sitemapXml = await sitemap.text();
  assert.match(sitemapXml, /<loc>https:\/\/diegoespinarodriguez\.com\/experiencia<\/loc>/);
  assert.ok(sitemapXml.includes(`/proyectos/${original.projects[0].id}</loc>`));
  assert.equal((await fetch(`${base}/api/openapi/v1.json`)).status, 200);

  // Write protection of the local editor.
  assert.equal((await (await fetch(`${base}/api/editor`)).json()).enabled, true);
  assert.equal((await fetch(`${base}/api/portfolio`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(original) })).status, 403);
  assert.equal((await put(original, { Origin: 'https://other.example' })).status, 403);
  const hostStatus = await new Promise((resolve, reject) => {
    const req = httpRequest(base + '/api/portfolio', { method: 'PUT', headers: { ...headers, Host: 'evil.example' } }, (r) => { r.resume(); resolve(r.statusCode); });
    req.on('error', reject);
    req.end(JSON.stringify(original));
  });
  assert.equal(hostStatus, 403);

  // Validation.
  assert.equal((await put({ ...original, profile: { ...original.profile, name: '' } })).status, 400);
  assert.equal((await put({ ...original, profile: { ...original.profile, bio: null } })).status, 400);
  assert.equal((await put({ ...original, projects: [null] })).status, 400);
  assert.equal((await put({ ...original, profile: { ...original.profile, intro: 'a'.repeat(20001) } })).status, 400);
  assert.equal((await put({ ...original, profile: { ...original.profile, intro: 'a'.repeat(10 * 1024 * 1024) } })).status, 413);
  assert.equal((await put({ ...original, profile: { ...original.profile, github: 'javascript:alert(1)' } })).status, 400);
  assert.equal((await put({ ...original, profile: { ...original.profile, email: 'invalid' } })).status, 400);
  assert.equal((await put({ ...original, entries: [{ ...original.entries[0], date: 'not-a-date' }] })).status, 400);
  assert.equal((await put({ ...original, projects: [original.projects[0], original.projects[0]] })).status, 400);
  assert.equal((await put({ ...original, experience: [{ ...original.experience[0], start: '10/2025' }] })).status, 400);
  assert.equal((await put({ ...original, projects: [{ ...original.projects[0], links: [{ label: 'x', url: 'ftp://a.b' }] }] })).status, 400);

  // Saving, conflicts and concurrency.
  const edited = { ...original, profile: { ...original.profile, intro: 'Prueba aislada de persistencia.' }, metrics: [{ id: 'test', value: '1', label: 'Prueba real' }] };
  const save = await put(edited);
  assert.equal(save.status, 200);
  let saved = await save.json();
  assert.notEqual(saved.revision, original.revision);
  assert.equal((await put(original)).status, 409);
  assert.equal((await fetch(`${base}/api/portfolio`, { headers: { 'If-None-Match': etag } })).status, 200);
  assert.equal((await (await fetch(`${base}/api/portfolio`)).json()).profile.intro, edited.profile.intro);
  const concurrent = await Promise.all([put(saved), put(saved)]);
  assert.deepEqual(concurrent.map((response) => response.status).sort(), [200, 409]);
  saved = await concurrent.find((response) => response.status === 200).json();

  // Images.
  const invalid = new FormData();
  invalid.append('file', new Blob(['<svg></svg>'], { type: 'image/svg+xml' }), 'image.svg');
  assert.equal((await fetch(`${base}/api/uploads`, { method: 'POST', headers: { 'X-Portfolio-Editor': 'local' }, body: invalid })).status, 400);
  const png = new FormData();
  png.append('file', new Blob([Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII=', 'base64')], { type: 'image/png' }), 'image.png');
  const upload = await fetch(`${base}/api/uploads`, { method: 'POST', headers: { 'X-Portfolio-Editor': 'local' }, body: png });
  assert.equal(upload.status, 200);
  const image = await upload.json();
  assert.match(image.url, /^\/uploads\/[a-f0-9]{32}\.png$/);
  const fetched = await fetch(base + image.url);
  assert.equal(fetched.status, 200);
  assert.equal(fetched.headers.get('content-type'), 'image/png');
  assert.equal(fetched.headers.get('x-content-type-options'), 'nosniff');
  assert.match(fetched.headers.get('cache-control'), /immutable/);
  assert.equal((await fetch(`${base}/api/missing`)).status, 404);
  assert.equal((await fetch(`${base}/uploads/missing.png`)).status, 404);

  // Production: persisted data, editor and API description disabled.
  await stop();
  await start('Production');
  assert.equal((await (await fetch(`${base}/api/portfolio`)).json()).profile.intro, edited.profile.intro);
  assert.equal((await (await fetch(`${base}/api/editor`)).json()).enabled, false);
  assert.equal((await put(saved)).status, 403);
  assert.equal((await fetch(`${base}/api/uploads`, { method: 'POST', headers: { 'X-Portfolio-Editor': 'local' }, body: png })).status, 403);
  assert.equal((await fetch(`${base}/api/openapi/v1.json`)).status, 404);
  console.log('OK: lectura con ETag, sitemap, GitHub desactivado, validación, protección de escritura, concurrencia, imágenes, persistencia tras reinicio y modo Production. Datos de prueba aislados.');
} finally {
  await stop();
}
