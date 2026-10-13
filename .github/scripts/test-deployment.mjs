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
else exit 2; fi
`,
);
executable(
  'curl',
  shell`
printf 'curl %s\n' "§*" >>"§MOCK_LOG"
if [[ §{MOCK_HEALTH_FAIL:-0} == 1 && §(cat "§MOCK_STATE") == new ]]; then exit 22; fi
`,
);
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
cat >"§MOCK_PACKET"
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
  return dir;
}

function run(dir, file, args = [], extra = {}) {
  const env = {
    ...process.env,
    MOCK_BIN: bin,
    MOCK_LOG: path.join(dir, 'commands.log'),
    MOCK_STATE: path.join(dir, 'state'),
    MOCK_PACKET: path.join(dir, 'packet'),
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
  console.log(
    'OK: ' +
      checks +
      ' deployment scenarios; rollback, health checks, shared lock, secret cleanup, SSH pinning and approval gates. No Docker or SSH connections.',
  );
} finally {
  const resolved = path.resolve(scratch);
  assert.equal(path.dirname(resolved), temporaryRoot);
  assert.ok(path.basename(resolved).startsWith('portfolio-deploy-tests-'));
  fs.rmSync(resolved, { recursive: true, force: true });
}
