# dependency-visualiser


## Running ProjectAnalyser

The console app can be run with the following steps:

- Clone this repo, ensure `dotnet` sdk is installed
- Clone all platform repos into C:/allRepos
- Change ConfigPatterns.json to have appropriate regexes for your platform
- Open a powershell terminal and `cd` to `src/ProjectAnalyser`
- Run `./RunProjectAnalyser.ps1`
- The .csv files should save to `src/Visual/import`

## Running Neo4j

- Ensure docker for windows is installed
- Switch docker to "Experimental" mode
- Go to powershell prompt and `cd` to `src/Visual`
- Run `./RunNeo4j.ps1`

