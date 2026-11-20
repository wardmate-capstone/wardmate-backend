#!/usr/bin/env bash
# WardMate - dung lai ha tang tren subscription Azure moi tu ban xuat cau hinh 20/11/2026.
# Chay theo tung buoc: bash wm-rebuild.sh <infra|build|apps|verify|budget>
# Khong in gia tri secret. State (ten tai nguyen, JWT moi) luu o ~/wm-state.env (chmod 600).
# Buoc budget doc email nhan canh bao tu bien moi truong BUDGET_EMAILS (cach nhau dau phay) do nguoi dung tu dat.
set -uo pipefail

SUB=e3691990-ce97-42fd-ad8b-846dcf7dc4ef
LOC=${LOC:-japaneast}
RG=rg-wardmate
ENVN=cae-wardmate
OLD=blackmeadow-a2f12767.japaneast.azurecontainerapps.io
EXP=$HOME/wardmate-export-20261120
SRC=$HOME/wm-src/wardmate-backend-kha
STATE=$HOME/wm-state.env
TAG=rebuild-$(TZ=Asia/Ho_Chi_Minh date +%Y%m%d)
APPS_ORDER="wardmate-gotenberg wardmate-iam wardmate-procedure-catalog wardmate-documentform wardmate-aiocr wardmate-application-workflow wardmate-analytics-system wardmate-gateway"

touch "$STATE"; chmod 600 "$STATE"; source "$STATE"
save(){ grep -v "^$1=" "$STATE" > "$STATE.tmp"; printf '%s=%q\n' "$1" "$2" >> "$STATE.tmp"; mv "$STATE.tmp" "$STATE"; chmod 600 "$STATE"; }
log(){ echo "### $*"; }

az account set --subscription "$SUB" || { echo "Khong chon duoc subscription $SUB"; exit 1; }
if [ -z "${SUFFIX:-}" ]; then SUFFIX=$(tr -dc 'a-z0-9' </dev/urandom | head -c 4); save SUFFIX "$SUFFIX"; fi
ACR=acrwardmate$SUFFIX
ST=stwardmate$SUFFIX

declare -A DF REPO
DF[wardmate-iam]=src/Services/WardMate.Services.IAM/WardMate.Services.IAM.API/Dockerfile; REPO[wardmate-iam]=wardmate-iam
DF[wardmate-gateway]=src/Gateways/WardMate.YarpGateway/Dockerfile; REPO[wardmate-gateway]=wardmate-gateway
DF[wardmate-procedure-catalog]=src/Services/WardMate.Services.ProcedureCatalog/WardMate.Services.ProcedureCatalog.API/Dockerfile; REPO[wardmate-procedure-catalog]=wardmate-procedure-catalog
DF[wardmate-documentform]=src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API/Dockerfile; REPO[wardmate-documentform]=wardmate-documentform
DF[wardmate-aiocr]=src/Services/WardMate.Services.AIOCR/WardMate.Services.AIOCR.API/Dockerfile; REPO[wardmate-aiocr]=wardmate-aiocr
DF[wardmate-application-workflow]=src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API/Dockerfile; REPO[wardmate-application-workflow]=wardmate-application-workflow
DF[wardmate-analytics-system]=src/Services/WardMate.Services.AnalyticsSystem/WardMate.Services.AnalyticsSystem.API/Dockerfile; REPO[wardmate-analytics-system]=wardmate-analyticssystem

infra(){
  log "Dang ky provider"
  for p in Microsoft.App Microsoft.ContainerRegistry Microsoft.Storage Microsoft.OperationalInsights Microsoft.Consumption; do az provider register -n $p --wait -o none; echo "$p: $(az provider show -n $p --query registrationState -o tsv)"; done
  log "Resource group $RG ($LOC)"; az group create -n $RG -l $LOC -o none || return 1
  log "ACR $ACR (Basic)"; az acr create -n $ACR -g $RG --sku Basic --admin-enabled true -o none || return 1
  log "Storage $ST"; az storage account create -n $ST -g $RG -l $LOC --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --allow-blob-public-access false -o none || return 1
  local cs; cs=$(az storage account show-connection-string -n $ST -g $RG --query connectionString -o tsv)
  for c in procedure-sources form-templates workflow-leave-documents wardmate-backups wardmate-files; do az storage container create -n $c --connection-string "$cs" -o none && echo "container $c"; done
  log "Container Apps environment $ENVN (khong Log Analytics)"; az containerapp env create -n $ENVN -g $RG -l $LOC --logs-destination none -o none || return 1
  NEW=$(az containerapp env show -n $ENVN -g $RG --query properties.defaultDomain -o tsv); save NEW "$NEW"; echo "domain moi: $NEW"
}

build(){
  [ -n "${NEW:-}" ] || { echo "Chua co domain moi, chay infra truoc"; return 1; }
  log "Tai code kha"; rm -rf "$HOME/wm-src" && mkdir -p "$HOME/wm-src" && curl -sL https://codeload.github.com/wardmate-capstone/wardmate-backend/tar.gz/refs/heads/kha | tar xz -C "$HOME/wm-src" || return 1
  sed -i "s/$OLD/$NEW/g" "$SRC/src/Gateways/WardMate.YarpGateway/appsettings.Production.json"
  echo "gateway: con $(grep -c "$OLD" $SRC/src/Gateways/WardMate.YarpGateway/appsettings.Production.json) dia chi cu, $(grep -c "$NEW" $SRC/src/Gateways/WardMate.YarpGateway/appsettings.Production.json) dia chi moi"
  local ok=0
  for a in "${!DF[@]}"; do
    if [ -n "${ONLY:-}" ] && [ "$ONLY" != "$a" ]; then continue; fi
    local s; s=$(cd "$SRC" && az acr build -r $ACR -t "${REPO[$a]}:$TAG" -f "${DF[$a]}" . --no-logs --query status -o tsv 2>/dev/null | tail -n 1)
    echo "build $a: $s"; [ "$s" = Succeeded ] || ok=1
  done
  return $ok
}

envlist(){ # KEY=VALUE noi bo, khong in ra man hinh
  jq -r --arg old "$OLD" --arg new "$NEW" '.properties.template.containers[0].env[]? | select(.secretRef==null and .value!=null) | "\(.name)=\(.value|gsub($old;$new))"' "$EXP/apps/$1.json" \
    | grep -v -E '^(AzureBlob__ConnectionString|Jwt__Key|RabbitMq__Password|Email__Password|ConnectionStrings__DocumentDb)='
}
inline(){ jq -r --arg n "$2" '.properties.template.containers[0].env[]? | select(.name==$n) | .value // empty' "$EXP/apps/$1.json"; }

apps(){
  [ -n "${NEW:-}" ] || { echo "Chua co domain moi"; return 1; }
  if [ -z "${JWT:-}" ]; then JWT=$(openssl rand -base64 48 | tr -d '\n'); save JWT "$JWT"; echo "Da tao JWT key moi"; fi
  if [ -z "${RMQ:-}" ]; then read -r -s -p "Dan mat khau RabbitMQ (CloudAMQP) roi Enter: " RMQ; echo; save RMQ "$RMQ"; fi
  local BLOB EMAILPW DOCDB ACRU ACRP
  BLOB=$(az storage account show-connection-string -n $ST -g $RG --query connectionString -o tsv)
  EMAILPW=$(inline wardmate-iam Email__Password)
  DOCDB=$(inline wardmate-application-workflow ConnectionStrings__WorkflowDatabase)
  ACRU=$(az acr credential show -n $ACR --query username -o tsv); ACRP=$(az acr credential show -n $ACR --query 'passwords[0].value' -o tsv)
  echo "kiem tra do dai: mailpw=${#EMAILPW} docdb=${#DOCDB} blob=${#BLOB} jwt=${#JWT} rmq=${#RMQ}"
  for a in $APPS_ORDER; do
    if [ -n "${ONLY:-}" ] && [ "$ONLY" != "$a" ]; then continue; fi
    local f="$EXP/apps/$a.json" port cpu=0.25 mem=0.5Gi min=0 max=2 img
    port=$(jq -r '.properties.configuration.ingress.targetPort' "$f")
    local -a envs=() secs=() reg=()
    mapfile -t envs < <(envlist $a)
    for r in $(jq -r '.properties.template.containers[0].env[]? | select(.secretRef!=null) | "\(.name):\(.secretRef)"' "$f"); do
      local n=${r%%:*} s=${r#*:}
      case "$s" in
        jwt-key|iam-jwt-key) secs+=("jwt-key=$JWT"); envs+=("$n=secretref:jwt-key");;
        rabbitmq-password) secs+=("rabbitmq-password=$RMQ"); envs+=("$n=secretref:rabbitmq-password");;
        email-password) secs+=("email-password=$EMAILPW"); envs+=("$n=secretref:email-password");;
        azure-blob-connection*|blob-connection-string) secs+=("blob-connection=$BLOB"); envs+=("$n=secretref:blob-connection");;
        document-db-connection) secs+=("document-db-connection=$DOCDB"); envs+=("$n=secretref:document-db-connection");;
        *) echo "!! $a: secret $s chua co cach map";;
      esac
    done
    if [ "$a" = wardmate-documentform ]; then
      secs+=("blob-connection=$BLOB"); envs+=("AzureBlob__ConnectionString=secretref:blob-connection")
      envs+=("QrCode__VerificationBaseUrl=https://wardmate-gateway.$NEW/api/v1/public/applications/")
    fi
    if [ "$a" = wardmate-gotenberg ]; then img=docker.io/gotenberg/gotenberg:8; cpu=0.5; mem=1Gi
    else img=$ACR.azurecr.io/${REPO[$a]}:$TAG; reg=(--registry-server $ACR.azurecr.io --registry-username "$ACRU" --registry-password "$ACRP"); fi
    [ "$a" = wardmate-analytics-system ] && min=1
    [ ${#secs[@]} -gt 0 ] && mapfile -t secs < <(printf '%s\n' "${secs[@]}" | awk -F= '!seen[$1]++')
    local -a secarg=(); [ ${#secs[@]} -gt 0 ] && secarg=(--secrets "${secs[@]}")
    local -a envarg=(); [ ${#envs[@]} -gt 0 ] && envarg=(--env-vars "${envs[@]}")
    if az containerapp show -n $a -g $RG -o none 2>/dev/null; then
      [ ${#secs[@]} -gt 0 ] && az containerapp secret set -n $a -g $RG "${secarg[@]:1}" -o none
      az containerapp update -n $a -g $RG --image "$img" --cpu $cpu --memory $mem --min-replicas $min --max-replicas $max --replace-env-vars "${envs[@]}" -o none 2>&1 | grep -v -i warning
      echo "update $a: $(az containerapp show -n $a -g $RG --query properties.provisioningState -o tsv)"
    else
      az containerapp create -n $a -g $RG --environment $ENVN --image "$img" "${reg[@]}" --target-port "$port" --ingress external \
        --cpu $cpu --memory $mem --min-replicas $min --max-replicas $max "${secarg[@]}" "${envarg[@]}" -o none 2>&1 | grep -v -i warning
      echo "create $a: $(az containerapp show -n $a -g $RG --query properties.provisioningState -o tsv 2>/dev/null) env=${#envs[@]} secrets=${#secs[@]}"
    fi
  done
}

verify(){
  for a in $APPS_ORDER; do
    local f p c; f=$(az containerapp show -n $a -g $RG --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null)
    case $a in wardmate-gateway|wardmate-gotenberg) p=/health;; *) p=/health/ready;; esac
    for i in 1 2 3 4 5 6 7 8; do c=$(curl -s -m 40 -o /dev/null -w '%{http_code}' "https://$f$p"); [ "$c" = 200 ] && break; sleep 15; done
    echo "$a $p -> $c ($f)"
  done
  local G="https://wardmate-gateway.$NEW"
  for p in /api/v1/procedures /api/v1/system/app-config /api/v1/citizen/submissions /api/v1/ai/chat/sessions /api/v1/manager/applications; do echo "gateway $p -> $(curl -s -m 40 -o /dev/null -w '%{http_code}' $G$p)"; done
  echo "GATEWAY URL: $G"
}

budget(){
  [ -n "${BUDGET_EMAILS:-}" ] || { echo "Dat BUDGET_EMAILS truoc (vd: export BUDGET_EMAILS=a@x,b@y)"; return 1; }
  local id em; id=$(az account show --query id -o tsv); em=$(jq -cn --arg s "$BUDGET_EMAILS" '$s|split(",")')
  local body; body=$(jq -n --arg start "$(date +%Y-%m-01)T00:00:00Z" --argjson em "$em" '{properties:{category:"Cost",amount:80,timeGrain:"Monthly",timePeriod:{startDate:$start,endDate:"2027-12-31T00:00:00Z"},notifications:{
    at50:{enabled:true,operator:"GreaterThanOrEqualTo",threshold:62.5,thresholdType:"Actual",contactEmails:$em},
    at80:{enabled:true,operator:"GreaterThanOrEqualTo",threshold:100,thresholdType:"Actual",contactEmails:$em},
    fc80:{enabled:true,operator:"GreaterThanOrEqualTo",threshold:100,thresholdType:"Forecasted",contactEmails:$em}}}}')
  az rest --method put --url "https://management.azure.com/subscriptions/$id/providers/Microsoft.Consumption/budgets/wardmate-monthly?api-version=2023-05-01" --body "$body" --query "{name:name, amount:properties.amount}" -o tsv
}

case "${1:-}" in
  infra) infra;; build) build;; apps) apps;; verify) verify;; budget) budget;;
  *) echo "Dung: bash wm-rebuild.sh <infra|build|apps|verify|budget>";;
esac
