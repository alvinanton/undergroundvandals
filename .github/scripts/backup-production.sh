#!/usr/bin/env bash
set -euo pipefail
umask 077

: "${PRODUCTION_DATABASE_URL:?Missing production database URL}"
: "${AZURE_STORAGE_CONNECTION_STRING:?Missing backup storage credentials}"
: "${POSTGRES_IMAGE:?Missing PostgreSQL tools image}"
: "${AZURE_CLI_IMAGE:?Missing Azure CLI image}"
: "${RUNNER_TEMP:?Missing runner temporary directory}"
: "${GITHUB_RUN_ID:?Missing workflow run ID}"
: "${GITHUB_RUN_ATTEMPT:?Missing workflow attempt}"
: "${GITHUB_SHA:?Missing release commit}"
: "${GITHUB_OUTPUT:?Missing step output file}"

expected_dir="$RUNNER_TEMP/production-backup-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"
if [[ "${BACKUP_DIR:-}" != "$expected_dir" ]]; then
  echo "Unexpected backup directory." >&2
  exit 1
fi
if [[ "$PRODUCTION_DATABASE_URL" != postgresql://* && "$PRODUCTION_DATABASE_URL" != postgres://* ]]; then
  echo "The backup connection must be a PostgreSQL URL." >&2
  exit 1
fi

dump_container="uv-prod-dump-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"
restore_container="uv-prod-restore-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"
cleanup() {
  docker rm --force "$dump_container" "$restore_container" > /dev/null 2>&1 || true
  if [[ "$BACKUP_DIR" == "$expected_dir" && -d "$BACKUP_DIR" ]]; then
    rm -rf -- "$BACKUP_DIR"
  fi
}
trap cleanup EXIT

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"
docker pull "$POSTGRES_IMAGE"
docker pull "$AZURE_CLI_IMAGE"

storage() {
  docker run --rm \
    --user "$(id -u):$(id -g)" \
    --env AZURE_STORAGE_CONNECTION_STRING \
    --env AZURE_CONFIG_DIR=/tmp/azure \
    --env HOME=/tmp \
    --mount "type=bind,source=$BACKUP_DIR,target=/backup" \
    "$AZURE_CLI_IMAGE" az storage "$@" --only-show-errors
}

access=$(storage container show --name db-backups --query properties.publicAccess --output tsv)
if [[ -n "$access" ]]; then
  echo "The db-backups container must have anonymous access disabled." >&2
  exit 1
fi

export PGDATABASE="$PRODUCTION_DATABASE_URL"
docker create --name "$dump_container" \
  --env PGDATABASE --env PGCONNECT_TIMEOUT=15 \
  "$POSTGRES_IMAGE" pg_dump \
  --no-password --format=custom --no-owner --no-acl --file=/tmp/production.dump > /dev/null
docker start --attach "$dump_container" > /dev/null
docker cp "$dump_container:/tmp/production.dump" "$BACKUP_DIR/production.dump"
docker rm "$dump_container" > /dev/null
test -s "$BACKUP_DIR/production.dump"

backup_blob="production/$(date -u +%Y%m%dT%H%M%SZ)-$GITHUB_SHA-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT.dump"
checksum=$(sha256sum "$BACKUP_DIR/production.dump" | cut -d ' ' -f 1)
storage blob upload --container-name db-backups --name "$backup_blob" \
  --file /backup/production.dump --overwrite false \
  --metadata "commit=$GITHUB_SHA" "sha256=$checksum" --output none
storage blob download --container-name db-backups --name "$backup_blob" \
  --file /backup/verified.dump --output none
cmp --silent "$BACKUP_DIR/production.dump" "$BACKUP_DIR/verified.dump"
printf 'blob=%s\n' "$backup_blob" >> "$GITHUB_OUTPUT"

docker run --rm --network none \
  --mount "type=bind,source=$BACKUP_DIR,target=/backup,readonly" \
  "$POSTGRES_IMAGE" pg_restore --list /backup/verified.dump > /dev/null

docker run --detach --rm --network none --name "$restore_container" \
  --env POSTGRES_HOST_AUTH_METHOD=trust --env POSTGRES_DB=restorecheck \
  --mount "type=bind,source=$BACKUP_DIR,target=/backup,readonly" \
  "$POSTGRES_IMAGE" > /dev/null
for ((attempt = 1; attempt <= 30; attempt++)); do
  if docker exec "$restore_container" pg_isready --host=127.0.0.1 --username=postgres --dbname=restorecheck > /dev/null 2>&1; then
    break
  fi
  sleep 2
done
docker exec "$restore_container" pg_isready --host=127.0.0.1 --username=postgres --dbname=restorecheck > /dev/null
docker exec "$restore_container" pg_restore --username=postgres --dbname=restorecheck \
  --no-owner --no-acl --exit-on-error /backup/verified.dump
echo "Backup uploaded, downloaded, byte-verified, and restored into isolated PostgreSQL."
