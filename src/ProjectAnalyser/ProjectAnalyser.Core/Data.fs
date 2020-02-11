namespace ProjectAnalyser.Core.Data

module Neo4J =

    type ProjectType = NotKnown | NetFramework | NetCore
    type DeployedProject = DeployedProject of string
    type CodeProject = CodeProject of name:string * ProjectType
    
    type ProjectNode = 
        | Code of CodeProject
        | Deployed of DeployedProject
    
    type NugetPackageType = Unknown | Framework | Standard
    type NugetLib = NugetLib of name:string * version:string * NugetPackageType
    type RuntimeLib = RuntimeLib of name: string

    type LibraryNode =
        | Nuget of NugetLib
        | Runtime of RuntimeLib
    
    type ResourceNode = ResourceNode of string
    
    type Node = 
        | Project  of ProjectNode
        | Library  of LibraryNode
        | Resource of ResourceNode
        
    type Relationship =
        | ProjectReferencesLibrary         of CodeProject     * LibraryNode
        | ProjectReferencesProject         of CodeProject     * CodeProject
        | DeployedProjectCanTalkToOther    of DeployedProject * DeployedProject
        | DeployedProjectCanTalkToResource of DeployedProject * ResourceNode
        
    type ProjectInfo = { Nodes: Node list; Relationships : Relationship list }
    
module Analyser =

    type AnalyserRun = { FilePath: string; AnalyserName: string }
    type CliLogger = { WriteOut: string -> unit; WriteErr: string -> unit }
    type CliLoggerFactory = { Create: AnalyserRun -> CliLogger }

    type AnalyseError = 
        | Failed of string
        | FailedWithException of System.Exception
    
    type AnalyserFailed = { Id: AnalyserRun; Error: AnalyseError }
    type AnalyserInvocation = { FilePath: string; GetName: string -> string }
    type IAnalyseProject =
        abstract member Run: AnalyserInvocation -> Result<Neo4J.ProjectInfo, AnalyserFailed>

