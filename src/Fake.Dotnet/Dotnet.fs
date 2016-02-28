module Fake.Dotnet

open Fake
open FSharp.Data
open System
open System.IO

/// Dotnet cli installer script
let private dotnetCliInstaller = "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/install.ps1"

/// Dotnet cli default install directory (set to default localappdata dotnet dir). Update this to redirect all tool commands to different location. 
let mutable DefaultDotnetCliDir = environVar "LocalAppData" @@ "Microsoft" @@ "dotnet"

/// Get dotnet cli executable path
/// ## Parameters
///
/// - 'dotnetCliDir' - dotnet cli install directory 
let private dotnetCliPath dotnetCliDir = dotnetCliDir @@ "cli" @@ "bin" @@ "dotnet.exe"

// Temporary path of installer script
let private tempInstallerScript = Path.GetTempPath() @@ "dotnet_install.ps1"

let private downloadInstaller fileName =  
    let installScript = Http.RequestStream dotnetCliInstaller
    use outFile = File.OpenWrite(fileName)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dotnet installer to %s" fileName)
    fileName

/// dotnet cli version (used to specify version when installing dotnet cli)
type DotnetCliVersion =
    | Latest
    | Version of string
    
/// dotnet cli install options
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


/// Install dotnet cli if required
/// ## Parameters
///
/// - 'setParams' - set installation options
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

    if exitCode <> 0 then failwithf "dotnet cli install failed with code %i" exitCode

/// dotnet cli command execution options
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


/// Execute raw dotnet cli command
/// ## Parameters
///
/// - 'options' - common execution options
/// - 'args' - command arguments
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

/// dotnet restore command options
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


/// Execute dotnet restore command
/// ## Parameters
///
/// - 'setParams' - set restore command parameters
/// - 'project' - project to restore packages
let DotnetRestore setParams project =    
    traceStartTask "Dotnet:restore" project
    let param = DotnetRestoreOptions.Default |> setParams    
    let args = sprintf "restore %s %s" project (buildRestoreArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet restore failed with code %i" result.ExitCode
    traceEndTask "Dotnet:restore" project

// build configuration
type BuildConfiguration =
    | Debug
    | Release
    | Custom of string

/// [omit]
let private buildConfigurationArg (param: BuildConfiguration) =
    sprintf "--configuration %s" 
        (match param with
        | Debug -> "Debug"
        | Release -> "Release"
        | Custom config -> config)

// dotnet pack command options
type DotNetPackOptions =
    {   
        /// Common tool options
        Common: DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: BuildConfiguration;
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
        buildConfigurationArg param.Configuration
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        (if param.NoBuild then "--no-build" else "")
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet pack command
/// ## Parameters
///
/// - 'setParams' - set pack command parameters
/// - 'project' - project to pack
let DotnetPack setParams project =    
    traceStartTask "Dotnet:pack" project
    let param = DotNetPackOptions.Default |> setParams    
    let args = sprintf "pack %s %s" project (buildPackArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet pack failed with code %i" result.ExitCode
    traceEndTask "Dotnet:pack" project


// dotnet publish command options
type DotNetPublishOptions =
    {   
        /// Common tool options
        Common: DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Target framework to compile for (--framework)
        Framework: string option;
        /// Target runtime to publish for (--runtime)
        Runtime: string option;
        /// Build base path (--build-base-path)
        BuildBasePath: string option;
        /// Output path (--output)
        OutputPath: string option;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Configuration = Release
        Framework = None
        Runtime = None
        BuildBasePath = None
        OutputPath = None
    }

/// [omit]
let private buildPublishArgs (param: DotNetPublishOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet publish command
/// ## Parameters
///
/// - 'setParams' - set publish command parameters
/// - 'project' - project to publish
let DotnetPublish setParams project =    
    traceStartTask "Dotnet:publish" project
    let param = DotNetPublishOptions.Default |> setParams    
    let args = sprintf "publish %s %s" project (buildPublishArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet publish failed with code %i" result.ExitCode
    traceEndTask "Dotnet:publish" project


// dotnet compile command options
type DotNetCompileOptions =
    {   
        /// Common tool options
        Common: DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Target framework to compile for (--framework)
        Framework: string option;
        /// Target runtime to publish for (--runtime)
        Runtime: string option;
        /// Build base path (--build-base-path)
        BuildBasePath: string option;
        /// Output path (--output)
        OutputPath: string option;
        /// Native flag (--native)
        Native: bool;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Configuration = Release
        Framework = None
        Runtime = None
        BuildBasePath = None
        OutputPath = None
        Native = false
    }


/// [omit]
let private buildCompileArgs (param: DotNetCompileOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        (if param.Native then "--native" else "")
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet compile command
/// ## Parameters
///
/// - 'setParams' - set compile command parameters
/// - 'project' - project to compile
let DotnetCompile setParams project =    
    traceStartTask "Dotnet:compile" project
    let param = DotNetCompileOptions.Default |> setParams    
    let args = sprintf "compile %s %s" project (buildCompileArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet compile failed with code %i" result.ExitCode
    traceEndTask "Dotnet:compile" project