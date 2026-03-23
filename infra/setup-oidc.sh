#!/usr/bin/env bash
# setup-oidc.sh — Configure OIDC federated identity for GitHub Actions → Azure
#
# Run once per environment after creating the resource groups.
# Prerequisites:
#   - az login (logged in as a user with Owner on the subscription)
#   - gh auth login (logged in to GitHub CLI)
#
# Usage:
#   ./infra/setup-oidc.sh dev   <subscription-id>   <resource-group>
#   ./infra/setup-oidc.sh test  <subscription-id>   <resource-group>
#   ./infra/setup-oidc.sh prod  <subscription-id>   <resource-group>

set -euo pipefail

ENV="${1:?Usage: $0 <env> <subscription-id> <resource-group>}"
SUBSCRIPTION_ID="${2:?}"
RESOURCE_GROUP="${3:?}"
REPO="DennesTorres/TripsTracker"
APP_NAME="sp-tripstracker-${ENV}"

echo "==> Creating App Registration: ${APP_NAME}"
APP_ID=$(az ad app create --display-name "${APP_NAME}" --query appId -o tsv)
echo "    App ID: ${APP_ID}"

echo "==> Creating Service Principal"
SP_ID=$(az ad sp create --id "${APP_ID}" --query id -o tsv)
echo "    SP Object ID: ${SP_ID}"

echo "==> Assigning Contributor role on resource group"
az role assignment create \
  --assignee "${APP_ID}" \
  --role "Contributor" \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}"

# Map environment to GitHub branch for federated credential
case "${ENV}" in
  dev)  BRANCH="development" ;;
  test) BRANCH="test" ;;
  prod) BRANCH="main" ;;
  *) echo "Unknown env: ${ENV}"; exit 1 ;;
esac

echo "==> Adding federated credential for branch: ${BRANCH}"
az ad app federated-credential create \
  --id "${APP_ID}" \
  --parameters "{
    \"name\": \"github-${BRANCH}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${REPO}:ref:refs/heads/${BRANCH}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

TENANT_ID=$(az account show --query tenantId -o tsv)

echo "==> Storing GitHub Actions secrets for environment: ${ENV}"
gh secret set AZURE_CLIENT_ID     --body "${APP_ID}"          --env "${ENV}"
gh secret set AZURE_TENANT_ID     --body "${TENANT_ID}"       --env "${ENV}"
gh secret set AZURE_SUBSCRIPTION_ID --body "${SUBSCRIPTION_ID}" --env "${ENV}"
gh secret set AZURE_RESOURCE_GROUP  --body "${RESOURCE_GROUP}"  --env "${ENV}"

echo ""
echo "==> Done. GitHub Actions can now authenticate to Azure for environment: ${ENV}"
echo "    Secrets set: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID, AZURE_RESOURCE_GROUP"
echo ""
echo "    After running Bicep deployment, also set:"
echo "      gh secret set SWA_DEPLOYMENT_TOKEN --body <token-from-bicep-output> --env ${ENV}"
