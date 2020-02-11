module InAndOutTests

open Expecto
open ProjectAnalyser.Core.ProjConfig
open ProjectAnalyser.Core.FilePaths
open System.IO

[<Tests>]
let tests =
    let baseDir = "C:\\base\\dir"
    let combine parts = Path.Combine(baseDir, (String.concat "\\" parts)) |> Path.GetFullPath

    testList "file paths tests" [
        testCase "gets file names for distinct names" <| fun _ ->
            
            let api1Abs = combine ["Api1"; "Api1.csproj"]
            let api2Abs = combine ["Api2"; "Api2.csproj"]
            let api3Abs = combine ["Api3"; "Api3.csproj"]
            
            let factory = getInvocationFactory [ api1Abs; api2Abs; api3Abs ]
            let api1Inv = factory api1Abs
            let api2Inv = factory api2Abs
            let api3Inv = factory api3Abs
            
            Expect.equal (api1Inv.GetName api1Abs)                 "Api1" "unique - api1 - abs api1 failed"
            Expect.equal (api1Inv.GetName api2Abs)                 "Api2" "unique - api1 - abs api2 failed"
            Expect.equal (api1Inv.GetName api3Abs)                 "Api3" "unique - api1 - abs api3 failed"
            Expect.equal (api1Inv.GetName "..\\Api2\\Api2.csproj") "Api2" "unique - api1 - rel api2 failed"
            Expect.equal (api1Inv.GetName "..\\Api3\\Api3.csproj") "Api3" "unique - api1 - rel api3 failed"

            Expect.equal (api2Inv.GetName api1Abs)                 "Api1" "unique - api2 - abs api1 failed"
            Expect.equal (api2Inv.GetName api2Abs)                 "Api2" "unique - api2 - abs api2 failed"
            Expect.equal (api2Inv.GetName api3Abs)                 "Api3" "unique - api2 - abs api3 failed"
            Expect.equal (api2Inv.GetName "..\\Api1\\Api1.csproj") "Api1" "unique - api2 - rel api1 failed"
            Expect.equal (api2Inv.GetName "..\\Api3\\Api3.csproj") "Api3" "unique - api2 - rel api3 failed"
        
        testCase "gets file names for same names" <| fun _ ->
            
            let api1Abs = combine ["Api1"; "Api.csproj"]
            let api2Abs = combine ["Api2"; "Api.csproj"]
            let api3Abs = combine ["Api3"; "Api.csproj"]
            
            let factory = getInvocationFactory [ api1Abs; api2Abs; api3Abs ]
            let api1Inv = factory api1Abs
            let api2Inv = factory api2Abs
            let api3Inv = factory api3Abs
            
            Expect.equal (api1Inv.GetName api1Abs)                "Api1/Api" "same - api1 - abs api1 failed"
            Expect.equal (api1Inv.GetName api2Abs)                "Api2/Api" "same - api1 - abs api2 failed"
            Expect.equal (api1Inv.GetName api3Abs)                "Api3/Api" "same - api1 - abs api3 failed"
            Expect.equal (api1Inv.GetName "..\\Api2\\Api.csproj") "Api2/Api" "same - api1 - rel api2 failed"
            Expect.equal (api1Inv.GetName "..\\Api3\\Api.csproj") "Api3/Api" "same - api1 - rel api3 failed"

            Expect.equal (api2Inv.GetName api1Abs)                "Api1/Api" "same - api2 - abs api1 failed"
            Expect.equal (api2Inv.GetName api2Abs)                "Api2/Api" "same - api2 - abs api2 failed"
            Expect.equal (api2Inv.GetName api3Abs)                "Api3/Api" "same - api2 - abs api3 failed"
            Expect.equal (api2Inv.GetName "..\\Api1\\Api.csproj") "Api1/Api" "same - api2 - rel api1 failed"
            Expect.equal (api2Inv.GetName "..\\Api3\\Api.csproj") "Api3/Api" "same - api2 - rel api3 failed"

        testCase "gets file names for same names different directories" <| fun _ ->
            
            let api1Abs = combine ["Api1"; "Api"; "Api.csproj"]
            let api2Abs = combine ["Sub"; "Api2"; "Other"; "src"; "Api"; "Api.csproj"]
            let api3Abs = combine ["src"; "Api3"; "Api"; "Api.csproj"]
            
            let factory = getInvocationFactory [ api1Abs; api2Abs; api3Abs ]
            let api1Inv = factory api1Abs
            let api2Inv = factory api2Abs
            let api3Inv = factory api3Abs
            
            let api1 = "Api1/Api/Api"
            let api2 = "src/Api/Api"
            let api3 = "Api3/Api/Api"

            Expect.equal (api1Inv.GetName api1Abs)                                          api1 "same diff dir - api1 - abs api1 failed"
            Expect.equal (api1Inv.GetName api2Abs)                                          api2 "same diff dir - api1 - abs api2 failed"
            Expect.equal (api1Inv.GetName api3Abs)                                          api3 "same diff dir - api1 - abs api3 failed"
            Expect.equal (api1Inv.GetName "..\\..\\Sub\\Api2\\Other\\src\\Api\\Api.csproj") api2 "same diff dir - api1 - rel api2 failed"
            Expect.equal (api1Inv.GetName "..\\..\\src\\Api3\\Api\\Api.csproj")             api3 "same diff dir - api1 - rel api3 failed"
            Expect.equal (api2Inv.GetName api1Abs)                                          api1 "same diff dir - api2 - abs api1 failed"
            Expect.equal (api2Inv.GetName api2Abs)                                          api2 "same diff dir - api2 - abs api2 failed"
            Expect.equal (api2Inv.GetName api3Abs)                                          api3 "same diff dir - api2 - abs api3 failed"
            Expect.equal (api2Inv.GetName "..\\..\\..\\..\\..\\Api1\\Api\\Api.csproj")      api1 "same diff dir - api2 - rel api1 failed"
            Expect.equal (api2Inv.GetName "..\\..\\..\\..\\..\\src\\Api3\\Api\\Api.csproj") api3 "same diff dir - api2 - rel api3 failed"

    ]
