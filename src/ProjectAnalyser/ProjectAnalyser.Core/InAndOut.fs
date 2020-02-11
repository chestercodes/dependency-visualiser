namespace ProjectAnalyser.Core

open System
open System.IO
open ProjectAnalyser.Core.Data.Neo4J

module Output =
    
    let getNodeCsvFiles (projectInfo: ProjectInfo) =
        let projects = projectInfo.Nodes |> List.choose (fun x -> match x with | Project (Code p) -> Some p | _ -> None)
        let projectNames = projects |> List.map (fun (CodeProject (n, _)) -> n) |> List.distinct
        let deployedProjects = projectInfo.Nodes |> List.choose (fun x -> match x with | Project (Deployed (DeployedProject p)) -> Some p | _ -> None)
        let resources = projectInfo.Nodes |> List.choose (fun x -> match x with | Resource (ResourceNode d) -> Some d | _ -> None)
        let runtime = projectInfo.Nodes |> List.choose (fun x -> match x with | Library (Runtime (RuntimeLib x)) -> Some x | _ -> None)
        let library = projectInfo.Nodes |> List.choose (fun x -> match x with | Library (Nuget (NugetLib (name, _, _))) -> Some name | _ -> None)
        
        let projects = 
            projectNames 
            |> List.map (fun x ->
                let isPlatform platform =
                    projects
                    |> List.filter (fun (CodeProject (n, t)) -> n = x && t = platform)
                    |> fun x -> x.Length > 0
                    
                let deployed = if deployedProjects |> List.contains x then 1 else 0
                let plat = 
                    match () with
                    | _ when isPlatform (NetFramework) -> "netframework"
                    | _ when isPlatform (NetCore) -> "netcore"
                    | _ -> "unknown"

                sprintf "%s,%i,%s" x deployed plat
            )
            |> List.sort
        let libs = ( library @ runtime ) |> List.distinct |> List.sort
        
        [
            "projects.csv", sprintf "name,deployed,platform\n%s" (String.concat Environment.NewLine projects)
            "resources.csv", sprintf "name\n%s" (String.concat Environment.NewLine (resources |> List.sort))
            "libraries.csv", sprintf "name\n%s" (String.concat Environment.NewLine libs)
        ]
    
    let nugetTypeToStr nugetType = match nugetType with | Unknown -> "unknown" | Standard -> "standard" | Framework -> "framework"
    
    let getRelationshipCsvs (projectInfo: ProjectInfo) =
        let projectReferencesProject = 
            projectInfo.Relationships
            |> List.choose (fun x -> 
                match x with
                | ProjectReferencesProject ((CodeProject (startName, _)), (CodeProject (endName, _))) -> sprintf "%s,%s" startName endName |> Some
                | _ -> None)
        let projectReferencesNugetLibrary = 
            projectInfo.Relationships
            |> List.choose (fun x -> 
                match x with
                | ProjectReferencesLibrary ((CodeProject (project, _)), (Nuget (NugetLib (library, version, build)))) -> 
                    sprintf "%s,%s,%s,%s" project library version (nugetTypeToStr build) |> Some
                | _ -> None)
        let projectReferencesRuntimeLibrary = 
            projectInfo.Relationships
            |> List.choose (fun x -> 
                match x with
                | ProjectReferencesLibrary ((CodeProject (project, _)), (Runtime (RuntimeLib library))) -> sprintf "%s,%s" project library |> Some
                | _ -> None)
        let projectCanTalkToProject = 
            projectInfo.Relationships
            |> List.choose (fun x -> 
                match x with
                | DeployedProjectCanTalkToOther ((DeployedProject s), (DeployedProject e)) -> sprintf "%s,%s" s e |> Some
                | _ -> None)
        let projectCanTalkToResource = 
            projectInfo.Relationships
            |> List.choose (fun x -> 
                match x with
                | DeployedProjectCanTalkToResource ((DeployedProject s), (ResourceNode e)) -> sprintf "%s,%s" s e |> Some
                | _ -> None)

        [
            "project_ref_project.csv", sprintf "start,end\n%s" (String.concat Environment.NewLine projectReferencesProject)
            "project_ref_nugetlib.csv", sprintf "project,library,version,build\n%s" (String.concat Environment.NewLine projectReferencesNugetLibrary)
            "project_ref_runtimelib.csv", sprintf "project,library\n%s" (String.concat Environment.NewLine projectReferencesRuntimeLibrary)
            "project_talk_project.csv", sprintf "start,end\n%s" (String.concat Environment.NewLine projectCanTalkToProject)
            "project_talk_resource.csv", sprintf "project,resource\n%s" (String.concat Environment.NewLine projectCanTalkToResource)
        ]
 
    let getOutputCsvFiles (projectInfo: ProjectInfo) =
        [
            getNodeCsvFiles projectInfo
            getRelationshipCsvs projectInfo
        ]
        |> List.collect id

    let mergeNodes (results: ProjectInfo list) =
        let allNodes = results |> List.collect (fun x -> x.Nodes |> Seq.toList) |> List.distinct
        let allRelationships = results |> List.collect (fun x -> x.Relationships |> Seq.toList)
        { ProjectInfo.Nodes = allNodes; Relationships = allRelationships }
    
    let writeToOutput (outputDir: string) (projectInfo: ProjectInfo) =
        printfn "writing to output"
        let csvOutputs = getOutputCsvFiles projectInfo
        for (name, contents) in csvOutputs do
            let path = Path.Combine(outputDir, name)
            File.WriteAllText(path, contents)
    
        //let cqlPath = Path.Combine(outputDir, "relationships.cql")
        //File.WriteAllLines(cqlPath, getCqlStatements(projectInfo))
    
    let updateNeo4j (outputDir: string) (projectInfo: ProjectInfo) =
        ()
    

module FilePaths =
    open ProjectAnalyser.Core.Data.Analyser

    type PathAndName = { FilePath: string; Name: string }

    let getInvocationFactory absFilePaths =
        let absFilePaths = 
            absFilePaths
            |> List.distinctBy (fun x ->
                // remove SomeProject.csproj and SomeProject.vbproj in same dir, only do first.
                Path.Combine((Path.GetDirectoryName x), Path.GetFileNameWithoutExtension x) |> Path.GetFullPath
            )

        let rec pathsAndParts (todo: string list) (nParts: int) agg =
            match todo with
            | [] -> agg
            | paths ->
                let aggAddition =
                    paths
                    |> List.groupBy (fun x -> 
                        let spl = x.Split '\\'
                        spl
                        |> Array.rev
                        |> Array.mapi (fun i x -> 
                            if i = 0 then Path.GetFileNameWithoutExtension x else x
                        )
                        |> Array.take nParts
                    )
                    |> List.filter (fun (k, v)  -> v.Length = 1)
                    |> List.map (fun (k, v) -> 
                        let name = String.concat "/" (k |> Array.rev)
                        { FilePath = v |> List.head; Name = name }
                    )
                
                let stillTodo = 
                    paths
                    |> List.filter (fun x -> 
                        let uniquePaths = aggAddition |> List.map (fun x -> x.FilePath)
                        uniquePaths |> List.contains x |> not
                    )

                printfn "Deduping %A" stillTodo
                pathsAndParts stillTodo (nParts + 1) (agg @ aggAddition)
        
        let pathsAndNames = pathsAndParts absFilePaths 1 []
        
        fun filePath ->
            { 
                FilePath = filePath
                GetName = fun projName -> 
                    let filePath = 
                        if filePath = projName then
                            // is original path
                            filePath
                        else
                            // is project reference
                            let dir = Path.GetDirectoryName filePath
                            Path.Combine (dir, projName) |> Path.GetFullPath

                    let filePath = Path.GetFullPath filePath
                    let pathAndNameMatch = pathsAndNames |> List.filter (fun x -> x.FilePath.ToLower() = filePath.ToLower())
                    match pathAndNameMatch with
                    | [ single ] -> single.Name
                    | [] -> 
                        printfn "No file names for %s. Might not line up with other names!" filePath
                        Path.GetFileNameWithoutExtension filePath
                    | _ -> raise (Exception (sprintf "Multiple file names for %s" filePath))
            }
    
    let getInvocations (projectPaths: string list) = 
        projectPaths
        |> List.map Path.GetFullPath
        |> getInvocationFactory
        |> fun factory -> projectPaths |> List.map factory

