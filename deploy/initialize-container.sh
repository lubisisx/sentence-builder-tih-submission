#!/bin/bash
set -eu
# Explicit opt-in job only. The API never runs this script.
/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -C -b -i /setup/create-container-database.sql
/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -C -b -d TIH_SentenceBuilder_Dev -i /scripts/initial-persistence.sql
/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -C -b -d TIH_SentenceBuilder_Dev -i /scripts/seed-vocabulary.sql
