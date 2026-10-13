#!/usr/bin/env bash
# Run only in the protected production job, with secrets supplied through environment variables.
set -euo pipefail
set +x
umask 077

fail() { printf 'SSH deployment error: %s\n' "$*" >&2; exit 1; }
: "${SSH_PRIVATE_KEY:?Configure SSH_PRIVATE_KEY in the production environment}"
: "${SSH_KNOWN_HOSTS:?Configure SSH_KNOWN_HOSTS in the production environment}"
: "${SSH_HOST:?Configure SSH_HOST in the production environment}"
: "${SSH_USER:?Configure SSH_USER in the production environment}"
: "${REGISTRY_TOKEN:?Missing temporary GitHub token}"
: "${RUNNER_TEMP:?Run this script on the GitHub runner}"
: "${DEPLOY_SERVICE:?Missing service}"
: "${IMAGE_REF:?Missing immutable image reference}"
: "${GITHUB_SHA:?Missing source commit}"
: "${GITHUB_REPOSITORY:?Missing source repository}"
: "${REGISTRY_USER:?Missing registry user}"
SSH_PORT=${SSH_PORT:-22}
DEPLOY_PATH=${DEPLOY_PATH:-portfolio}

[[ $SSH_HOST =~ ^[A-Za-z0-9][A-Za-z0-9.:-]*$ ]] || fail 'Invalid SSH host.'
[[ $SSH_USER =~ ^[A-Za-z0-9_][A-Za-z0-9_-]*$ ]] || fail 'Invalid SSH user.'
[[ $SSH_PORT =~ ^[0-9]{1,5}$ ]] && (( 10#$SSH_PORT > 0 && 10#$SSH_PORT <= 65535 )) || fail 'Invalid SSH port.'
[[ $DEPLOY_PATH =~ ^[A-Za-z0-9_./-]+$ ]] || fail 'Use a simple absolute or home-relative deployment path.'
[[ $DEPLOY_SERVICE == api || $DEPLOY_SERVICE == web ]] || fail 'Invalid service.'
[[ $GITHUB_SHA =~ ^[0-9a-f]{40}$ ]] || fail 'Invalid commit.'
[[ $GITHUB_REPOSITORY =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || fail 'Invalid repository.'
[[ $REGISTRY_USER =~ ^[A-Za-z0-9_-]+$ ]] || fail 'Invalid registry user.'
owner=${GITHUB_REPOSITORY%%/*}
expected_ref="ghcr.io/${owner,,}/diego-portfolio-$DEPLOY_SERVICE"
[[ $IMAGE_REF == "$expected_ref"@sha256:* ]] && [[ ${IMAGE_REF##*@sha256:} =~ ^[0-9a-f]{64}$ ]] || fail 'Invalid image digest.'

temporary=$(mktemp -d "$RUNNER_TEMP/portfolio-ssh.XXXXXX")
agent_started=0
cleanup() {
  if (( agent_started == 1 )); then ssh-agent -k >/dev/null 2>&1 || true; fi
  if [[ $temporary == "$RUNNER_TEMP/portfolio-ssh."* && -d $temporary ]]; then
    rm -rf -- "$temporary"
  fi
}
trap cleanup EXIT

printf '%s\n' "$SSH_PRIVATE_KEY" | tr -d '\r' >"$temporary/id"
printf '%s\n' "$SSH_KNOWN_HOSTS" | tr -d '\r' >"$temporary/known_hosts"
chmod 600 "$temporary/id" "$temporary/known_hosts"
unset SSH_PRIVATE_KEY SSH_KNOWN_HOSTS
lookup=$SSH_HOST
if (( 10#$SSH_PORT != 22 )); then lookup="[$SSH_HOST]:$SSH_PORT"; fi
ssh-keygen -F "$lookup" -f "$temporary/known_hosts" >/dev/null || fail 'Pinned known_hosts does not contain this host/port.'

# The optional passphrase is supplied via ASKPASS, never as a process argument.
cat >"$temporary/askpass" <<'ASKPASS'
#!/usr/bin/env bash
printf '%s\n' "${SSH_KEY_PASSPHRASE:-}"
ASKPASS
chmod 700 "$temporary/askpass"
export SSH_ASKPASS="$temporary/askpass" SSH_ASKPASS_REQUIRE=force DISPLAY=portfolio-actions
eval "$(ssh-agent -s)" >/dev/null
agent_started=1
ssh-add "$temporary/id" </dev/null >/dev/null 2>&1 || fail 'The SSH key or its passphrase is invalid.'
unset SSH_KEY_PASSPHRASE

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
# All arguments are validated above; only metadata appears in the command, not the registry token.
printf -v arguments '%q ' "$DEPLOY_SERVICE" "$IMAGE_REF" "$GITHUB_SHA" \
  "$GITHUB_REPOSITORY" "$REGISTRY_USER" "$DEPLOY_PATH"
remote_command="set +x; IFS= read -r GHCR_TOKEN; export GHCR_TOKEN; exec bash -s -- $arguments"
{
  printf '%s\n' "$REGISTRY_TOKEN"
  cat "$script_dir/deploy-container.sh"
} | ssh -p "$SSH_PORT" -i "$temporary/id" \
  -o BatchMode=yes -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
  -o "UserKnownHostsFile=$temporary/known_hosts" \
  -o ConnectTimeout=15 -o ServerAliveInterval=15 -o ServerAliveCountMax=4 \
  "$SSH_USER@$SSH_HOST" "$remote_command"
