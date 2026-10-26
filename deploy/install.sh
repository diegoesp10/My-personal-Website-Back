#!/usr/bin/env bash
# Runs on the server, called by scripts/Deploy-Server.ps1 from ~/portfolio:
#   install.sh [--replace-data]  loads the uploaded images, imports the content into the data volume when it is empty
#                                (or replaces it, keeping the previous one in backups/) and (re)starts the stack.
#   install.sh --set-token       reads the Cloudflare Tunnel token from stdin, stores it in .env and starts the tunnel.
#   install.sh --content-only    replaces only the content with the uploaded data.tgz, keeping the previous one in
#                                backups/, without touching images or containers.
# The visit statistics (visitors.json) live only on the server and are never replaced.
set -euo pipefail
cd "$(dirname "$0")"

if [ "${1:-}" = "--content-only" ]; then
  [ -f data.tgz ] || { echo 'Falta data.tgz con el contenido.' >&2; exit 1; }
  echo '== Contenido'
  # The API reads the files on every request, so each language is moved into place with an atomic rename while it runs.
  docker run --rm --user 0 --entrypoint sh \
    --mount type=volume,source=diego-portfolio-data,target=/app/data \
    --mount type=bind,source="$PWD/data.tgz",target=/import/data.tgz,readonly \
    diego-portfolio-api:local -c '
      set -e
      stage=/app/data/.import
      rm -rf "$stage"
      mkdir -p "$stage" /app/data/backups
      tar -xzf /import/data.tgz -C "$stage"
      stamp=$(date +%Y%m%d-%H%M%S)
      for path in "$stage"/portfolio.json "$stage"/portfolio.*.json; do
        [ -f "$path" ] || continue
        file=${path##*/}
        if [ -e "/app/data/$file" ]; then cp "/app/data/$file" "/app/data/backups/${file%.json}-$stamp.json"; fi
        chown app:app "$path"
        chmod 644 "$path"
        mv "$path" "/app/data/$file"
        echo "Publicado $file (copia anterior en backups/)."
      done
      if [ -d "$stage/uploads" ]; then
        mkdir -p /app/data/uploads
        cp -R "$stage/uploads/." /app/data/uploads/
      fi
      rm -rf "$stage"
      chown -R app:app /app/data/uploads /app/data/backups'
  rm data.tgz
  for language in es en; do
    curl -fsS -o /dev/null "http://127.0.0.1:8080/api/portfolio?language=$language" \
      || { echo "La API no sirve el contenido en $language: revisa backups/ en el volumen." >&2; exit 1; }
  done
  echo 'Contenido publicado y servido por la API.'
  exit 0
fi

if [ "${1:-}" = "--set-token" ]; then
  # Accepts the token alone or the whole command shown by Cloudflare (docker run ... --token eyJ...).
  if [ -t 0 ]; then
    IFS= read -rsp 'Pega el comando de Cloudflare (o solo el token) y pulsa Enter: ' input || true
    echo
  else
    IFS= read -r input || true
  fi
  token=$(printf '%s' "$input" | grep -oE 'eyJ[A-Za-z0-9_+/=.-]+' | head -n 1 || true)
  if [ -z "$token" ]; then echo 'No parece un token de Cloudflare Tunnel (empieza por eyJ).' >&2; exit 1; fi
  umask 077
  printf 'COMPOSE_PROFILES=tunnel\nCLOUDFLARE_TUNNEL_TOKEN=%s\n' "$token" > .env
  docker compose up -d --wait --wait-timeout 120
  sleep 5
  docker compose logs --tail 15 tunnel
  exit 0
fi

replace=0
[ "${1:-}" = "--replace-data" ] && replace=1

if [ -f images.tar.gz ]; then
  echo '== Cargando imágenes'
  gunzip -c images.tar.gz | docker load
  rm images.tar.gz
fi

docker volume create diego-portfolio-data >/dev/null
if [ -f data.tgz ]; then
  echo '== Contenido'
  docker run --rm --user 0 --entrypoint sh -e REPLACE="$replace" \
    --mount type=volume,source=diego-portfolio-data,target=/app/data \
    --mount type=bind,source="$PWD/data.tgz",target=/import/data.tgz,readonly \
    diego-portfolio-api:local -c '
      set -e
      if [ "$REPLACE" = 1 ]; then
        # Only the languages included in the archive are replaced; each one is backed up first.
        for file in $(tar -tzf /import/data.tgz | grep -E "^portfolio(\.[a-z]+)?\.json$"); do
          if [ -e "/app/data/$file" ]; then
            mkdir -p /app/data/backups
            cp "/app/data/$file" "/app/data/backups/${file%.json}-$(date +%Y%m%d-%H%M%S).json"
            rm "/app/data/$file"
          fi
        done
      fi
      if [ -e /app/data/portfolio.json ]; then
        echo "El servidor ya tiene contenido y se conserva (-ReplaceData lo sustituye)."
      else
        tar -xzf /import/data.tgz -C /app/data
        echo "Contenido local importado."
      fi
      chown -R app:app /app/data
      chmod -R u+rwX,go-w /app/data'
  rm data.tgz
fi

echo '== Arrancando'
docker compose up -d --wait --wait-timeout 120
docker image prune -f >/dev/null
curl -fsS -o /dev/null http://127.0.0.1:8080/api/health && echo 'Web y API responden.'
docker compose ps --format 'table {{.Name}}\t{{.Status}}'
