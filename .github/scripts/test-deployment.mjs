// Deployment contract tests: fake commands, isolated folders, no Docker daemon or SSH connection.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

function shell(strings) {
  return strings.raw[0].replaceAll('§', '$');
}

const scripts = path.dirname(import.meta.filename);
const temporaryRoot = fs.realpathSync(os.tmpdir());
const scratch = fs.mkdtempSync(path.join(temporaryRoot, 'portfolio-deploy-tests-'));
const bin = path.join(scratch, 'bin');
fs.mkdirSync(bin);
const token = 'test_only_registry_token';
const commit = 'a'.repeat(40);
const previous = 'sha256:' + 'b'.repeat(64);
const digest = 'c'.repeat(64);
const bash = process.platform === 'win32' ? 'C:/Program Files/Git/bin/bash.exe' : 'bash';
let checks = 0;

function executable(name, content) {
  const file = path.join(bin, name);
  fs.writeFileSync(file, '#!/usr/bin/env bash\nset -euo pipefail\n' + content);
  fs.chmodSync(file, 0o755);
}

executable(
  'docker',
  shell`
printf 'docker %s\n' "§*" >>"§MOCK_LOG"
if [[ §1 == login ]]; then
  IFS= read -r supplied
  [[ §supplied == test_only_registry_token ]]
  printf '{"auth":"test-only"}\n' >"§DOCKER_CONFIG/config.json"
elif [[ §1 == inspect ]]; then
  printf 'sha256:%064d\n' 0 | sed 's/0/b/g'
elif [[ §1 == tag ]]; then
  if [[ §3 == *:local ]]; then
    if [[ §2 == *@sha256:* ]]; then echo new >"§MOCK_STATE"; else echo old >"§MOCK_STATE"; fi
  fi
elif [[ §1 == pull ]]; then
  [[ §{MOCK_PULL_FAIL:-0} == 0 ]]
elif [[ §1 == compose ]]; then
  if [[ §* == *'config --images'* ]]; then
    if [[ §{MOCK_CONFIG_FAIL:-0} == 1 ]]; then echo unrelated:latest
    else printf 'diego-portfolio-%s:local\n' "§{!#}"; fi
  elif [[ §* == *'up -d'* ]]; then
    if [[ §{MOCK_START_FAIL:-0} == 1 && §(cat "§MOCK_STATE") == new ]]; then exit 1; fi
  else exit 2; fi
elif [[ §1 == run ]]; then
  # Runs the content importer against the fixture volume instead of a container.
  shift
  variables=() archive='' script=''
  while (( §# )); do
    case §1 in
      -e) variables+=("§2"); shift 2 ;;
      --mount) if [[ §2 == type=bind,* ]]; then archive=§{2#*source=}; archive=§{archive%%,*}; fi; shift 2 ;;
      -c) script=§2; shift 2 ;;
      *) shift ;;
    esac
  done
  volume=§MOCK_VOLUME
  if command -v cygpath >/dev/null; then volume=§(cygpath -u "§volume"); archive=§(cygpath -u "§archive"); fi
  env "§{variables[@]}" DATA="§volume" ARCHIVE="§archive" sh -c "§script"
else exit 2; fi
`,
);
executable(
  'curl',
  shell`
printf 'curl %s\n' "§*" >>"§MOCK_LOG"
if [[ §{MOCK_HEALTH_FAIL:-0} == 1 && §(cat "§MOCK_STATE") == new ]]; then exit 22; fi
if [[ §* == *api/portfolio* && §{MOCK_CONTENT_FAIL:-0} == 1 ]] && grep -q broken "§MOCK_VOLUME/portfolio.json"; then exit 22; fi
`,
);
executable('chown', 'exit 0\n');
executable(
  'flock',
  shell`
printf 'flock %s\n' "§*" >>"§MOCK_LOG"
[[ §{MOCK_LOCK_FAIL:-0} == 0 ]]
`,
);
executable('timeout', 'shift\nexec "$@"\n');
executable(
  'gh',
  shell`
if [[ §* == *'/environments/production'* ]]; then echo "§{MOCK_REVIEWERS:-1}"
elif [[ §* == *'/git/ref/heads/main'* ]]; then echo "§MOCK_HEAD"
else exit 2; fi
`,
);
executable('ssh-keygen', '[[ ${MOCK_HOST_FAIL:-0} == 0 ]]\n');
executable(
  'ssh-agent',
  shell`
if [[ §1 == -s ]]; then
  echo 'SSH_AUTH_SOCK=/test/socket; export SSH_AUTH_SOCK; SSH_AGENT_PID=999999; export SSH_AGENT_PID;'
fi
`,
);
executable(
  'ssh-add',
  shell`
[[ §{MOCK_KEY_FAIL:-0} == 0 ]]
[[ §("§SSH_ASKPASS") == test_only_passphrase ]]
`,
);
executable(
  'ssh',
  shell`
printf 'ssh %s\n' "§*" >>"§MOCK_LOG"
calls=§(grep -c '^ssh ' "§MOCK_LOG")
cat >"§MOCK_PACKET.§calls"
cp "§MOCK_PACKET.§calls" "§MOCK_PACKET"
[[ §{MOCK_SSH_FAIL:-0} == 0 ]]
`,
);

function fixture(name) {
  const dir = path.join(scratch, name);
  fs.mkdirSync(dir);
  fs.writeFileSync(path.join(dir, 'compose.yaml'), 'services: {}\n');
  fs.mkdirSync(path.join(dir, 'volume'));
  fs.writeFileSync(path.join(dir, 'volume/portfolio.json'), 'existing-user-content');
  fs.mkdirSync(path.join(dir, 'runner'));
  // The content volume is separate, so the image scenarios still prove they never touch content.
  fs.mkdirSync(path.join(dir, 'content-volume/uploads'), { recursive: true });
  fs.writeFileSync(path.join(dir, 'content-volume/portfolio.json'), 'previous-content\n');
  fs.writeFileSync(path.join(dir, 'content-volume/visitors.json'), 'private-visits\n');
  return dir;
}

const image = 'f'.repeat(32) + '.webp';

// Versioned content as the editor leaves it, plus files that must never be published.
function content(dir, spanish = { revision: 'local', profile: { image: '/uploads/' + image } }) {
  const folder = path.join(dir, 'content');
  fs.mkdirSync(path.join(folder, 'uploads'), { recursive: true });
  fs.writeFileSync(path.join(folder, 'portfolio.json'), JSON.stringify(spanish));
  fs.writeFileSync(path.join(folder, 'portfolio.en.json'), JSON.stringify({ revision: 'local', profile: {} }));
  fs.writeFileSync(path.join(folder, 'portfolio.json.tmp'), 'save in progress');
  fs.writeFileSync(path.join(folder, 'visitors.json'), 'private-visits');
  fs.writeFileSync(path.join(folder, 'uploads', image), 'image');
  fs.writeFileSync(path.join(folder, 'uploads', '.gitkeep'), '');
  return folder;
}

function pack(dir, folder) {
  const output = path.join(dir, 'packed-' + fs.readdirSync(dir).length);
  const result = spawnSync(process.execPath, [path.join(scripts, 'pack-content.mjs'), folder, output], {
    encoding: 'utf8',
    windowsHide: true,
  });
  return { ...result, output };
}

// Builds the archive the runner uploads, at the path deploy-content.sh expects.
function archive(dir, files) {
  const folder = path.join(dir, 'archive-source');
  fs.rmSync(folder, { recursive: true, force: true });
  for (const [name, value] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(folder, name)), { recursive: true });
    fs.writeFileSync(path.join(folder, name), value);
  }
  fs.mkdirSync(path.join(dir, '.deploy/incoming'), { recursive: true });
  const target = path.join(dir, '.deploy/incoming/content-' + commit + '.tgz');
  const result = spawnSync(
    bash,
    ['-c', 'if command -v cygpath >/dev/null; then set -- "$(cygpath -u "$1")" "$(cygpath -u "$2")"; fi; tar -czf - -C "$2" . >"$1"', 'tar', target, folder],
    { windowsHide: true },
  );
  assert.equal(result.status, 0, String(result.stderr));
  return target;
}

function run(dir, file, args = [], extra = {}) {
  const env = {
    ...process.env,
    MOCK_BIN: bin,
    MOCK_LOG: path.join(dir, 'commands.log'),
    MOCK_STATE: path.join(dir, 'state'),
    MOCK_PACKET: path.join(dir, 'packet'),
    MOCK_VOLUME: path.join(dir, 'content-volume'),
    MOCK_HEAD: commit,
    GH_TOKEN: token,
    GHCR_TOKEN: token,
    GITHUB_SHA: commit,
    GITHUB_REPOSITORY: 'example/portfolio',
    GITHUB_REF: 'refs/heads/main',
    ...extra,
  };
  const result = spawnSync(
    bash,
    [
      '-c',
      'mock_bin="$MOCK_BIN"; if command -v cygpath >/dev/null; then mock_bin=$(cygpath -u "$mock_bin"); fi; export PATH="$mock_bin:$PATH"; exec bash "$@"',
      'deployment-test',
      path.join(scripts, file),
      ...args,
    ],
    { cwd: dir, env, encoding: 'utf8', timeout: 10000, windowsHide: true },
  );
  if (result.error) throw result.error;
  const log = fs.existsSync(env.MOCK_LOG) ? fs.readFileSync(env.MOCK_LOG, 'utf8') : '';
  assert.ok(!log.includes(token), 'Registry credentials must not appear in command arguments');
  assert.ok(
    !((result.stdout || '') + (result.stderr || '')).includes(token),
    'No credentials in output',
  );
  assert.equal(
    fs.readFileSync(path.join(dir, 'volume/portfolio.json'), 'utf8'),
    'existing-user-content',
  );
  if (fs.existsSync(path.join(dir, '.deploy'))) {
    assert.ok(
      !fs.readdirSync(path.join(dir, '.deploy')).some((name) => name.startsWith('registry.')),
      'Temporary registry credentials cleaned',
    );
  }
  return { ...result, log };
}

function remote(name, extra = {}, service = 'api', overrides = []) {
  const dir = fixture(name);
  const args = [
    service,
    'ghcr.io/example/diego-portfolio-' + service + '@sha256:' + digest,
    commit,
    'example/portfolio',
    'example',
    '.',
    ...overrides,
  ];
  const result = run(dir, 'deploy-container.sh', args, extra);
  return { dir, ...result };
}

try {
  for (const service of ['api', 'web']) {
    const result = remote('success-' + service, {}, service);
    assert.equal(result.status, 0, result.stderr);
    const release = JSON.parse(
      fs.readFileSync(path.join(result.dir, '.deploy/releases/' + service + '.json')),
    );
    assert.equal(release.commit, commit);
    assert.equal(release.previousImage, previous);
    assert.equal(release.image, 'ghcr.io/example/diego-portfolio-' + service + '@sha256:' + digest);
    assert.ok(
      result.log.indexOf('flock ') < result.log.indexOf('docker pull '),
      'Lock before mutations',
    );
    assert.match(
      result.log,
      new RegExp('--no-deps --force-recreate --wait --wait-timeout 120 ' + service),
    );
    assert.doesNotMatch(result.log, /docker (?:volume|system|image prune|compose down)/);
    checks++;
  }
  for (const [name, extra] of [
    ['start-failure', { MOCK_START_FAIL: '1' }],
    ['http-failure', { MOCK_HEALTH_FAIL: '1' }],
  ]) {
    const result = remote(name, extra);
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Previous image restored/);
    assert.equal(fs.readFileSync(path.join(result.dir, 'state'), 'utf8').trim(), 'old');
    assert.equal((result.log.match(/up -d/g) || []).length, 2);
    assert.ok(
      !fs.existsSync(path.join(result.dir, '.deploy/releases/api.json')),
      'Failed release not recorded',
    );
    checks++;
  }
  for (const [name, extra] of [
    ['pull-failure', { MOCK_PULL_FAIL: '1' }],
    ['lock-failure', { MOCK_LOCK_FAIL: '1' }],
    ['wrong-compose-image', { MOCK_CONFIG_FAIL: '1' }],
  ]) {
    const result = remote(name, extra);
    assert.notEqual(result.status, 0);
    assert.doesNotMatch(
      result.log,
      /up -d/,
      'Do not recreate containers before prerequisites pass',
    );
    checks++;
  }
  {
    const dir = fixture('invalid-digest');
    const result = run(dir, 'deploy-container.sh', [
      'api',
      'ghcr.io/example/diego-portfolio-api:latest',
      commit,
      'example/portfolio',
      'example',
      '.',
    ]);
    assert.notEqual(result.status, 0);
    assert.equal(result.log, '');
    checks++;
  }
  for (const [name, extra, valid] of [
    ['gate-valid', {}, true],
    ['gate-without-reviewers', { MOCK_REVIEWERS: '0' }, false],
    ['gate-stale-main', { MOCK_HEAD: 'd'.repeat(40) }, false],
    ['gate-other-branch', { GITHUB_REF: 'refs/heads/feature' }, false],
  ]) {
    const result = run(fixture(name), 'check-production.sh', [], extra);
    assert.equal(result.status === 0, valid, result.stderr);
    checks++;
  }
  for (const [name, extra, valid] of [
    ['ssh-valid', {}, true],
    ['ssh-wrong-known-host', { MOCK_HOST_FAIL: '1' }, false],
    ['ssh-key-invalid', { MOCK_KEY_FAIL: '1' }, false],
    ['ssh-connect-failure', { MOCK_SSH_FAIL: '1' }, false],
    ['ssh-injected-host', { SSH_HOST: 'server;echo bad' }, false],
  ]) {
    const dir = fixture(name);
    const result = run(dir, 'deploy-over-ssh.sh', [], {
      RUNNER_TEMP: path.join(dir, 'runner'),
      SSH_HOST: 'server.example.com',
      SSH_USER: 'deploy',
      SSH_PRIVATE_KEY: 'test_only_private_key_placeholder',
      SSH_KNOWN_HOSTS: 'server.example.com ssh-ed25519 test_only_public_key',
      SSH_KEY_PASSPHRASE: 'test_only_passphrase',
      REGISTRY_TOKEN: token,
      REGISTRY_USER: 'example',
      DEPLOY_SERVICE: 'api',
      IMAGE_REF: 'ghcr.io/example/diego-portfolio-api@sha256:' + digest,
      ...extra,
    });
    assert.equal(result.status === 0, valid, result.stderr);
    assert.deepEqual(
      fs.readdirSync(path.join(dir, 'runner')),
      [],
      'Runner key and ASKPASS files cleaned',
    );
    assert.ok(!result.log.includes('test_only_private_key_placeholder'));
    assert.ok(!result.log.includes('test_only_passphrase'));
    if (valid) {
      const packet = fs.readFileSync(path.join(dir, 'packet'), 'utf8');
      assert.equal(packet.split('\n')[0], token, 'Registry token transmitted through SSH stdin');
      assert.ok(packet.includes('flock -w 180 9'));
      assert.match(result.log, /StrictHostKeyChecking=yes/);
    }
    checks++;
  }
  {
    const dir = fixture('pack-valid');
    const folder = content(dir);
    const first = pack(dir, folder);
    assert.equal(first.status, 0, first.stderr);
    assert.deepEqual(fs.readdirSync(first.output).sort(), ['portfolio.en.json', 'portfolio.json', 'uploads']);
    assert.deepEqual(fs.readdirSync(path.join(first.output, 'uploads')), [image], 'Only editor images');
    const revision = JSON.parse(fs.readFileSync(path.join(first.output, 'portfolio.json'), 'utf8')).revision;
    assert.match(revision, /^[0-9a-f]{32}$/);
    const again = pack(dir, folder);
    assert.equal(
      fs.readFileSync(path.join(again.output, 'portfolio.json'), 'utf8'),
      fs.readFileSync(path.join(first.output, 'portfolio.json'), 'utf8'),
      'Same content, same published file',
    );
    content(dir, { revision: 'local', profile: { image: '/uploads/' + image, role: 'changed' } });
    const changed = pack(dir, folder);
    assert.notEqual(JSON.parse(fs.readFileSync(path.join(changed.output, 'portfolio.json'), 'utf8')).revision, revision);
    checks++;
  }
  for (const [name, spanish] of [
    ['pack-missing-image', { profile: { image: '/uploads/' + 'e'.repeat(32) + '.png' } }],
    ['pack-invalid-json', null],
  ]) {
    const dir = fixture(name);
    const folder = content(dir, spanish ?? {});
    if (spanish === null) fs.writeFileSync(path.join(folder, 'portfolio.json'), '{ not json');
    const result = pack(dir, folder);
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Content error/);
    checks++;
  }
  const published = { 'portfolio.json': 'new-content\n', 'portfolio.en.json': 'new-english\n', ['uploads/' + image]: 'image' };
  {
    const dir = fixture('content-publish');
    const uploaded = archive(dir, published);
    const result = run(dir, 'deploy-content.sh', [commit, 'example/portfolio', '.']);
    assert.equal(result.status, 0, result.stderr);
    const volume = path.join(dir, 'content-volume');
    assert.equal(fs.readFileSync(path.join(volume, 'portfolio.json'), 'utf8'), 'new-content\n');
    assert.equal(fs.readFileSync(path.join(volume, 'portfolio.en.json'), 'utf8'), 'new-english\n');
    assert.equal(fs.readFileSync(path.join(volume, 'uploads', image), 'utf8'), 'image');
    assert.equal(fs.readFileSync(path.join(volume, 'visitors.json'), 'utf8'), 'private-visits\n', 'Visits untouched');
    const backups = fs.readdirSync(path.join(volume, 'backups'));
    assert.equal(backups.length, 1, 'Only the language that existed is backed up');
    assert.equal(fs.readFileSync(path.join(volume, 'backups', backups[0]), 'utf8'), 'previous-content\n');
    assert.ok(!fs.existsSync(uploaded), 'Uploaded archive removed');
    assert.equal(JSON.parse(fs.readFileSync(path.join(dir, '.deploy/releases/content.json'))).commit, commit);
    assert.ok(result.log.indexOf('flock ') < result.log.indexOf('docker run '), 'Lock before mutations');
    assert.match(result.log, /api\/portfolio\?language=es/);
    assert.match(result.log, /api\/portfolio\?language=en/);

    archive(dir, published);
    const unchanged = run(dir, 'deploy-content.sh', [commit, 'example/portfolio', '.']);
    assert.equal(unchanged.status, 0, unchanged.stderr);
    assert.match(unchanged.stdout, /Content unchanged/);
    assert.equal(fs.readdirSync(path.join(volume, 'backups')).length, 1, 'No backup when nothing changes');
    checks++;
  }
  {
    const dir = fixture('content-rollback');
    archive(dir, { ...published, 'portfolio.json': 'broken-content\n' });
    const result = run(dir, 'deploy-content.sh', [commit, 'example/portfolio', '.'], { MOCK_CONTENT_FAIL: '1' });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Previous content restored/);
    assert.equal(fs.readFileSync(path.join(dir, 'content-volume/portfolio.json'), 'utf8'), 'previous-content\n');
    assert.ok(!fs.existsSync(path.join(dir, '.deploy/releases/content.json')), 'Failed release not recorded');
    checks++;
  }
  for (const [name, files] of [
    ['content-unexpected-file', { ...published, 'visitors.json': 'overwrite' }],
    ['content-unexpected-image', { ...published, 'uploads/script.sh': 'run me' }],
    ['content-without-spanish', { 'portfolio.en.json': 'new-english\n' }],
  ]) {
    const dir = fixture(name);
    archive(dir, files);
    const result = run(dir, 'deploy-content.sh', [commit, 'example/portfolio', '.']);
    assert.notEqual(result.status, 0);
    assert.doesNotMatch(result.log, /docker run/, 'Rejected before touching the volume');
    assert.equal(fs.readFileSync(path.join(dir, 'content-volume/portfolio.json'), 'utf8'), 'previous-content\n');
    checks++;
  }
  {
    const dir = fixture('ssh-content');
    content(dir);
    const result = run(dir, 'deploy-over-ssh.sh', [], {
      RUNNER_TEMP: path.join(dir, 'runner'),
      SSH_HOST: 'server.example.com',
      SSH_USER: 'deploy',
      SSH_PRIVATE_KEY: 'test_only_private_key_placeholder',
      SSH_KNOWN_HOSTS: 'server.example.com ssh-ed25519 test_only_public_key',
      SSH_KEY_PASSPHRASE: 'test_only_passphrase',
      DEPLOY_SERVICE: 'content',
      CONTENT_PATH: 'content',
    });
    assert.equal(result.status, 0, result.stderr);
    assert.equal((result.log.match(/^ssh /gm) || []).length, 2, 'Archive upload, then publication');
    const upload = fs.readFileSync(path.join(dir, 'packet.1'));
    assert.deepEqual([...upload.subarray(0, 2)], [0x1f, 0x8b], 'The archive travels gzipped');
    assert.match(result.log, new RegExp('content-' + commit + '\\.tgz'));
    assert.ok(fs.readFileSync(path.join(dir, 'packet.2'), 'utf8').includes('flock -w 180 9'));
    assert.ok(!result.log.includes('test_only_private_key_placeholder'));
    assert.deepEqual(fs.readdirSync(path.join(dir, 'runner')), [], 'Runner files cleaned');
    checks++;
  }
  console.log(
    'OK: ' +
      checks +
      ' deployment scenarios; rollback, health checks, shared lock, secret cleanup, SSH pinning, approval gates and content publication. No Docker or SSH connections.',
  );
} finally {
  const resolved = path.resolve(scratch);
  assert.equal(path.dirname(resolved), temporaryRoot);
  assert.ok(path.basename(resolved).startsWith('portfolio-deploy-tests-'));
  fs.rmSync(resolved, { recursive: true, force: true });
}
