param([string]$Database='clubs',[int]$Port=55432)
$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $workspace
$pg=Join-Path $workspace '.infra/pgsql/bin'
if(!(Test-Path -LiteralPath (Join-Path $pg 'pg_ctl.exe'))){throw 'Install PostgreSQL first, or extract the official Windows binary distribution into .infra/pgsql. See README.'}
& "$pg/pg_ctl.exe" -D .infra/pgdata status | Out-Null
if($LASTEXITCODE -ne 0){& "$pg/pg_ctl.exe" -D .infra/pgdata -l .infra/postgres.log start}
$env:ConnectionStrings__Database="Host=127.0.0.1;Port=$Port;Database=$Database;Username=clubs"
$env:ASPNETCORE_ENVIRONMENT='Development'
& "$pg/createdb.exe" -h 127.0.0.1 -p $Port -U clubs $Database 2>$null
if(!(Test-Path -LiteralPath '.infra/seed-password')){[IO.File]::WriteAllText((Join-Path $workspace '.infra/seed-password'),[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(18)))}
$env:CLUBS_SEED_PASSWORD=Get-Content .infra/seed-password -Raw
dotnet run --project server --no-launch-profile -- --migrate --seed
if($LASTEXITCODE -ne 0){throw 'Migration/seed failed'}
Write-Host 'Database ready. Start the API and frontend in separate terminals using README commands.'
