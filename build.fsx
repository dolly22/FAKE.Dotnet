#I "packages/FAKE/tools/"
#r "FakeLib.dll"

open Fake
open Fake.FSharpFormatting
open Fake.ReleaseNotesHelper
open Fake.AssemblyInfoFile

let authors = ["Tomas Dolezal"]

let docsDir = "./artifacts/docs"
let buildDir = "./artifacts/build"

let releaseNotes = LoadReleaseNotes "release_notes.md"
let mutable version : SemVerHelper.SemVerInfo option = None

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin"
        |> DeleteDirs
)

Target "UpdateVersion" (fun _ ->   
    tracefn "Release notes version: %s" releaseNotes.NugetVersion

    //TODO: add ci prerelease

    // update version info file
    CreateFSharpAssemblyInfo "src/SolutionInfo.fs"
        [   Attribute.Version releaseNotes.AssemblyVersion
            Attribute.FileVersion releaseNotes.AssemblyVersion
            Attribute.InformationalVersion releaseNotes.NugetVersion ]

    version <- Some releaseNotes.SemVer
)

Target "BuildProjects" (fun _ ->
    !! "src/*/*.fsproj" 
        |> Seq.iter(fun proj -> 
            build (fun p -> 
            { 
                p with
                    Targets = [ "Build" ]
                    Properties = 
                    [ 
                        ("Configuration", "Release"); 
                        ("OutDir", currentDirectory @@ buildDir) ]
            }) proj
        )
)

Target "GenerateDocs" (fun _ ->
    let toolRoot = "./packages/FSharp.Formatting.CommandTool";
    let templatesDir = toolRoot @@ "templates/reference/"

    let githubLink = "https://github.com/dolly22/fake.dotnet"
    let projInfo =
      [ "page-description", "FAKE - dotnet cli"
        "page-author", separated ", " authors
        "project-author", separated ", " authors
        "github-link", githubLink
        "project-github", githubLink
        "project-nuget", "https://www.nuget.org/packages/FAKE.Dotnet"
        "root", "http://dolly22.github.io/fake.dotnet"
        "project-name", "FAKE - dotnet cli" ]

    CreateDir docsDir

    let dllFiles = 
        [ buildDir @@ "Fake.Dotnet.dll" ]

    CreateDocsForDlls docsDir templatesDir (projInfo @ ["--libDirs", buildDir]) (githubLink + "/blob/master") dllFiles

    CopyDir (docsDir @@ "content") "help/content" allFiles
)

Target "NugetPack" (fun _ -> 
    CreateDir "artifacts"

    let nugetVersion = 
        match version with
        | None -> "0.0.1-dev"
        | Some x -> x.ToString()

    NuGetPackDirectly (fun p -> 
    {
        p with
            ToolPath = "packages/NuGet.CommandLine/tools/nuget.exe"
            OutputPath = "artifacts"
            WorkingDir = "artifacts"
            Version = nugetVersion
    }) "Fake.Dotnet.nuspec"
)

Target "Default" <| DoNothing

"Clean"
    ==> "UpdateVersion"
    ==> "BuildProjects"
    ==> "NugetPack"
    ==> "GenerateDocs"
    ==> "Default"

// start build
RunTargetOrDefault "Default"