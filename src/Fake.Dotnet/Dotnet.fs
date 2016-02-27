module Fake.Dotnet

open Fake
open FSharp.Data
open System
open System.IO

/// Dotnet cli installer script
let private dotnetCliInstaller = "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1"

/// Dotnet cli default install directory (windows)
let mutable DefaultDotnetCliDir = environVar "LocalAppData" @@ "Microsoft" @@ "dotnet"

// Dotnet cli executable path
let private dotnetCliPath installDir = installDir @@ "cli" @@ "bin" @@ "dotnet.exe"

// Temporary path of installer script
let private tempInstallerScript = Path.GetTempPath() @@ "dotnet_install.ps1"

let private downloadInstaller fileName =  
    let installScript = Http.RequestStream dotnetCliInstaller
    use outFile = File.OpenWrite(fileName)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dotnet installer to %s" fileName)
    fileName

type DotnetCliVersion =
    | Latest
    | Version of string
    
type DotNetCliInstallOptions =
    {   
        /// Always download install script (otherwise install script is cached in temporary folder)
        AlwaysDownload: bool;
        /// DotnetCli version
        Version: DotnetCliVersion;
        /// Distribution channel
        Channel: string option;
        /// Custom installation directory (for local build installation)
        InstallDirectory: string option
    }

    /// Parameter default values.
    static member Default = {
        AlwaysDownload = false
        Version = Latest
        Channel = None
        InstallDirectory = None
    }

let private optionToParam option paramFormat =
    match option with
    | Some value -> sprintf paramFormat value
    | None -> ""

/// [omit]
let private buildDotnetCliInstallArgs (param: DotNetCliInstallOptions) =
    let versionParam = 
        match param.Version with
        | Latest -> ""
        | Version ver -> sprintf "-version '%s'" ver
    [   
        versionParam
        optionToParam param.Channel "-channel '%s'"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


let DotnetCliInstall setParams =
    let param = DotNetCliInstallOptions.Default |> setParams  

    let installScript = 
        match param.AlwaysDownload || not(File.Exists(tempInstallerScript)) with
            | true -> downloadInstaller tempInstallerScript
            | false -> tempInstallerScript

    // set custom install directory
    if param.InstallDirectory.IsSome then
        setEnvironVar "DOTNET_INSTALL_DIR" param.InstallDirectory.Value

    let args = sprintf "-NoProfile -NoLogo -Command \"%s %s; exit $LastExitCode;\"" installScript (buildDotnetCliInstallArgs param)
    let exitCode = 
        ExecProcess (fun info ->
            info.FileName <- "powershell"
            info.WorkingDirectory <- Path.GetTempPath()
            info.Arguments <- args
        ) TimeSpan.MaxValue

    if exitCode <> 0 then failwithf "dotnet install failed with code %i" exitCode

type DotnetOptions =
    {
        /// Dotnet cli install directory
        DotnetDirectory: string;
        /// Command working directory
        WorkingDirectory: string;
    }

    static member Default = {
        DotnetDirectory = DefaultDotnetCliDir
        WorkingDirectory = currentDirectory
    }

let Dotnet (options: DotnetOptions) args = 
    let errors = new System.Collections.Generic.List<string>()
    let messages = new System.Collections.Generic.List<string>()
    let timeout = TimeSpan.MaxValue

    let errorF msg =
        traceError msg
        errors.Add msg 

    let messageF msg =
        traceImportant msg
        messages.Add msg

    let result = 
        ExecProcessWithLambdas (fun info ->
            info.FileName <- dotnetCliPath options.DotnetDirectory
            info.WorkingDirectory <- options.WorkingDirectory
            info.Arguments <- args
        ) timeout true errorF messageF

    ProcessResult.New result messages errors


/// [omit]
let private argList2 name values =
    values
    |> Seq.collect (fun v -> ["--" + name; sprintf @"""%s""" v])
    |> String.concat " "

type DotnetRestoreOptions =
    {   
        /// Common tool options
        Common: DotnetOptions;
        /// Nuget feeds to search updates in. Use default if empty.
        Sources: string list;
        /// Path to the nuget.exe.
        ConfigFile: string option;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Sources = []
        ConfigFile = None
    }

/// [omit]
let private buildRestoreArgs (param: DotnetRestoreOptions) =
    [   param.Sources |> argList2 "source"
        param.ConfigFile |> Option.toList |> argList2 "configFile"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "

let DotnetRestore setParams project =    
    traceStartTask "Dotnet:restore" project
    let param = DotnetRestoreOptions.Default |> setParams    
    let args = sprintf "restore %s %s" project (buildRestoreArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet restore failed with code %i" result.ExitCode
    traceEndTask "Dotnet:restore" project


type PackConfiguration =
    | Debug
    | Release
    | Custom of string

type DotNetPackOptions =
    {   
        /// Common tool options
        Common: DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: PackConfiguration;
        /// Version suffix to use
        VersionSuffix: string option;
        /// Build base path (--build-base-path)
        BuildBasePath: string option;
        /// Output path (--output)
        OutputPath: string option;
        /// No build flag (--no-build)
        NoBuild: bool;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Configuration = Release
        VersionSuffix = None
        BuildBasePath = None
        OutputPath = None
        NoBuild = false
    }

/// [omit]
let private buildPackArgs (param: DotNetPackOptions) =
    [  
        sprintf "--configuration %s" 
            (match param.Configuration with
            | Debug -> "Debug"
            | Release -> "Release"
            | Custom config -> config)
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        (if param.NoBuild then "--no-build" else "")
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "

let DotnetPack setParams project =    
    traceStartTask "Dotnet:pack" project
    let param = DotNetPackOptions.Default |> setParams    
    let args = sprintf "pack %s %s" project (buildPackArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet pack failed with code %i" result.ExitCode
    traceEndTask "Dotnet:pack" project