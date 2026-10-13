#!/usr/bin/env bash
# Runs on the server, called by scripts/Deploy-Server.ps1 from ~/portfolio:
#   install.sh [--replace-data]  loads the uploaded images, imports the content into the data volume when it is empty
#                                (or replaces it, keeping the previous one in backups/) and (re)starts the stack.
#   install.sh --set-token       reads the Cloudflare Tunnel token from stdin, stores it in .env and starts the tunnel.
# The visit statistics (visitors.json) live only on the server and are never replaced.
set -euo pipefail
cd "$(dirname "$0")"

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
      if [ -e /app/data/portfolio.json ] && [ "$REPLACE" = 1 ]; then
        mkdir -p /app/data/backups
        cp /app/data/portfolio.json "/app/data/backups/portfolio-$(date +%Y%m%d-%H%M%S).json"
        rm /app/data/portfolio.json
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
