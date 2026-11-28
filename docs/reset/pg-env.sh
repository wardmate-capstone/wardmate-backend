#!/usr/bin/env bash
# RESET-DATA-1128 — nạp kết nối PostgreSQL của một Container App vào biến PG* mà KHÔNG in mật khẩu.
# Dùng trong Azure Cloud Shell (bash):  source docs/reset/pg-env.sh
#   pg_from_app wardmate-procedure-catalog ProcedureDatabase
#   psql -c 'select 1'            # psql/pg_dump tự đọc PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD
#   pg_clear                      # xóa biến sau khi dùng
RG="${RG:-rg-wardmate}"

pg_clear() { unset PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD PGSSLMODE; }

# Liệt kê tên biến chuỗi kết nối của app (chỉ tên và tên secret, không có giá trị).
pg_names() {
  az containerapp show -g "$RG" -n "$1" \
    --query "properties.template.containers[0].env[?starts_with(name,'ConnectionStrings__')].{name:name,secretRef:secretRef}" -o table
}

pg_from_app() {
  local app="$1" key="ConnectionStrings__$2" ref cs
  pg_clear
  ref=$(az containerapp show -g "$RG" -n "$app" \
    --query "properties.template.containers[0].env[?name=='$key'].secretRef | [0]" -o tsv)
  if [ -n "$ref" ]; then
    cs=$(az containerapp secret show -g "$RG" -n "$app" --secret-name "$ref" --query value -o tsv)
  else
    cs=$(az containerapp show -g "$RG" -n "$app" \
      --query "properties.template.containers[0].env[?name=='$key'].value | [0]" -o tsv)
  fi
  if [ -z "$cs" ]; then echo "Không tìm thấy $key trên $app (xem: pg_names $app)" >&2; return 1; fi
  local part k v
  IFS=';' read -ra parts <<< "$cs"
  for part in "${parts[@]}"; do
    k=$(echo "${part%%=*}" | tr -d ' ' | tr '[:upper:]' '[:lower:]'); v="${part#*=}"
    case "$k" in
      host|server) export PGHOST="$v" ;;
      port) export PGPORT="$v" ;;
      database|db) export PGDATABASE="$v" ;;
      username|userid|user|uid) export PGUSER="$v" ;;
      password|pwd) export PGPASSWORD="$v" ;;
    esac
  done
  export PGSSLMODE=require
  unset cs
  echo "Đã nạp $app/$2 → host=$PGHOST port=${PGPORT:-5432} db=$PGDATABASE user=$PGUSER (mật khẩu không in)"
}
