// Real HTTP regression tests with isolated data. Never touches App_Data or the deployed server.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');
mkdirSync(resolve(root, '.test-data'), { recursive: true });
const dataPath = mkdtempSync(resolve(root, '.test-data/errors-'));
const base = 'http://127.0.0.1:5083';
const editor = { 'X-Portfolio-Editor': 'local' };
let server;
async function stop() {
  if (server && server.exitCode === null) { const ended = once(server, 'exit'); server.kill(); await ended; }
}
async function start(mode = 'Development', permitLimit = 300) {
  server = spawn('dotnet', [resolve(root, 'Portfolio.Api/bin/Debug/net10.0/Portfolio.Api.dll')], {
    cwd: resolve(root, 'Portfolio.Api'), windowsHide: true,
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: mode, ASPNETCORE_URLS: base,
      Portfolio__DataPath: dataPath, GitHub__Enabled: 'false', Visitors__Enabled: 'false',
      RateLimiting__PermitLimit: String(permitLimit), ErrorRegistry__MaximumEntries: '1000' },
  });
  let output = '';
  server.stdout.on('data', data => { output += data; });
  server.stderr.on('data', data => { output += data; });
  for (let index = 0; index < 80; index++) {
    if (server.exitCode !== null) throw new Error(output);
    try { if ((await fetch(base + '/api/health')).ok) return; } catch {}
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('API did not start: ' + output);
}
async function problem(response, status, code, language) {
  assert.equal(response.status, status);
  assert.match(response.headers.get('content-type'), /^application\/problem\+json/);
  assert.equal(response.headers.get('cache-control'), 'no-store');
  const error = await response.json();
  assert.equal(error.status, status); assert.equal(error.code, code); assert.equal(error.language, language);
  assert.equal(error.type, 'about:blank'); assert.equal(error.error, error.detail);
  assert.equal(error.traceId, response.headers.get('X-Correlation-ID'));
  assert.equal(response.headers.get('content-language'), language);
  assert.ok(error.traceId);
  assert.ok(!JSON.stringify(error).includes('do-not-record'));
  return error;
}
const put = body => fetch(base + '/api/portfolio', {
  method: 'PUT', headers: { ...editor, 'Content-Type': 'application/json' }, body: JSON.stringify(body),
});
async function history(language = 'es', headers = editor) {
  const response = await fetch(base + '/api/editor/errors?language=' + language, { headers });
  assert.equal(response.status, 200);
  return response.json();
}
try {
  await start();
  const es = await (await fetch(base + '/api/portfolio')).json();
  const enResponse = await fetch(base + '/api/portfolio?language=en');
  const en = await enResponse.json();
  assert.equal(en.language, 'en'); assert.equal(en.message, 'Request completed successfully.');
  // Each language has its own authored copy of the content, with the same identifiers.
  assert.equal(en.profile.name, es.profile.name); assert.notEqual(en.profile.intro, es.profile.intro);
  assert.deepEqual(en.projects.map(project => project.id), es.projects.map(project => project.id));
  assert.match(enResponse.headers.get('etag'), /-en"$/);
  assert.equal((await fetch(base + '/api/portfolio?language=en', { headers: { 'If-None-Match': '"' + en.revision + '"' } })).status, 200);
  const validation = await problem(await put({ ...es, language: 'en', profile: { ...es.profile, name: '' }, password: 'do-not-record' }), 400, 'portfolio.profile', 'en');
  assert.equal(validation.detail, 'Name and specialisation are required.');
  await problem(await put({ ...es, profile: { ...es.profile, name: '' } }), 400, 'portfolio.profile', 'es');
  await problem(await put({ ...es, language: 'fr' }), 400, 'request.language', 'es');
  await problem(await put({ ...es, language: null }), 400, 'request.language', 'es');
  await problem(await fetch(base + '/api/health?language=en&language=es'), 400, 'request.language', 'es');
  await problem(await fetch(base + '/api/portfolio?language=en', { method: 'PUT', headers: { ...editor, 'Content-Type': 'application/json' }, body: '{broken' }), 400, 'request.invalid', 'es');
  await problem(await fetch(base + '/api/portfolio', { method: 'PUT', headers: { ...editor, 'Content-Type': 'application/json' }, body: '{"language":"en","language":"es"}' }), 400, 'request.language', 'es');
  await problem(await fetch(base + '/api/missing/do-not-record?password=do-not-record&language=en'), 404, 'request.not_found', 'en');
  await problem(await fetch(base + '/api/health?language=en', { method: 'DELETE', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ language: 'en' }) }), 404, 'request.not_found', 'en');
  await problem(await fetch(base + '/api/editor/errors?language=en'), 403, 'request.forbidden', 'en');
  await problem(await fetch(base + '/api/editor/errors?language=en', { headers: { ...editor, Origin: 'https://other.example' } }), 403, 'request.forbidden', 'en');
  const form = new FormData(); form.set('language', 'en'); form.set('file', new Blob(['do-not-record']), 'private-file.txt');
  const formatError = await problem(await fetch(base + '/api/uploads', { method: 'POST', headers: editor, body: form }), 400, 'image.format', 'en');
  assert.match(formatError.detail, /Unsupported format/);
  const saved = await (await put({ ...en, language: 'en' })).json();
  assert.equal(saved.message, 'Content saved successfully.'); assert.equal(saved.language, 'en');
  // Saved in the English file only; language and message are transport fields, never stored.
  const persisted = JSON.parse(readFileSync(resolve(dataPath, 'portfolio.en.json'), 'utf8'));
  assert.equal(persisted.revision, saved.revision); assert.equal(persisted.profile.intro, en.profile.intro);
  assert.equal(persisted.language, undefined); assert.equal(persisted.message, undefined);
  assert.equal(JSON.parse(readFileSync(resolve(dataPath, 'portfolio.json'), 'utf8')).revision, es.revision);
  const conflict = await problem(await put({ ...en, language: 'en' }), 409, 'portfolio.conflict', 'en');
  assert.match(conflict.detail, /Reload the editor/);
  // Force an unexpected server error using ONLY the isolated test content, then restore it.
  const file = resolve(dataPath, 'portfolio.en.json'); const content = readFileSync(file, 'utf8');
  writeFileSync(file, '{do-not-record');
  await problem(await fetch(base + '/api/portfolio?language=en'), 500, 'server.unexpected', 'en');
  writeFileSync(file, content);
  let records;
  for (let index = 0; index < 20; index++) {
    records = await history('en');
    if (records.entries.some(entry => entry.traceId === validation.traceId)) break;
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  const registered = records.entries.find(entry => entry.traceId === validation.traceId);
  assert.ok(registered); assert.equal(registered.message, validation.detail);
  assert.ok(records.entries.some(entry => entry.code === 'server.unexpected' && entry.exceptionType === 'System.Text.Json.JsonException'));
  assert.ok(!JSON.stringify(records).includes('do-not-record'));
  await stop(); await start();
  assert.ok((await history()).entries.some(entry => entry.traceId === validation.traceId));
  await stop(); await start('Production');
  await problem(await fetch(base + '/api/editor/errors?language=en', { headers: editor }), 403, 'request.forbidden', 'en');
  await problem(await put({ ...saved, language: 'en' }), 403, 'request.forbidden', 'en');
  await stop(); await start('Production', 2);
  await fetch(base + '/api/health');
  await problem(await fetch(base + '/api/health?language=en'), 429, 'request.rate_limit', 'en');
  await problem(await fetch(base + '/api/health?language=fr'), 429, 'request.rate_limit', 'es');
  await problem(await put({ language: 'fr' }), 429, 'request.rate_limit', 'es');
  await problem(await fetch(base + '/sitemap.xml'), 429, 'request.rate_limit', 'es');

  // A broken registry must not replace the original validation error.
  await stop(); writeFileSync(resolve(dataPath, 'errors.json'), 'do-not-record-invalid-history'); await start();
  await problem(await put({ ...saved, language: 'en', profile: { ...saved.profile, name: '' } }), 400, 'portfolio.profile', 'en');
  assert.equal(readFileSync(resolve(dataPath, 'errors.json'), 'utf8'), 'do-not-record-invalid-history');
  console.log('OK: language contracts, localised successes and errors, ETag per language, multipart, 400/403/404/409/429/500, private history, correlation, privacy, restart and registry failure.');
} finally { await stop(); }
