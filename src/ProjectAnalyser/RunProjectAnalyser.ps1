$scriptDir = $PSScriptRoot

$visualDir = Resolve-Path(Join-Path $scriptDir "../Visual")
$importDir = Join-Path $visualDir "import"

[System.IO.Directory]::CreateDirectory($importDir)

$configPatternsPath = Join-Path $scriptDir "ConfigPatterns.json"

$allReposDir = "C:/allRepos"

dotnet run --project "$scriptDir/ProjectAnalyser/ProjectAnalyser.fsproj" `
    --run-path $allReposDir `
    --output-data $importDir `
    --config-patterns $configPatternsPath