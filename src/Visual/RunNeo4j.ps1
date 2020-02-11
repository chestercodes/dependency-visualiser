$scriptDir = $PSScriptRoot

$importDir = Join-Path $scriptDir "import"
$dataDir = Join-Path $scriptDir "data"
$logsDir = Join-Path $scriptDir "logs"

[System.IO.Directory]::CreateDirectory($importDir)
[System.IO.Directory]::CreateDirectory($dataDir)
[System.IO.Directory]::CreateDirectory($logsDir)

Write-host "Needs to be docker for windows windows containers with experimental features enabled
If the call below fails try running:

    docker-compose down

and running this script again."

docker-compose up