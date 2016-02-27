#I "packages/FAKE/tools/"
#r "FakeLib.dll"

open Fake
open Fake.Git
open Fake.FSharpFormatting
open Fake.ReleaseNotesHelper
open Fake.AssemblyInfoFile
open Fake.SemVerHelper

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

    // compute commit count
    let repositoryDir = currentDirectory
    let currentSha = getCurrentSHA1 repositoryDir
    let comitCount = runSimpleGitCommand repositoryDir "rev-list --count HEAD"

    let prereleaseInfo = 
        match releaseNotes.SemVer.PreRelease with
        | Some ver ->     
            let buildCounterFixed = comitCount.PadLeft(3, '0')             
            let versionWithBuild = sprintf "%s-%s" ver.Origin buildCounterFixed           
            Some {
                PreRelease.Origin = versionWithBuild
                Name = versionWithBuild
                Number = None
            }
        | _ -> None

    // update version info file
    CreateFSharpAssemblyInfo "src/SolutionInfo.fs"
        [   Attribute.Version releaseNotes.AssemblyVersion
            Attribute.FileVersion releaseNotes.AssemblyVersion
            Attribute.InformationalVersion releaseNotes.NugetVersion ]

    version <- Some { releaseNotes.SemVer with PreRelease = prereleaseInfo }
    tracefn "Using version: %A" version.Value
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

    let githubLink = "https://github.com/dolly22/FAKE.Dotnet"
    let projInfo =
      [ "page-description", "FAKE.Dotnet"
        "page-author", separated ", " authors
        "project-author", separated ", " authors
        "github-link", githubLink
        "project-github", githubLink
        "project-nuget", "https://www.nuget.org/packages/FAKE.Dotnet"
        "root", "http://dolly22.github.io/FAKE.Dotnet"
        "project-name", "FAKE.Dotnet" ]

    CreateDir docsDir

    let dllFiles = 
        [ buildDir @@ "Fake.Dotnet.dll" ]

    let apiDocsDir = docsDir @@ "apidocs"
    CreateDir apiDocsDir

    CreateDocsForDlls apiDocsDir templatesDir (projInfo @ ["--libDirs", buildDir]) (githubLink + "/blob/master") dllFiles

    CopyDir (docsDir @@ "content") "help/content" allFiles
)

Target "UpdateDocs" (fun _ ->
    let githubPagesDir = currentDirectory @@ "gh-pages"

    CleanDir githubPagesDir
    cloneSingleBranch "" "https://github.com/dolly22/FAKE.Dotnet" "gh-pages" githubPagesDir

    fullclean githubPagesDir
    CopyRecursive docsDir githubPagesDir true |> printfn "%A"
    StageAll githubPagesDir
    Commit githubPagesDir (sprintf "Update generated documentation %s" <| version.Value.ToString())
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
    =?> ("UpdateDocs", hasBuildParam "--update-docs")
    ==> "Default"

// start build
RunTargetOrDefault "Default"