#I "packages/FAKE.Core/tools/"
#r "FakeLib.dll"

// change to nuget source, or build Fake.Dotnet debug library

// Debug build from visual studio output
#I "../../../src/Fake.Dotnet/bin/Debug/"

// Release build from build script output
// #I "../../../artifacts/build/"

#r "Fake.Dotnet.dll"

open Fake
open Dotnet

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin" ++ "test/*/bin"
        |> DeleteDirs
)

Target "InstallDotnet" (fun _ ->
    DotnetCliInstall id
)

Target "BuildProjects" (fun _ ->
    !! "src/*/project.json" 
        |> Seq.iter(fun proj ->  
            DotnetRestore id proj
            DotnetPack (fun c -> 
                { c with 
                    Configuration = Debug;
                    VersionSuffix = Some "ci-100";
                    OutputPath = Some (currentDirectory @@ "artifacts")
                }) proj
        )
)

Target "Default"                        <| DoNothing

"Clean"
    ==> "InstallDotnet"
    ==> "BuildProjects"
    ==> "Default"

// start build
RunTargetOrDefault "Default"