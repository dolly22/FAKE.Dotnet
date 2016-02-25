#I "packages/FAKE.Core/tools/"
#r "FakeLib.dll"

// change to nuget source, or build Fake.Dotnet debug library

// Debug build from visual studio output
#I "../../../src/Fake.Dotnet/bin/Debug/"

// Release build from build script output
// #I "../../../artifacts/build/"

#r "Fake.Dotnet.dll"

open Fake
open Fake.Dnx

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin" ++ "test/*/bin"
        |> DeleteDirs
)

Target "UpgradeDnx" (fun _ ->
    // upgrade to latest
    DnvmUpgrade id    
)

Target "BuildProjects" (fun _ ->
    let sdkVersion = GlobalJsonSdk "global.json"
    tracefn "Using global.json sdk version: %s" sdkVersion

    //set sdk version from global.json
    let setRuntimeOptions options = 
        { options with 
            VersionOrAlias = sdkVersion 
        }

    // set version suffix for project.json version template (1.0.0-*)
    SetDnxVersionSuffix "ci-100"

    !! "src/*/project.json" 
        |> Seq.iter(fun proj ->  

            // restore packages
            DnuRestore (fun o -> 
                { o with 
                    Runtime = o.Runtime |> setRuntimeOptions
                }) proj

            // build (pack) project and generate outputs
            DnuPack (fun o -> 
                { o with 
                    Runtime = o.Runtime |> setRuntimeOptions
                    Configuration = Release
                    OutputPath = Some (currentDirectory @@ "artifacts")
                }) proj
        )
)


Target "Default" <| DoNothing

"Clean"
    ==> "UpgradeDnx"
    ==> "BuildProjects"
    ==> "Default"

// start build
RunTargetOrDefault "Default"