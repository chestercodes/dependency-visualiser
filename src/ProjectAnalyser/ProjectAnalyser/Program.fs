open System
open Argu
open ProjectAnalyser.Core.Data.Analyser
open ProjectAnalyser.Core.Output
open ProjectAnalyser.Core.FilePaths
open System.IO

type CliError =
    | ArgumentsNotSpecified
    | CouldNotGetFiles of pattern:string
    | AnAnalysisFailed

type CmdArgs =
    | [<AltCommandLine("-p")>] Run_Path of path:string
    | [<AltCommandLine("-o")>] Output_Data of path:string
    //| [<AltCommandLine("-n")>] Neo4j of url:string
    | [<AltCommandLine("-c")>] Config_Patterns of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Run_Path _ -> "Path of file or directory to search"
            | Config_Patterns _ -> "Config patterns file path"
            | Output_Data _ -> "Write csv files to directory"
            //| Neo4j _ -> "Instance url"
            
let getExitCode result =
    match result with
    | Ok () -> 0
    | Error err ->
        match err with
        | ArgumentsNotSpecified -> 1
        | CouldNotGetFiles _ -> 2
        | AnAnalysisFailed _ -> 3

let run configPatternsContents pathOpt outputOpt neo4jOpt  =
    let loggerFactory analyserRun =
        { 
            CliLogger.WriteOut = fun x -> Console.WriteLine(x)
            WriteErr = fun x -> Console.WriteLine(x)
        }

    let projectConfig = ProjectAnalyser.Core.ProjConfig.ProjectConfig(configPatternsContents)
    let projectReferences = ProjectAnalyser.Core.ProjFile.ProjectReferences()
    
    let analysers: ProjectAnalyser.Core.Data.Analyser.IAnalyseProject list = [
        projectConfig
        projectReferences
    ]

    let projectFilesInDirectory folder = 
        let csProjFiles = System.IO.Directory.GetFiles(folder, "*.csproj", IO.SearchOption.AllDirectories) |> Array.toList
        let vbProjFiles = System.IO.Directory.GetFiles(folder, "*.vbproj", IO.SearchOption.AllDirectories) |> Array.toList
        //let fsProjFiles = System.IO.Directory.GetFiles(folder, "*.fsproj", IO.SearchOption.AllDirectories) |> Array.toList
        //let packageJsonFiles = System.IO.Directory.GetFiles(folder, "package.json", IO.SearchOption.AllDirectories) |> Array.toList
        csProjFiles @ vbProjFiles //@ fsProjFiles @ packageJsonFiles
        |> List.filter (fun x -> x.Contains("Test") |> not)
    
    let defaultPaths() = projectFilesInDirectory "."
    
    let getFromPath (pathDirOrPattern: string) =
        match pathDirOrPattern with
        | x when x.Contains('*') -> 
            System.IO.Directory.GetFiles(x) |> Array.toList |> Ok
        | x when IO.File.Exists(x) -> 
            [ x ] |> Ok
        | x when IO.Directory.Exists(x) -> 
            projectFilesInDirectory x |> Ok
        | _ -> CouldNotGetFiles pathDirOrPattern |> Error
        
    let runProjects (invocations: AnalyserInvocation list) =
        analysers
        |> List.collect (fun analyser -> 
            invocations
            |> List.map (fun invocation -> 
                async {
                    let result = analyser.Run invocation
                    return result
                }
            )
        )
        |> Async.Parallel
        |> Async.RunSynchronously 

    match pathOpt with
    | Some pathDirOrPattern -> 
        getFromPath pathDirOrPattern
    | None -> defaultPaths() |> Ok
    |> Result.bind (fun projectPaths -> 
        let invocations = getInvocations projectPaths
        runProjects invocations
        |> (fun results -> 
            match results |> Array.filter (fun x -> match x with | Error _ -> true | _ -> false) with
            | [||] -> 
                let analysisResults = results |> Array.choose (fun x -> match x with | Ok analysisResult -> Some analysisResult | Error _ -> None)
                let mergedNodes = mergeNodes (analysisResults |> Array.toList)
                match outputOpt, neo4jOpt with
                | None, None -> 
                    printfn "no output mode selected"
                    ()
                | Some output, Some neo4j -> 
                    writeToOutput output mergedNodes
                    updateNeo4j neo4j mergedNodes
                | Some output, None -> 
                    writeToOutput output mergedNodes
                | None, Some neo4j -> 
                    updateNeo4j neo4j mergedNodes
                Ok ()
            | failedRuns -> 
                let failedRuns = failedRuns |> Array.choose (fun x -> match x with | Ok _ -> None | Error err -> Some err )
                let first = failedRuns |> Array.head
                match first.Error with
                | Failed msg -> printfn "Failed because of - %s" msg
                | FailedWithException ex -> printfn "Failed because of exn - %s" ex.Message
                Error AnAnalysisFailed
        )
    )


[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CmdArgs>(programName = "project-analyser", errorHandler = errorHandler)
    
    match parser.ParseCommandLine argv with
    | p when p.Contains(Output_Data) -> // || p.Contains(Neo4j) -> 
        let outputOpt = if p.Contains(Output_Data) then p.GetResult(Output_Data) |> Some else None
        let neo4jOpt = (* if p.Contains(Neo4j) then p.GetResult(Neo4j) |> Some else *) None
        let pathOpt = if p.Contains(Run_Path) then Some (p.GetResult(Run_Path)) else None
        
        let configPatternsContents = 
            if p.Contains(Config_Patterns) then 
                File.ReadAllText(p.GetResult(Config_Patterns))
            else """{ "resources": {}, "projects": {} }"""
        
        run configPatternsContents pathOpt outputOpt neo4jOpt
    | _ ->
        printfn "%s" (parser.PrintUsage())
        Error ArgumentsNotSpecified
    |> getExitCode