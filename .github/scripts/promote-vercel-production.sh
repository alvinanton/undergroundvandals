#!/usr/bin/env bash
set -euo pipefail

: "${VERCEL_TOKEN:?Missing Vercel access token}"
: "${GITHUB_SHA:?Missing release commit SHA}"
: "${GITHUB_OUTPUT:?Missing GitHub step output file}"

readonly project_name="undergroundvandals"
readonly vercel_cli_version="65.16.0"
deployment_url=""

for ((attempt = 1; attempt <= 20; attempt++)); do
  deployments_json=$(npx --yes "vercel@$vercel_cli_version" list "$project_name" \
    --environment production \
    --status READY \
    --meta "githubCommitSha=$GITHUB_SHA" \
    --limit 100 \
    --json \
    --token "$VERCEL_TOKEN" \
    --non-interactive)

  deployment_url=$(printf '%s' "$deployments_json" | node -e '
    const payload = JSON.parse(require("node:fs").readFileSync(0, "utf8"));
    const deployments = Array.isArray(payload)
      ? payload
      : Array.isArray(payload.deployments)
        ? payload.deployments
        : Array.isArray(payload.data)
          ? payload.data
          : [];
    const sha = process.env.GITHUB_SHA;
    const candidates = deployments.filter((deployment) => {
      if (typeof deployment.url !== "string") return false;
      if (deployment.target && deployment.target !== "production") return false;
      const reportedSha = deployment.meta?.githubCommitSha ?? deployment.gitSource?.sha;
      return !reportedSha || reportedSha === sha;
    });
    candidates.sort((a, b) => Number(b.createdAt ?? 0) - Number(a.createdAt ?? 0));
    if (candidates[0]) process.stdout.write(candidates[0].url);
  ')

  if [[ -n "$deployment_url" ]]; then
    break
  fi

  if (( attempt < 20 )); then
    echo "No ready Production deployment found for $GITHUB_SHA (attempt $attempt/20); retrying in 15 seconds."
    sleep 15
  fi
done

if [[ -z "$deployment_url" ]]; then
  echo "No ready Vercel Production deployment matched release commit $GITHUB_SHA." >&2
  exit 1
fi

if [[ "$deployment_url" != https://* ]]; then
  deployment_url="https://$deployment_url"
fi

deployment_host=${deployment_url#https://}
deployment_host=${deployment_host%%/*}
if [[ "$deployment_host" != *.vercel.app ]]; then
  echo "The matched deployment did not have a Vercel deployment hostname." >&2
  exit 1
fi

echo "Promoting the ready Vercel deployment for commit $GITHUB_SHA."
npx --yes "vercel@$vercel_cli_version" promote "$deployment_url" \
  --yes --token "$VERCEL_TOKEN" --non-interactive

printf 'deployment_url=%s\n' "$deployment_url" >> "$GITHUB_OUTPUT"
echo "Promoted Vercel deployment: $deployment_url"
