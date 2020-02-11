namespace ProjectAnalyser.Core

open System
open System.IO
open System.Xml.Linq
open System.Xml.XPath
open System.Text.RegularExpressions
open ProjectAnalyser.Core.Data.Neo4J
open ProjectAnalyser.Core.Data.Analyser

module ProjFile =
    type PackageReference = { Include: string; Version: string option }
    type ProjectReference = { Include: string;  }
    type ProjFileReference = { Include: string; HintPath: string option }
    type DotnetProjFile = { PackageReferences: PackageReference list; ProjectReferences: ProjectReference list }
    type MsBuildProjFile = { References: ProjFileReference list; ProjectReferences: ProjectReference list }
    type ProjFile = | Dotnet of DotnetProjFile | MsBuild of MsBuildProjFile
    type NugetPackage = { PackageName: string; Version: string; Type: NugetPackageType }
    type ProjectRef = { ProjName: string }
    type Reference = 
         | Nuget of NugetPackage
         | Runtime of string
    type ProjectInfo = { References: Reference list; ProjectReferences: ProjectRef list; Type: ProjectType }
    
    let mapProjectToGraph (proj: ProjectInfo) (invocation: AnalyserInvocation) =
        let projectNode = CodeProject ((invocation.GetName invocation.FilePath), proj.Type)
        let projectNodes = proj.ProjectReferences |> List.map (fun x -> CodeProject ((invocation.GetName x.ProjName), NotKnown))
        let runtimeNodes = 
            proj.References
            |> List.choose (fun x -> match x with | Runtime name -> Some (RuntimeLib name) | _ -> None)
        let libraryNodes = 
            proj.References
            |> List.choose (fun x -> 
                match x with 
                | Nuget nuget -> Some (NugetLib (nuget.PackageName, nuget.Version, nuget.Type))
                | _ -> None
            )
        
        { 
            Nodes = List.collect id [
                    [ projectNode |> Code |> Project ]
                    projectNodes |> List.map (Code >> Project)
                    runtimeNodes  |> List.map (LibraryNode.Runtime >> Library)
                    libraryNodes  |> List.map (LibraryNode.Nuget >> Library)
                ]
            Relationships = List.collect id [
                    projectNodes  |> List.map (fun x -> ProjectReferencesProject (projectNode, x))
                    runtimeNodes  |> List.map (fun x -> ProjectReferencesLibrary (projectNode, x |> LibraryNode.Runtime))
                    libraryNodes  |> List.map (fun x -> ProjectReferencesLibrary (projectNode, x |> LibraryNode.Nuget))
                ]
        }

    let transformProject proj =
        let parseTypeFromHintPath (hintPath: string option) =
            match hintPath with
            | Some x when x.Contains("net2") -> Framework
            | Some x when x.Contains("net4") -> Framework
            | Some x when x.Contains("netstandard") -> Standard
            | _ -> Unknown

        let parseFromHintPath (packageName: string) (hintPath: string) =
            let returnNugetWithVersion version =
                Nuget { 
                    PackageName = packageName
                    Version = version
                    Type = parseTypeFromHintPath (Some hintPath) }

            let lastPart = (packageName.Split '.' |> Array.rev |> Array.head)
            let pattern = sprintf ".*%s\\.(?<version>[\\d\\.]+)\\\\.*" lastPart
            let regex = Regex(pattern)
            if regex.IsMatch hintPath then
                let version = (regex.Match hintPath).Groups.["version"].Value
                returnNugetWithVersion version
            else 
                let fourNumbersRegex = Regex("\\.(?<version>\\d\\.\\d\\.\\d\\.\\d)")
                let threeNumbersRegex = Regex("\\.(?<version>\\d\\.\\d\\.\\d)")
                match hintPath with
                | x when fourNumbersRegex.IsMatch(x) ->
                    let version = (fourNumbersRegex.Match hintPath).Groups.["version"].Value
                    returnNugetWithVersion version
                | x when threeNumbersRegex.IsMatch(x) ->
                    let version = (threeNumbersRegex.Match hintPath).Groups.["version"].Value
                    returnNugetWithVersion version
                | _ ->
                    printfn "Failed to parse version %s %s" packageName hintPath
                    Nuget { 
                        PackageName = packageName
                        Version = "-1"
                        Type = parseTypeFromHintPath (Some hintPath) }
                    //raise (Exception("Failed regex"))

        let parseFromInclude (includeParts: string list) hintPath =
            let version = 
                includeParts
                |> List.filter (fun x -> x.Contains("Version="))
                |> List.head
                |> fun (x: string) -> (x.Split '=').[1]
            Nuget {
                PackageName = includeParts.[0]
                Version = version
                Type = parseTypeFromHintPath hintPath }
        
        let parseReference (reference: ProjFileReference) =
            match reference.Include.Split ',', reference.HintPath with
            | [| packageName |], Some hintPath -> parseFromHintPath packageName hintPath
            | [| packageName |], None -> Runtime packageName
            | moreThanOne, hintPath -> parseFromInclude (moreThanOne |> Array.toList) hintPath
        
        let parseProjectReference (projRef: ProjectReference) = { ProjName = projRef.Include }
        
        match proj with
        | Dotnet dotnet -> 
            {
                Type = NetCore
                ProjectInfo.ProjectReferences = 
                    dotnet.ProjectReferences
                    |> List.map parseProjectReference
                References = 
                    dotnet.PackageReferences
                    |> List.map (fun x -> 
                        match x.Version with
                        | Some version -> 
                            Nuget { PackageName = x.Include; Version = version; Type = Standard }
                        | None -> 
                            Runtime x.Include
                    )
            } |> Ok
        | MsBuild msbuild -> 
            {
                Type = NetFramework
                ProjectReferences = msbuild.ProjectReferences |> List.map parseProjectReference
                References = msbuild.References |> List.map parseReference
            } |> Ok

    let parseContents contents =
        let msbuild: XNamespace = XNamespace.Get "http://schemas.microsoft.com/developer/msbuild/2003"
        let includeAttr (x:XElement) = x.Attribute(XName.Get "Include")
        let versionAttr (x:XElement) = x.Attribute(XName.Get "Version")
        let hintPathElement (x:XElement) = x.Element(msbuild + "HintPath")
        
        try
            let doc = XDocument.Parse contents
            
            let parseProjectReferences element =
                match includeAttr element with
                | null -> raise (Exception("No include"))
                | includeAttr -> { Include = includeAttr.Value }

            let parsePackageReferences element =
                let includeVal =
                    match includeAttr element with
                    | null -> raise (Exception("No include"))
                    | includeAttr -> includeAttr.Value
                let versionVal =
                    match versionAttr element with
                    | null -> None
                    | attr -> Some attr.Value
                { Include = includeVal; Version = versionVal }

            let parseReferences element =
                let includeVal =
                    match includeAttr element with
                    | null -> raise (Exception("No include"))
                    | includeAttr -> includeAttr.Value
                let hintPath =
                    match hintPathElement element with
                    | null -> None
                    | elem -> elem.Value |> Some
                { Include = includeVal; HintPath = hintPath }

            let parseDotnetFile() =
                let projectReferences = 
                    doc.XPathSelectElements "//ProjectReference"
                    |> Seq.map parseProjectReferences
                    |> Seq.toList
                let packageReferences = 
                    doc.XPathSelectElements "//PackageReference"
                    |> Seq.map parsePackageReferences
                    |> Seq.toList
                Dotnet { ProjectReferences = projectReferences; PackageReferences = packageReferences }

            let parseMsBuildFile() =
                let itemGroups = doc.Element(msbuild + "Project").Elements(msbuild + "ItemGroup")
                let projectReferences = 
                    itemGroups.Elements(msbuild + "ProjectReference")
                    |> Seq.map parseProjectReferences
                    |> Seq.toList
                let references = 
                    itemGroups.Elements(msbuild + "Reference")
                    |> Seq.map parseReferences
                    |> Seq.toList
                MsBuild { ProjectReferences = projectReferences; References = references }

            match doc.Element(msbuild + "Project") with
            | null -> parseDotnetFile()
            | _ -> parseMsBuildFile()
            |> Ok
        with 
            | _ as ex -> Error (sprintf "%s" ex.Message)
    
    type ProjectReferences() =
        interface ProjectAnalyser.Core.Data.Analyser.IAnalyseProject with
            member this.Run invocation =
                let fileContents = File.ReadAllText invocation.FilePath
                parseContents fileContents
                |> Result.bind transformProject
                |> Result.map (fun x -> mapProjectToGraph x invocation)
                |> Result.mapError (fun x ->
                    { Id = { FilePath = invocation.FilePath; AnalyserName = "ProjectReferences" }; Error = Failed x }
                )