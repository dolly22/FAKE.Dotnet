#I "packages/FAKE.Core/tools/"
#r "FakeLib.dll"

// change to nuget source, or build Fake.Dotnet debug library

// Debug build from visual studio output
#I "../../../src/Fake.Dotnet/bin/Debug/"

// Release build from build script output
// #I "../../../artifacts/build/"

#r "Fake.Dotnet.dll"

open Fake
open Fake.Dotnet

let solutionFile = "NetCoreSdk101.sln"

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin" ++ "test/*/bin"
        |> DeleteDirs
)

Target "InstallDotnet" (fun _ ->
    // install .NET Core SDK 1.0.1
    DotnetSdkInstall SdkVersions.NetCore101
)

Target "RestorePackages" (fun _ ->
    DotnetRestore id solutionFile
)

Target "BuildSolution" (fun _ ->
    // build solution and create nuget packages
    DotnetPack (fun c -> 
        { c with 
            Configuration = Debug;
            VersionSuffix = Some "ci-100";
            OutputPath = Some (currentDirectory @@ "artifacts")
        }) solutionFile
)

Target "Default" <| DoNothing

"Clean"
    ==> "InstallDotnet"
    ==> "RestorePackages"
    ==> "BuildSolution"
    ==> "Default"

// start build
RunTargetOrDefault "Default"