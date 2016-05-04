#I "packages/FAKE/tools/"
#r "FakeLib.dll"

#load "paket-files\dolly22\FAKE.Gitsemver\Gitsemver.fsx"

open Fake
open Fake.Git
open Fake.FSharpFormatting
open Fake.ReleaseNotesHelper
open Fake.AssemblyInfoFile
open Fake.SemVerHelper
open Gitsemver

let authors = ["Tomas Dolezal"]

let docsDir = "./artifacts/docs"
let buildDir = "./artifacts/build"

let mutable version : SemVerHelper.SemVerInfo option = None
let mutable currentGitSha : string = ""

Target "Clean" (fun _ ->
    !! "artifacts" ++ "src/*/bin"
        |> DeleteDirs
)

Target "UpdateVersion" (fun _ ->   
    let semver = 
        getSemverInfoDefault 
        |> appendPreReleaseBuildNumber 3 

    version <- Some semver        
    currentGitSha <- getCurrentSHA1 currentDirectory

    let fileVersion = sprintf "%d.%d.%d" semver.Major semver.Minor semver.Patch
    let assemblyVersion = sprintf "%d.0.0" semver.Major

    // update version info file
    CreateFSharpAssemblyInfo "src/SolutionInfo.fs"
        [   Attribute.Version assemblyVersion
            Attribute.FileVersion fileVersion
            Attribute.InformationalVersion (semver.ToString()) ]

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

    CreateDocsForDlls apiDocsDir templatesDir (projInfo @ ["--libDirs", buildDir]) (githubLink + "/blob/"+ currentGitSha) dllFiles

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