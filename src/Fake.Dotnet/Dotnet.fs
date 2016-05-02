/// dotnet cli helpers
module Fake.Dotnet

open Fake
open FSharp.Data
open System
open System.IO

/// Dotnet cli installer script
let private dotnetCliInstaller = "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.ps1"

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
    use outFile = File.Open(fileName, FileMode.Create)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dotnet installer to %s" fileName)
    fileName

/// dotnet cli architecture
type DotnetCliArchitecture =
    | Auto
    | X86
    | X64

/// dotnet cli version (used to specify version when installing dotnet cli)
type DotnetCliVersion =
    | Latest
    | Lkg
    | Version of string

/// dotnet cli channel
type DotnetCliChannel =
    | Future
    | Preview
    | Production
    
/// dotnet cli install options
type DotNetCliInstallOptions =
    {   
        /// Always download install script (otherwise install script is cached in temporary folder)
        AlwaysDownload: bool;
        /// DotnetCli version
        Version: DotnetCliVersion;
        /// Distribution channel
        Channel: DotnetCliChannel;
        /// Architecture
        Architecture: DotnetCliArchitecture;
        /// Custom installation directory (for local build installation)
        CustomInstallDir: string option
        /// Include symbols in the installation
        DebugSymbols: bool;
        /// If set it will not perform installation but instead display what command line to use
        DryRun: bool
        /// Do not update path variable
        NoPath: bool
    }

    /// Parameter default values.
    static member Default = {
        AlwaysDownload = false
        Version = Latest
        Channel = Preview
        Architecture = Auto
        CustomInstallDir = None
        DebugSymbols = false
        DryRun = false
        NoPath = true
    }

/// [omit]
let private optionToParam option paramFormat =
    match option with
    | Some value -> sprintf paramFormat value
    | None -> ""

/// [omit]
let private boolToFlag value flagParam = 
    match value with
    | true -> flagParam
    | false -> ""

/// [omit]
let private buildDotnetCliInstallArgs (param: DotNetCliInstallOptions) =
    let versionParamValue = 
        match param.Version with
        | Latest -> "latest"
        | Lkg -> "lkg"
        | Version ver -> ver

    let channelParamValue = 
        match param.Channel with
        | Future -> "future"
        | Preview -> "preview"
        | Production -> "production"

    let architectureParamValue = 
        match param.Architecture with
        | Auto -> None
        | X86 -> Some "x86"
        | X64 -> Some "x64"
    [   
        sprintf "-version '%s'" versionParamValue
        sprintf "-channel '%s'" channelParamValue
        optionToParam architectureParamValue "-architecture %s"
        boolToFlag param.DebugSymbols "-DebugSymbols"
        boolToFlag param.DryRun "-DryRun"
        boolToFlag param.NoPath "-NoPath"
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
    if param.CustomInstallDir.IsSome then
        setEnvironVar "DOTNET_INSTALL_DIR" param.CustomInstallDir.Value

    let args = sprintf "-NoProfile -NoLogo -Command \"%s %s; if (-not $?) { exit -1 };\"" installScript (buildDotnetCliInstallArgs param)
    let exitCode = 
        ExecProcess (fun info ->
            info.FileName <- "powershell"
            info.WorkingDirectory <- Path.GetTempPath()
            info.Arguments <- args
        ) TimeSpan.MaxValue

    if exitCode <> 0 then
        // force download new installer script
        traceError "dotnet cli install failed, trying to redownload installer..."
        let installScript = downloadInstaller tempInstallerScript
        failwithf "dotnet cli install failed with code %i" exitCode

/// dotnet cli command execution options
type DotnetOptions =
    {
        /// Dotnet cli install directory
        DotnetDirectory: string;
        /// Command working directory
        WorkingDirectory: string;
        /// Custom parameters
        CustomParams: string option
    }

    static member Default = {
        DotnetDirectory = DefaultDotnetCliDir
        WorkingDirectory = currentDirectory
        CustomParams = None
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

    let cmdArgs = match options.CustomParams with
                    | Some v -> sprintf "%s %s" args v
                    | None -> args

    let result = 
        ExecProcessWithLambdas (fun info ->
            info.FileName <- dotnetCliPath options.DotnetDirectory
            info.WorkingDirectory <- options.WorkingDirectory
            info.Arguments <- cmdArgs
        ) timeout true errorF messageF

    ProcessResult.New result messages errors


/// [omit]
let private argList2 name values =
    values
    |> Seq.collect (fun v -> ["--" + name; sprintf @"""%s""" v])
    |> String.concat " "


/// dotnet restore verbosity
type NugetRestoreVerbosity =
    | Debug
    | Verbose
    | Information
    | Minimal
    | Warning
    | Error

/// dotnet restore command options
type DotnetRestoreOptions =
    {   
        /// Common tool options
        Common: DotnetOptions;
        /// Nuget feeds to search updates in. Use default if empty.
        Sources: string list;
        /// Directory to install packages in (--packages).
        Packages: string list;
        /// Path to the nuget configuration file (nuget.config).
        ConfigFile: string option;
        /// No cache flag (--no-cache)
        NoCache: bool;
        /// Restore logging verbosity (--verbosity)
        Verbosity: NugetRestoreVerbosity option
        /// Only warning failed sources if there are packages meeting version requirement (--ignore-failed-sources)
        IgnoreFailedSources: bool;
        /// Disables restoring multiple projects in parallel (--disable-parallel)
        DisableParallel: bool;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Sources = []
        Packages = []
        ConfigFile = None        
        NoCache = false
        Verbosity = None
        IgnoreFailedSources = false
        DisableParallel = false
    }

/// [omit]
let private buildRestoreArgs (param: DotnetRestoreOptions) =
    let restoreVerbosityParamValue = 
        match param.Verbosity with
        | Some v -> sprintf "--verbosity %s" <| v.ToString()
        | None -> ""

    [   param.Sources |> argList2 "source"
        param.Packages |> argList2 "packages"
        param.ConfigFile |> Option.toList |> argList2 "configFile"
        (if param.NoCache then "--no-cache" else "")
        (if param.IgnoreFailedSources then "--ignore-failed-sources" else "")
        (if param.DisableParallel then "--disable-parallel" else "")
        restoreVerbosityParamValue
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

/// build configuration
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

/// dotnet pack command options
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


/// dotnet publish command options
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
        /// Defines what `*` should be replaced with in version field in project.json (--version-suffix)
        VersionSuffix: string option;
        /// No build flag (--no-build)
        NoBuild: bool;
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Configuration = Release
        Framework = None
        Runtime = None
        BuildBasePath = None
        OutputPath = None
        VersionSuffix = None
        NoBuild = false
    }

/// [omit]
let private buildPublishArgs (param: DotNetPublishOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        (if param.NoBuild then "--no-build" else "")
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


/// dotnet build command options
type DotNetBuildOptions =
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
let private buildBuildArgs (param: DotNetBuildOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        (if param.Native then "--native" else "")
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet build command
/// ## Parameters
///
/// - 'setParams' - set compile command parameters
/// - 'project' - project to compile
let DotnetCompile setParams project =    
    traceStartTask "Dotnet:build" project
    let param = DotNetBuildOptions.Default |> setParams    
    let args = sprintf "build %s %s" project (buildBuildArgs param)
    let result = Dotnet param.Common args    
    if not result.OK then failwithf "dotnet build failed with code %i" result.ExitCode
    traceEndTask "Dotnet:build" project