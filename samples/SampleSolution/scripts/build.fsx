#I "packages/FAKE.Core/tools/"
#r "FakeLib.dll"

// change to nuget source, or build FAKE.Dotnet debug library
#r "../../../src/fakedotnet/bin/debug/fakedotnet.dll"

open Fake
open Dotnet

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin" ++ "test/*/bin"
        |> DeleteDirs
)

Target "InstallDotnet" (fun _ ->
    dotnetInstall false
)

Target "BuildProjects" (fun _ ->
    !! "src/*/project.json" 
        |> Seq.iter(fun proj ->  
            dotnetRestore id proj
            dotnetPack (fun c -> 
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