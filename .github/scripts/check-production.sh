#!/usr/bin/env bash
# A missing production environment or missing required reviewer must stop the deployment.
set -euo pipefail
: "${GH_TOKEN:?Missing job token}"
: "${GITHUB_REPOSITORY:?Missing repository}"
: "${GITHUB_SHA:?Missing commit}"
[[ ${GITHUB_REF:-} == refs/heads/main ]] || { echo 'Only main can deploy.' >&2; exit 1; }

protected=$(gh api "repos/$GITHUB_REPOSITORY/environments/production" \
  --jq '[.protection_rules[]? | select(.type == "required_reviewers") | .reviewers[]?] | length')
(( protected > 0 )) || { echo 'Configure a required reviewer on production before enabling deployment.' >&2; exit 1; }

current=$(gh api "repos/$GITHUB_REPOSITORY/git/ref/heads/main" --jq '.object.sha')
[[ $current == "$GITHUB_SHA" ]] || { echo 'This approved build is no longer the current main commit; deploy the newer run.' >&2; exit 1; }
