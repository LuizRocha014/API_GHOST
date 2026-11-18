#!/bin/sh
# Backup do banco que roda no Docker (servidor Linux). Grava em deploy/backups e apaga os de mais de MANTER_DIAS dias.
#   sh deploy/backup/backup-docker.sh
# Todo dia às 3h (crontab -e):
#   0 3 * * * cd /opt/agenda/deploy && sh backup/backup-docker.sh >> backups/backup.log 2>&1
# Copie a pasta backups para fora do servidor (rclone para Google Drive/S3, por exemplo): veja docs/deploy.md.
set -eu
cd "$(dirname "$0")/.."
MANTER_DIAS="${MANTER_DIAS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"

docker compose exec -T -e STAMP="$STAMP" db sh -c '
  F="/var/opt/mssql/backup/AgendaGhost-$STAMP.bak"
  /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b \
    -Q "BACKUP DATABASE [AgendaGhost] TO DISK = N'\''$F'\'' WITH INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = N'\''$F'\'' WITH CHECKSUM"
'
echo "$(date '+%F %T') backup AgendaGhost-$STAMP.bak conferido"
find ./backups -name 'AgendaGhost-*.bak' -mtime +"$MANTER_DIAS" -print -delete
