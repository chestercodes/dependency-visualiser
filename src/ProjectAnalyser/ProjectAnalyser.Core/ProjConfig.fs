namespace ProjectAnalyser.Core

open System
open System.IO
open System.Text.RegularExpressions
open ProjectAnalyser.Core.Data.Neo4J
open ProjectAnalyser.Core.Data.Analyser
open System.Collections.Generic
open Newtonsoft.Json

module ProjConfig =
    
    type ProjConfigError = 
        | FailedToDeserialiseJson of string

    [<CLIMutable>]
    type ConfigPatternsDto = { resources: Dictionary<string, string>; projects: Dictionary<string, string> }
    
    type ConfigRegex = ConfigRegex of regex:string
    type ConfigPatterns = { Resources: Map<ConfigRegex, ResourceNode>; Projects: Map<ConfigRegex, DeployedProject> }
    
    let findReleventFiles projDir =
        let files = [
            //"App.config"
            "Web.config"
            "Web.Release.config"
            "Web.Debug.config"
            "parameters.xml"
            "appSettings.json"
            "appSettings.Production.json"
        ]

        files 
        |> List.map (fun x -> Path.Combine(projDir, x))
        |> List.filter File.Exists

    let scanFile configPatterns fileContents = 
        let returnIfRegexMatches = fun (ConfigRegex regex, node) -> 
            if Regex.IsMatch(fileContents, regex) then
                Some node
            else None

        let projects = configPatterns.Projects |> Map.toList |> List.choose returnIfRegexMatches
        let resources = configPatterns.Resources |> Map.toList |> List.choose returnIfRegexMatches
        (projects, resources)

    let mapToGraph fileConfigs invocation = 
        let projectNode = DeployedProject (invocation.GetName invocation.FilePath)
        let projects = fileConfigs |> List.collect (fun (ps, _) -> ps) |> List.distinct
        let resources = fileConfigs |> List.collect (fun (_, ds) -> ds) |> List.distinct
        
        {
            Nodes = [
                [ projectNode ] @ projects |> List.map (Deployed >> Project)
                resources |> List.map Resource
            ]
            |> List.collect id

            Relationships = [
                projects |> List.map (fun x -> DeployedProjectCanTalkToOther(projectNode, x))
                resources |> List.map (fun x -> DeployedProjectCanTalkToResource(projectNode, x))
            ]
            |> List.collect id
        }

    type ProjectConfig(configPatternContents) =
        interface ProjectAnalyser.Core.Data.Analyser.IAnalyseProject with
            member this.Run invocation =
                
                let tryParseConfigPatternFile contents =
                    try
                        let obj = JsonConvert.DeserializeObject<ConfigPatternsDto>(contents)
                        if isNull obj.resources || isNull obj.projects then
                            raise (Exception ("pattern config file has null resources or projects"))
                        else
                            { 
                                Resources = 
                                    obj.resources.Keys
                                    |> Seq.map (fun k -> (ConfigRegex k, ResourceNode obj.resources.[k]))
                                    |> Map.ofSeq
                                Projects = 
                                    obj.projects.Keys
                                    |> Seq.map (fun k -> 
                                        let name = invocation.GetName obj.projects.[k]
                                        (ConfigRegex k, DeployedProject name)
                                    )
                                    |> Map.ofSeq
                            }
                            |> Ok
                    with
                        | ex -> FailedToDeserialiseJson ex.Message |> Error
                
                tryParseConfigPatternFile configPatternContents
                |> Result.map (fun configPatterns ->
                    let scan fileContents = scanFile configPatterns fileContents 
                    match findReleventFiles (Path.GetDirectoryName invocation.FilePath) with
                    | [] -> { Nodes = []; Relationships = [] } // not a deployed project
                    | configFilePaths ->
                        configFilePaths
                        |> List.map (File.ReadAllText >> scan)
                        |> fun configTups -> mapToGraph configTups invocation
                )
                |> Result.mapError (fun x ->
                    match x with
                    | FailedToDeserialiseJson msg ->
                        { 
                            Id = { FilePath = invocation.FilePath; AnalyserName = "ProjectConfig" }
                            Error = Failed msg
                        }
                )
