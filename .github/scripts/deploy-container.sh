#!/usr/bin/env bash
# Executed over SSH. Credentials arrive on stdin via the wrapper, never in arguments.
set -euo pipefail
set +x
umask 077

fail() { printf 'Deployment error: %s\n' "$*" >&2; exit 1; }
[[ $# == 6 ]] || fail 'Expected service, image digest, commit, repository, registry user and directory.'
service=$1 image_ref=$2 commit=$3 repository=$4 registry_user=$5 deployment_path=$6
[[ $service == api || $service == web ]] || fail 'Only api and web can be deployed.'
[[ $commit =~ ^[0-9a-f]{40}$ ]] || fail 'Invalid commit.'
[[ $repository =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || fail 'Invalid repository.'
[[ $registry_user =~ ^[A-Za-z0-9_-]+$ ]] || fail 'Invalid registry user.'
owner=${repository%%/*}
expected_ref="ghcr.io/${owner,,}/diego-portfolio-$service"
[[ $image_ref == "$expected_ref"@sha256:* ]] && [[ ${image_ref##*@sha256:} =~ ^[0-9a-f]{64}$ ]] || fail 'Expected this service image pinned to a digest.'
registry_token=${GHCR_TOKEN:?Missing temporary registry token}
unset GHCR_TOKEN

cd -- "$deployment_path"
deployment_root=$(pwd -P)
[[ -f compose.yaml ]] || fail 'Provision the production stack before enabling CI/CD.'
mkdir -p .deploy/releases
# Both repositories acquire the same lock on the VM.
exec 9>.deploy/deploy.lock
flock -w 180 9 || fail 'Another deployment still owns the VM lock.'

compose=(docker compose --project-name diego-portfolio --file compose.yaml)
local_image="diego-portfolio-$service:local"
configured_image=$("${compose[@]}" config --images "$service")
[[ $configured_image == "$local_image" ]] || fail 'The production Compose image does not match this deployment.'
container="diego-portfolio-$service"
previous_image=$(docker inspect --format '{{.Image}}' "$container")
[[ $previous_image =~ ^sha256:[0-9a-f]{64}$ ]] || fail 'The existing service image could not be identified.'
docker tag "$previous_image" "diego-portfolio-$service:previous"

docker_config=$(mktemp -d "$deployment_root/.deploy/registry.XXXXXX")
export DOCKER_CONFIG=$docker_config
changed=0

probe() {
  # The Nginx check alone does not prove that the API proxy is working.
  curl --fail --silent --show-error --retry 6 --retry-delay 2 --retry-all-errors \
    --connect-timeout 5 --max-time 15 http://127.0.0.1:8080/healthz >/dev/null &&
  curl --fail --silent --show-error --retry 6 --retry-delay 2 --retry-all-errors \
    --connect-timeout 5 --max-time 15 http://127.0.0.1:8080/api/health >/dev/null
}

finish() {
  status=$?
  trap - EXIT
  if (( status != 0 && changed == 1 )); then
    printf 'Deployment failed; restoring the previous %s image.\n' "$service" >&2
    if docker tag "$previous_image" "$local_image" &&
       "${compose[@]}" up -d --no-deps --force-recreate --wait --wait-timeout 120 "$service" &&
       probe; then
      printf 'Previous image restored and HTTP checks passed.\n' >&2
    else
      printf 'ROLLBACK FAILED: check the service on the VM immediately.\n' >&2
    fi
  fi
  # Only remove the private directory created by mktemp inside this deployment.
  if [[ $docker_config == "$deployment_root/.deploy/registry."* && -d $docker_config ]]; then
    rm -rf -- "$docker_config"
  fi
  exit "$status"
}
trap finish EXIT

printf '%s\n' "$registry_token" | docker login ghcr.io --username "$registry_user" --password-stdin >/dev/null
unset registry_token
timeout 180 docker pull "$image_ref"
changed=1
# The image is downloaded by immutable digest. The local tag keeps the existing Compose compatible.
docker tag "$image_ref" "$local_image"
"${compose[@]}" up -d --no-deps --force-recreate --wait --wait-timeout 120 "$service"
probe

release_file=".deploy/releases/$service.json"
if [[ -f $release_file ]]; then
  cp -- "$release_file" ".deploy/releases/$service.previous.json"
fi
release_temp=$(mktemp ".deploy/releases/$service.XXXXXX")
printf '{"service":"%s","image":"%s","commit":"%s","repository":"%s","previousImage":"%s","deployedAt":"%s"}\n' \
  "$service" "$image_ref" "$commit" "$repository" "$previous_image" "$(date -u +%FT%TZ)" >"$release_temp"
mv -- "$release_temp" "$release_file"
printf 'Deployment completed: %s at commit %s.\n' "$service" "$commit"
