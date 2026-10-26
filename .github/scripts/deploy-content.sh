#!/usr/bin/env bash
# Executed over SSH after deploy-over-ssh.sh (or Deploy-Server.ps1 -ContentOnly) uploads the archive built by
# pack-content.mjs to .deploy/incoming/content-<commit>.tgz. Publishes the versioned content into the data volume:
# every changed language is backed up in backups/ and moved into place with an atomic rename while the API keeps
# running (it reads the files on every request); new images are added and existing ones are never overwritten.
# If the API stops serving a language, the backups of this run are restored. Visits and errors are never touched.
set -euo pipefail
set +x
umask 077

fail() { printf 'Content deployment error: %s\n' "$*" >&2; exit 1; }
[[ $# == 3 ]] || fail 'Expected commit, repository and directory.'
commit=$1 repository=$2 deployment_path=$3
[[ $commit =~ ^[0-9a-f]{40}$ ]] || fail 'Invalid commit.'
[[ $repository =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]] || fail 'Invalid repository.'

cd -- "$deployment_path"
deployment_root=$(pwd -P)
[[ -f compose.yaml ]] || fail 'Provision the production stack before enabling CI/CD.'
archive="$deployment_root/.deploy/incoming/content-$commit.tgz"
[[ -f $archive ]] || fail 'The content archive was not uploaded.'
trap 'rm -f -- "$archive"' EXIT
mkdir -p .deploy/releases
# Both repositories acquire the same lock on the VM.
exec 9>.deploy/deploy.lock
flock -w 180 9 || fail 'Another deployment still owns the VM lock.'

# Only content may enter the volume: language files and images with the names the API generates, no links.
while IFS= read -r line; do
  [[ ${line:0:1} == - || ${line:0:1} == d ]] || fail 'The content archive may only contain files and folders.'
done < <(tar -tvzf - <"$archive")
languages=()
while IFS= read -r entry; do
  entry=${entry#./}
  if [[ $entry =~ ^portfolio(\.([a-z]{2}))?\.json$ ]]; then
    languages+=("${BASH_REMATCH[2]:-es}")
  elif [[ -n $entry && $entry != uploads/ && $entry != uploads && ! $entry =~ ^uploads/[a-f0-9]{32}\.(png|jpg|webp)$ ]]; then
    fail "Unexpected entry in the content archive: $entry"
  fi
done < <(tar -tzf - <"$archive")
[[ " ${languages[*]} " == *' es '* ]] || fail 'The content archive has no Spanish content.'

# Runs inside the API image as root, with the volume at $DATA and the archive at $ARCHIVE.
importer=$(cat <<'IMPORTER'
set -eu
case "$MODE" in
  publish)
    stage="$DATA/.content-$STAMP"
    rm -rf "$stage"
    mkdir -p "$stage" "$DATA/backups" "$DATA/uploads"
    tar -xzf - -C "$stage" <"$ARCHIVE"
    for path in "$stage"/portfolio*.json; do
      [ -f "$path" ] || continue
      file=${path##*/}
      if [ -f "$DATA/$file" ]; then
        [ "$(sha256sum <"$DATA/$file")" = "$(sha256sum <"$path")" ] && continue
        cp "$DATA/$file" "$DATA/backups/${file%.json}-$STAMP.json"
      fi
      chown app:app "$path"
      chmod 644 "$path"
      mv "$path" "$DATA/$file"
      echo "published $file"
    done
    for path in "$stage"/uploads/*; do
      [ -f "$path" ] || continue
      name=${path##*/}
      # Image names are random and never reused, so an existing one is already the same image.
      [ -e "$DATA/uploads/$name" ] && continue
      chown app:app "$path"
      chmod 644 "$path"
      mv "$path" "$DATA/uploads/$name"
      echo "published uploads/$name"
    done
    rm -rf "$stage"
    chown app:app "$DATA/backups" "$DATA/uploads"
    ;;
  restore)
    for path in "$DATA"/backups/*-"$STAMP".json; do
      [ -f "$path" ] || continue
      name=${path##*/}
      file="${name%-"$STAMP".json}.json"
      cp "$path" "$DATA/$file.restoring"
      chown app:app "$DATA/$file.restoring"
      mv "$DATA/$file.restoring" "$DATA/$file"
      echo "restored $file"
    done
    ;;
esac
IMPORTER
)
stamp=$(date -u +%Y%m%d-%H%M%S)
content() {
  docker run --rm --user 0 --network none --entrypoint sh \
    -e "MODE=$1" -e "STAMP=$stamp" -e DATA=/app/data -e ARCHIVE=/import/content.tgz \
    --mount type=volume,source=diego-portfolio-data,target=/app/data \
    --mount "type=bind,source=$archive,target=/import/content.tgz,readonly" \
    diego-portfolio-api:local -c "$importer"
}

probe() {
  local language
  for language in "${languages[@]}"; do
    curl --fail --silent --show-error --retry 3 --retry-delay 2 --retry-all-errors \
      --connect-timeout 5 --max-time 15 -o /dev/null "http://127.0.0.1:8080/api/portfolio?language=$language" ||
      return 1
  done
}

changes=$(content publish)
if [[ -z $changes ]]; then
  printf 'Content unchanged at commit %s.\n' "$commit"
  exit 0
fi
printf '%s\n' "$changes"
if ! probe; then
  printf 'The API does not serve the new content; restoring the previous version.\n' >&2
  if content restore >&2 && probe; then
    printf 'Previous content restored and served.\n' >&2
  else
    printf 'RESTORE FAILED: check the content on the VM immediately (backups/*-%s.json).\n' "$stamp" >&2
  fi
  exit 1
fi

release_file=.deploy/releases/content.json
if [[ -f $release_file ]]; then
  cp -- "$release_file" .deploy/releases/content.previous.json
fi
release_temp=$(mktemp .deploy/releases/content.XXXXXX)
printf '{"service":"content","commit":"%s","repository":"%s","backups":"%s","deployedAt":"%s"}\n' \
  "$commit" "$repository" "$stamp" "$(date -u +%FT%TZ)" >"$release_temp"
mv -- "$release_temp" "$release_file"
printf 'Content deployment completed at commit %s.\n' "$commit"
