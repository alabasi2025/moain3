#!/usr/bin/env bash
# Rebuild the full dev environment on a fresh Debian/Ubuntu sandbox (tested: Debian 13). Run from repo root.
set -euo pipefail
SA_PW="${SA_PW:-Hr$(openssl rand -hex 8)!Aa}"; echo "$SA_PW" > /tmp/.sapw; chmod 600 /tmp/.sapw
# 1) SQL Server 2022 + sqlcmd
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor --yes -o /usr/share/keyrings/microsoft-prod.gpg
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/ubuntu/22.04/mssql-server-2022 jammy main" | sudo tee /etc/apt/sources.list.d/mssql.list >/dev/null
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/ubuntu/22.04/prod jammy main" | sudo tee /etc/apt/sources.list.d/msprod.list >/dev/null
sudo apt-get update -qq && sudo DEBIAN_FRONTEND=noninteractive ACCEPT_EULA=Y apt-get install -y -qq mssql-server mssql-tools18 unixodbc-dev
# Debian 13 lacks libldap-2.5 required by sqlservr:
curl -fsSL -o /tmp/ldap.deb http://archive.ubuntu.com/ubuntu/pool/main/o/openldap/libldap-2.5-0_2.5.11+dfsg-1~exp1ubuntu3_amd64.deb && sudo dpkg -i /tmp/ldap.deb
# No systemd in sandbox: set SA password then start manually
sudo -u mssql env MSSQL_SA_PASSWORD="$SA_PW" ACCEPT_EULA=Y MSSQL_PID=Developer timeout 90 /opt/mssql/bin/sqlservr --reset-sa-password || true
sudo -u mssql bash -c 'setsid -f /opt/mssql/bin/sqlservr > /tmp/sqlservr.log 2>&1 < /dev/null'; sleep 30
S="/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $SA_PW -I -b -f 65001"
# 2) Database (ORDER MATTERS; -I is mandatory)
$S -Q "IF DB_ID('HRMS') IS NULL CREATE DATABASE HRMS COLLATE Arabic_100_CI_AS;"
$S -d HRMS -i pack/01_schema.sql && $S -d HRMS -i pack/02_seed.sql && $S -d HRMS -i fixes/06_fixes.sql
$S -d HRMS -Q "IF NOT EXISTS(SELECT 1 FROM hr.Companies WHERE Code='C1') BEGIN INSERT hr.Companies(Code,NameAr) VALUES('C1',N'شركة تجريبية'); DECLARE @id bigint=SCOPE_IDENTITY(); EXEC hr.usp_SeedCompanyDefaults @CompanyId=@id; EXEC hr.usp_ApplyYemenLaborDefaults @CompanyId=@id; END"
$S -d HRMS -i fixes/08_demo_users.sql      # DEMO accounts admin/1 and user/1
# 3) .NET 8
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT=$HOME/.dotnet PATH=$PATH:$HOME/.dotnet
dotnet build Hrms.sln -nologo -v q && dotnet test tests/Hrms.Tests -nologo -v q
# 4) Runtime secrets (env only, never committed)
cat > /tmp/.hrms_env <<ENV
export ConnectionStrings__Hrms='Server=localhost;Database=HRMS;User Id=sa;Password=$SA_PW;TrustServerCertificate=True;Encrypt=False'
export Jwt__Key='$(openssl rand -base64 48)'
export Encryption__Key='$(openssl rand -base64 32)'
export Bootstrap__AdminPassword='1'
ENV
chmod 600 /tmp/.hrms_env
echo "DONE. Start API:  source /tmp/.hrms_env && ASPNETCORE_URLS=http://0.0.0.0:5001 setsid -f dotnet src/Hrms.Api/bin/Debug/net8.0/Hrms.Api.dll > /tmp/api.log 2>&1 < /dev/null"
