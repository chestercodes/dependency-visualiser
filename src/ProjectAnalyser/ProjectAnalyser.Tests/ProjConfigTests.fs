module ProjConfigTests

open Expecto
open ProjectAnalyser.Core.ProjConfig
open ProjectAnalyser.Core.Data.Neo4J

[<Tests>]
let tests =
    
    let appConfig = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <connectionStrings>  
        <add name="MainDatabase"   
            connectionString="Server=myServerAddress;Database=Main ;Trusted_Connection=True;" />  
    </connectionStrings> 
    <appSettings>
        <add key="MainApiHostAndPort" value="api.host:123"/>
    </appSettings>
</configuration>"""
    
    let mainDb = ResourceNode "Main"
    let mainDbRegex = ConfigRegex "Database=Main\\s*;"

    let apiProj = DeployedProject "Api"
    let apiProjRegex = ConfigRegex "api.host:123"
    

    testList "proj config tests" [
        testCase "parses database regex ok" <| fun _ ->
            
            let expected = [], [ mainDb ]
      
            let configPatterns = { Resources = [(mainDbRegex, mainDb)] |> Map.ofList; Projects = [] |> Map.ofList }
      
            let result = scanFile configPatterns appConfig
            Expect.equal result expected "doesnt parse db in app config"
        
        testCase "parses api regex ok" <| fun _ ->
                
            let expected = [ apiProj ], []
            let configPatterns = { Resources = [] |> Map.ofList; Projects = [(apiProjRegex, apiProj)] |> Map.ofList }
      
            let result = scanFile configPatterns appConfig
            Expect.equal result expected "doesnt parse api in app config" 

        testCase "parses both ok" <| fun _ ->
            
            let expected = [ apiProj], [ mainDb ]
            let configPatterns = { Resources = [(mainDbRegex, mainDb)] |> Map.ofList; Projects = [(apiProjRegex, apiProj)] |> Map.ofList }
      
            let result = scanFile configPatterns appConfig
            Expect.equal result expected "doesnt parse both in app config" 
    ]
