module ProjFileTests

open Expecto
open ProjectAnalyser.Core.ProjFile
open ProjectAnalyser.Core.Data.Neo4J
open ProjectAnalyser.Core.Data.Analyser

let projectName = "proj"

[<Tests>]
let tests =
  testList "proj file parsing tests" [
    testCase "parses framework project file correctly" <| fun _ ->
      let xml = """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <ItemGroup>
        <Reference Include="Newtonsoft.Json, Version=6.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed, processorArchitecture=MSIL">
            <HintPath>..\packages\Newtonsoft.Json.6.0.8\lib\net45\Newtonsoft.Json.dll</HintPath>
        </Reference>
        <Reference Include="Microsoft.CSharp" />
      </ItemGroup>
      <ItemGroup>
        <ProjectReference Include="..\SomeProject\SomeProject.csproj" />
      </ItemGroup>
</Project>"""
      
      let expected = MsBuild { 
        References = [
            {   Include = "Newtonsoft.Json, Version=6.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed, processorArchitecture=MSIL"
                HintPath = Some "..\\packages\\Newtonsoft.Json.6.0.8\\lib\\net45\\Newtonsoft.Json.dll" }
            { Include = "Microsoft.CSharp"; HintPath = None }
        ]
        ProjectReferences = [
            { Include = "..\SomeProject\SomeProject.csproj" }
        ]
        }

      let result = parseContents xml
      Expect.equal result (expected |> Ok) "framework projects not the same"

    testCase "parses dotnet project file correctly" <| fun _ ->
        let xml = """<Project>
        <ItemGroup>
          <PackageReference Include="Newtonsoft.Json" Version="6.0.0.0" />
        </ItemGroup>
        <ItemGroup>
          <ProjectReference Include="..\SomeProject\SomeProject.csproj" />
        </ItemGroup>
</Project>"""
        
        let expected = Dotnet { 
            PackageReferences = [
                { Include = "Newtonsoft.Json"; Version = Some "6.0.0.0" }
            ]
            ProjectReferences = [
                { Include = "..\SomeProject\SomeProject.csproj" }
            ]
            }
        
        let result = parseContents xml

        Expect.equal result (expected |> Ok) "dotnet projects not the same"
    ]


[<Tests>]
let tests2 =
  testList "packages and projects tests" [
    testCase "msbuild project has its references parsed" <| fun _ ->
      let proj = MsBuild { 
        References = [
            {   Include = "Newtonsoft.Json, Version=6.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed, processorArchitecture=MSIL"
                HintPath = Some "..\\packages\\Newtonsoft.Json.6.0.8\\lib\\net45\\Newtonsoft.Json.dll" }
            {   Include = "Newtonsoft.Json"
                HintPath = Some "..\\packages\\Newtonsoft.Json.6.0.8\\lib\\net45\\Newtonsoft.Json.dll" }
            { Include = "Microsoft.CSharp"; HintPath = None }
        ]
        ProjectReferences = [
            { Include = "..\SomeProject\SomeProject.csproj" }
        ]
        }

      let expected = { 
        Type = NetFramework
        References = [
            Reference.Nuget { PackageName = "Newtonsoft.Json"; Version = "6.0.0.0"; Type = Framework }
            Reference.Nuget { PackageName = "Newtonsoft.Json"; Version = "6.0.8"; Type = Framework }
            Reference.Runtime "Microsoft.CSharp"
        ]
        ProjectReferences = [ { ProjName = "..\SomeProject\SomeProject.csproj" } ]
        }
      let result = transformProject proj
      Expect.equal result (expected |> Ok) "framework project transform not the same"

    testCase "dotnet project has project references parsed" <| fun _ ->
        let proj = Dotnet { 
            PackageReferences = [
                { Include = "Newtonsoft.Json"; Version = Some "6.0.0.0" }
            ]
            ProjectReferences = [
                { Include = "..\SomeProject\SomeProject.csproj" }
            ]
            }
        
        let expected = { 
            Type = NetCore
            References = [
                Reference.Nuget { PackageName = "Newtonsoft.Json"; Version = "6.0.0.0"; Type = Standard }
            ]
            ProjectReferences = [ { ProjName = "..\SomeProject\SomeProject.csproj" } ]
            }
        
        let result = transformProject proj
        Expect.equal result (expected |> Ok) "dotnet projects not the same"
    ]

[<Tests>]
let tests3 =
  testList "map tests" [
    testCase "map creates correct nodes and relationships" <| fun _ ->
      let projectInfo = { 
        Type = NetFramework
        References = [
            Reference.Nuget { PackageName = "Newtonsoft.Json"; Version = "6.0.0.0"; Type = Framework }
            Reference.Nuget { PackageName = "AutoMapper"; Version = "6.0.8"; Type = Framework }
            Reference.Runtime "Microsoft.CSharp"
        ]

        ProjectReferences = [ { ProjName = "SomeProject" } ]
        }

      let projectNode = CodeProject (projectName, NetFramework)
      let projectRefNode = CodeProject ("SomeProject", NotKnown)
      let newtonsoftJsonNode = NugetLib ("Newtonsoft.Json", "6.0.0.0", Framework)
      let automapperNode = NugetLib ("AutoMapper", "6.0.8", Framework)
      let cSharpNode = RuntimeLib "Microsoft.CSharp"

      let expected = { 
        Nodes = [
            Project (Code projectNode)
            Project (Code projectRefNode)
            Library (Runtime cSharpNode)
            Library (Nuget newtonsoftJsonNode)
            Library (Nuget automapperNode)
        ]
        Relationships = [
            ProjectReferencesProject (projectNode, projectRefNode)
            ProjectReferencesLibrary (projectNode, Runtime cSharpNode)
            ProjectReferencesLibrary (projectNode, Nuget newtonsoftJsonNode)
            ProjectReferencesLibrary (projectNode, Nuget automapperNode)
        ]
        }
      let invocation = { FilePath = projectName; GetName = fun s -> s }
      let result = mapProjectToGraph projectInfo invocation
      
      Expect.equal result expected "framework project transform not the same"
    ]
