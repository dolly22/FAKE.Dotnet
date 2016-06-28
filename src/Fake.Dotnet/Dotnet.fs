/// dotnet cli helpers
module Fake.Dotnet

open Fake
open FSharp.Data
open FSharp.Data.JsonExtensions
open System
open System.IO
open System.Security.Cryptography
open System.Text

/// Get dotnet cli download uri
let private getDotnetCliInstallerUrl branch = sprintf "https://raw.githubusercontent.com/dotnet/cli/%s/scripts/obtain/dotnet-install.ps1" branch

/// Dotnet cli default install directory (set to default localappdata dotnet dir). Update this to redirect all tool commands to different location. 
let mutable DefaultDotnetCliDir = environVar "LocalAppData" @@ "Microsoft" @@ "dotnet"

/// Get dotnet cli executable path
/// ## Parameters
///
/// - 'dotnetCliDir' - dotnet cli install directory 
let private dotnetCliPath dotnetCliDir = dotnetCliDir @@ "dotnet.exe"

let private downloadInstaller fileName branch =  
    let url = getDotnetCliInstallerUrl branch
    let installScript = Http.RequestStream url
    use outFile = File.Open(fileName, FileMode.Create)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dotnet installer to %s" fileName)
    fileName

/// dotnet cli architecture
type DotnetCliArchitecture =
    /// this value represents currently running OS architecture 
    | Auto
    | X86
    | X64

/// dotnet cli version (used to specify version when installing dotnet cli)
type DotnetCliVersion =
    /// most latest build on specific channel 
    | Latest
    ///  last known good version on specific channel (Note: LKG work is in progress. Once the work is finished, this will become new default)
    | Lkg
    /// 4-part version in a format A.B.C.D - represents specific version of build
    | Version of string

/// dotnet cli channel
type DotnetCliChannel =
    /// Possibly unstable, frequently changing, may contain new finished and unfinished features
    | Future
    /// Most stable releases
    | Production
    /// Custom channel
    | Channel of string
    
/// dotnet cli install options
type DotNetCliInstallOptions =
    {   
        /// Always download install script (otherwise install script is cached in temporary folder)
        AlwaysDownload: bool;
        /// Download installer from this github branch
        InstallerBranch: string;
        /// Distribution channel
        Channel: DotnetCliChannel;
        /// DotnetCli version
        Version: DotnetCliVersion;
        /// Custom installation directory (for local build installation)
        CustomInstallDir: string option
        /// Architecture
        Architecture: DotnetCliArchitecture;
        /// Include symbols in the installation (Switch does not work yet. Symbols zip is not being uploaded yet) 
        DebugSymbols: bool;
        /// If set it will not perform installation but instead display what command line to use
        DryRun: bool
        /// Do not update path variable
        NoPath: bool
    }

    /// Parameter default values.
    static member Default = {
        AlwaysDownload = false
        InstallerBranch = "master"
        Channel = Future
        Version = Latest        
        CustomInstallDir = None
        Architecture = Auto        
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
        | Production -> "production"
        | Channel x -> x

    let architectureParamValue = 
        match param.Architecture with
        | Auto -> None
        | X86 -> Some "x86"
        | X64 -> Some "x64"
    [   
        sprintf "-channel '%s'" channelParamValue
        sprintf "-version '%s'" versionParamValue        
        optionToParam architectureParamValue "-architecture %s"
        boolToFlag param.DebugSymbols "-DebugSymbols"
        boolToFlag param.DryRun "-DryRun"
        boolToFlag param.NoPath "-NoPath"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


let private md5 (data : byte array) : string =
    use md5 = MD5.Create()
    (StringBuilder(), md5.ComputeHash(data))
    ||> Array.fold (fun sb b -> sb.Append(b.ToString("x2")))
    |> string


/// Install dotnet cli if required
/// ## Parameters
///
/// - 'setParams' - set installation options
let DotnetCliInstall setParams =
    let param = DotNetCliInstallOptions.Default |> setParams  

    let scriptName = sprintf "dotnet_install_%s.ps1" <| md5 (Encoding.ASCII.GetBytes(param.InstallerBranch))
    let tempInstallerScript = Path.GetTempPath() @@ scriptName

    let installScript = 
        match param.AlwaysDownload || not(File.Exists(tempInstallerScript)) with
            | true -> downloadInstaller tempInstallerScript param.InstallerBranch
            | false -> tempInstallerScript

    // set custom install directory
    if param.CustomInstallDir.IsSome then
        setEnvironVar "DOTNET_INSTALL_DIR" param.CustomInstallDir.Value

    let args = sprintf "-ExecutionPolicy Bypass -NoProfile -NoLogo -NonInteractive -Command \"%s %s; if (-not $?) { exit -1 };\"" installScript (buildDotnetCliInstallArgs param)
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
        /// Dotnet cli executable path
        DotnetCliPath: string;
        /// Command working directory
        WorkingDirectory: string;
        /// Custom parameters
        CustomParams: string option
    }

    static member Default = {
        DotnetCliPath = dotnetCliPath DefaultDotnetCliDir
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
            info.FileName <- options.DotnetCliPath
            info.WorkingDirectory <- options.WorkingDirectory
            info.Arguments <- cmdArgs
        ) timeout true errorF messageF

    ProcessResult.New result messages errors


/// [omit]
let private argList2 name values =
    values
    |> Seq.collect (fun v -> ["--" + name; sprintf @"""%s""" v])
    |> String.concat " "

/// [omit]
let private argOption name value =
    match value with
        | true -> sprintf "--%s" name
        | false -> ""

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
    [   param.Sources |> argList2 "source"
        param.Packages |> argList2 "packages"
        param.ConfigFile |> Option.toList |> argList2 "configFile"
        param.NoCache |> argOption "no-cache" 
        param.IgnoreFailedSources |> argOption "ignore-failed-sources" 
        param.DisableParallel |> argOption "disable-parallel" 
        param.Verbosity |> Option.toList |> Seq.map (fun v -> v.ToString()) |> argList2 "verbosity"
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
        param.NoBuild |> argOption "no-build" 
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
        param.NoBuild |> argOption "no-build" 
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

/// get sdk version from global.json
/// ## Parameters
///
/// - 'project' - global.json path
let GlobalJsonSdk project =
    let data = ReadFileAsString project
    let info = JsonValue.Parse(data)
    info?sdk?version.AsString()   