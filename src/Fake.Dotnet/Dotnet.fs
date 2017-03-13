/// .NET Core + CLI tools helpers
module Fake.Dotnet

open Fake
open FSharp.Data
open System
open System.IO
open System.Security.Cryptography
open System.Text

/// .NET Core SDK default install directory (set to default localappdata dotnet dir). Update this to redirect all tool commands to different location. 
let DefaultDotnetSdkDir = environVar "LocalAppData" @@ "Microsoft" @@ "dotnet"

/// Get dotnet cli executable path
/// ## Parameters
///
/// - 'dotnetSdkDir' - dotnet cli install directory 
let private dotnetCliToolPath dotnetSdkDir = dotnetSdkDir @@ "dotnet.exe"

/// Get .NET Core SDK download uri
let private getDotnetSdkInstallerUrl branch = sprintf "https://raw.githubusercontent.com/dotnet/cli/%s/scripts/obtain/dotnet-install.ps1" branch

/// Download .NET Core SDK installer
let private downloadDotnetSdkInstaller branch fileName =  
    let url = getDotnetSdkInstallerUrl branch
    let installScript = Http.RequestStream url
    use outFile = File.Open(fileName, FileMode.Create)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dotnet sdk installer (%s) to %s" url fileName)

/// [omit]
let private md5 (data : byte array) : string =
    use md5 = MD5.Create()
    (StringBuilder(), md5.ComputeHash(data))
    ||> Array.fold (fun sb b -> sb.Append(b.ToString("x2")))
    |> string


/// .NET Core SDK installer download options
type DotnetSdkInstallerOptions =
    {   
        /// Always download install script (otherwise install script is cached in temporary folder)
        AlwaysDownload: bool;
        /// Download installer from this github branch
        Branch: string;
    }

    /// Parameter default values.
    static member Default = {
        AlwaysDownload = false
        Branch = "rel/1.0.0"
    }

/// Download .NET Core SDK installer
/// ## Parameters
///
/// - 'setParams' - set download installer options
let DotnetSdkDownloadInstaller setParams =
    let param = DotnetSdkInstallerOptions.Default |> setParams

    let scriptName = sprintf "dotnet_install_%s.ps1" <| md5 (Encoding.ASCII.GetBytes(param.Branch))
    let tempInstallerScript = Path.GetTempPath() @@ scriptName

    // maybe download installer script
    match param.AlwaysDownload || not(File.Exists(tempInstallerScript)) with
        | true -> downloadDotnetSdkInstaller param.Branch tempInstallerScript 
        | _ -> ()

    tempInstallerScript


/// .NET Core SDK architecture
type DotnetSdkArchitecture =
    /// this value represents currently running OS architecture 
    | Auto
    | X86
    | X64

/// .NET Core SDK version (used to specify version when installing .NET Core SDK)
type DotnetSdkVersion =
    /// most latest build on specific channel 
    | Latest
    ///  last known good version on specific channel (Note: LKG work is in progress. Once the work is finished, this will become new default)
    | Lkg
    /// 4-part version in a format A.B.C.D - represents specific version of build
    | Version of string
  
/// .NET Core SDK install options
type DotnetSdkInstallOptions =
    {   
        /// Custom installer obtain (download) options
        InstallerOptions: DotnetSdkInstallerOptions -> DotnetSdkInstallerOptions
        /// .NET Core SDK channel (defaults to normalized installer branch)
        Channel: string option;
        /// .NET Core SDK version
        Version: DotnetSdkVersion;
        /// Custom installation directory (for local build installation)
        CustomInstallDir: string option
        /// Architecture
        Architecture: DotnetSdkArchitecture;
        /// Installs just the shared runtime bits, not the entire SDK
        SharedRuntime: bool;
        /// Include symbols in the installation (Switch does not work yet. Symbols zip is not being uploaded yet) 
        DebugSymbols: bool;
        /// If set it will not perform installation but instead display what command line to use
        DryRun: bool
        /// Do not update path variable
        NoPath: bool
        /// Displays diagnostics information.
        Verbose: bool
        ///  If set, the installer will use the proxy when making web requests
        ProxyAddress: string option
    }

    /// Parameter default values.
    static member Default = {
        InstallerOptions = id
        Channel = None
        Version = Latest
        CustomInstallDir = None
        Architecture = Auto
        SharedRuntime = false
        DebugSymbols = false
        DryRun = false
        NoPath = true
        Verbose = false
        ProxyAddress = None
    }

/// Well known DotnetSdk versions
type SdkVersions =

    /// Version shipped with VS2017 (1.0.0 SDK, shared framework 1.0.4 and 1.1.1)
    static member NetCore100 options: DotnetSdkInstallOptions = 
        { options with
            Version = Version "1.0.0"
        }

    /// Released 2017/03/07 (1.0.1 SDK, shared framework 1.0.4 and 1.1.1)
    static member NetCore101 options: DotnetSdkInstallOptions = 
        { options with
            Version = Version "1.0.1"
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
let private buildDotnetSdkInstallArgs (param: DotnetSdkInstallOptions) =
    let versionParamValue = 
        match param.Version with
        | Latest -> "latest"
        | Lkg -> "lkg"
        | Version ver -> ver

    // get channel value from installer branch info    
    let channelParamValue = 
        match param.Channel with
            | Some ch -> ch
            | None -> 
                let installerOptions = DotnetSdkInstallerOptions.Default |> param.InstallerOptions
                installerOptions.Branch |> replace "/" "-"

    let architectureParamValue = 
        match param.Architecture with
        | Auto -> None
        | X86 -> Some "x86"
        | X64 -> Some "x64"
    [   
        sprintf "-Channel '%s'" channelParamValue
        sprintf "-Version '%s'" versionParamValue        
        optionToParam architectureParamValue "-Architecture %s"
        optionToParam param.CustomInstallDir "-InstallDir '%s'"
        optionToParam param.ProxyAddress "-ProxyAddress '%s'"
        boolToFlag param.SharedRuntime "-SharedRuntime"
        boolToFlag param.DebugSymbols "-DebugSymbols"
        boolToFlag param.DryRun "-DryRun"
        boolToFlag param.NoPath "-NoPath"
        boolToFlag param.Verbose "-Verbose"        
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "



/// Install .NET Core SDK if required
/// ## Parameters
///
/// - 'setParams' - set installation options
let DotnetSdkInstall setParams =
    let param = DotnetSdkInstallOptions.Default |> setParams  
    let installScript = DotnetSdkDownloadInstaller param.InstallerOptions

    let args = sprintf "-ExecutionPolicy Bypass -NoProfile -NoLogo -NonInteractive -Command \"%s %s; if (-not $?) { exit -1 };\"" installScript (buildDotnetSdkInstallArgs param)
    let exitCode = 
        ExecProcess (fun info ->
            info.FileName <- "powershell"
            info.WorkingDirectory <- Path.GetTempPath()
            info.Arguments <- args
        ) TimeSpan.MaxValue

    if exitCode <> 0 then
        // force download new installer script
        traceError ".NET Core SDK install failed, trying to redownload installer..."
        DotnetSdkDownloadInstaller (param.InstallerOptions >> (fun o -> 
            { o with 
                AlwaysDownload = true
            })) |> ignore
        failwithf ".NET Core SDK install failed with code %i" exitCode

/// dotnet cli command execution options
type DotnetOptions =
    {
        /// Dotnet sdk install directory
        DotnetSdkDir: string;
        /// Command working directory
        WorkingDirectory: string;
        /// Custom parameters
        CustomParams: string option
    }

    static member Default = {
        DotnetSdkDir = DefaultDotnetSdkDir
        WorkingDirectory = currentDirectory
        CustomParams = None
    }


/// Execute raw dotnet cli command
/// ## Parameters
///
/// - 'setOptions' - set dotnet execution options
/// - 'args' - command arguments
let Dotnet (setOptions: DotnetOptions -> DotnetOptions) args = 
    let options = DotnetOptions.Default |> setOptions    

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
            info.FileName <- dotnetCliToolPath options.DotnetSdkDir
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
type DotnetVerbosity =
    | Quiet
    | Minimal
    | Normal
    | Detailed
    | Diagnostic

/// dotnet restore command options
type DotnetRestoreOptions =
    {   
        /// Common tool options
        CommonOptions: DotnetOptions -> DotnetOptions;
        /// Specifies a NuGet package source to use during the restore (-s|--source).
        Source: string option;
        /// Target runtime to restore packages for. (-r|--runtime <RUNTIME_IDENTIFIER>).
        Runtime: string option;
        /// Directory to install packages in (--packages).
        Packages: string list;
        /// Path to the nuget configuration file (nuget.config).
        ConfigFile: string option;
        /// No cache flag (--no-cache)
        NoCache: bool;
        /// Restore logging verbosity (--verbosity)
        Verbosity: DotnetVerbosity option
        /// Only warning failed sources if there are packages meeting version requirement (--ignore-failed-sources)
        IgnoreFailedSources: bool;
        /// Disables restoring multiple projects in parallel (--disable-parallel)
        DisableParallel: bool;
        /// Set this flag to ignore project to project references and only restore the root project (--no-dependencies)
        NoDependencies: bool;
    }

    /// Parameter default values.
    static member Default = {
        CommonOptions = id
        Source = None
        Runtime = None
        Packages = []
        ConfigFile = None        
        NoCache = false
        Verbosity = None
        IgnoreFailedSources = false
        DisableParallel = false
        NoDependencies = false
    }

/// [omit]
let private buildRestoreArgs (param: DotnetRestoreOptions) =
    [   param.Source |> Option.toList |> argList2 "source"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.Packages |> argList2 "packages"
        param.ConfigFile |> Option.toList |> argList2 "configFile"
        param.NoCache |> argOption "no-cache" 
        param.IgnoreFailedSources |> argOption "ignore-failed-sources" 
        param.DisableParallel |> argOption "disable-parallel" 
        param.NoDependencies |> argOption "no-dependencies" 
        param.Verbosity |> Option.toList |> Seq.map (fun v -> v.ToString()) |> argList2 "verbosity"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet restore (Restore dependencies specified in the .NET project)
/// ## Parameters
///
/// - 'setParams' - set restore command parameters
/// - 'project' - project to restore packages
let DotnetRestore setParams project =    
    traceStartTask "Dotnet:restore" project
    let param = DotnetRestoreOptions.Default |> setParams    
    let args = sprintf "restore %s %s" project (buildRestoreArgs param)
    let result = Dotnet param.CommonOptions args    
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
        CommonOptions: DotnetOptions -> DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Defines the value for the $(VersionSuffix) property in the project
        VersionSuffix: string option;
        /// Build base path (--build-base-path)
        BuildBasePath: string option;
        /// Output path (--output)
        OutputPath: string option;
        /// No build flag (--no-build)
        NoBuild: bool;
        /// Include packages with symbols in addition to regular packages in output directory (--include-symbols)
        IncludeSymbols: bool;
        /// Include PDBs and source files. Source files go into the src folder in the resulting nuget package (--include-source)
        IncludeSource: bool;
        /// Set the serviceable flag in the package. For more information, please see https://aka.ms/nupkgservicing (-s|--serviceable)
        Serviceable: bool;
        /// Set the verbosity level of the command. (--verbosity)
        Verbosity: DotnetVerbosity option
    }

    /// Parameter default values.
    static member Default = {
        CommonOptions = id
        Configuration = Release
        VersionSuffix = None
        BuildBasePath = None
        OutputPath = None
        NoBuild = false
        IncludeSymbols = false
        IncludeSource = false
        Serviceable = false
        Verbosity = None
    }

/// [omit]
let private buildPackArgs (param: DotNetPackOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        param.BuildBasePath |> Option.toList |> argList2 "build-base-path"
        param.OutputPath |> Option.toList |> argList2 "output"
        param.NoBuild |> argOption "no-build" 
        param.IncludeSymbols |> argOption "include-symbols" 
        param.IncludeSource |> argOption "include-source" 
        param.Serviceable |> argOption "serviceable" 
        param.Verbosity |> Option.toList |> Seq.map (fun v -> v.ToString()) |> argList2 "verbosity"
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
    let result = Dotnet param.CommonOptions args    
    if not result.OK then failwithf "dotnet pack failed with code %i" result.ExitCode
    traceEndTask "Dotnet:pack" project


/// dotnet publish command options
type DotNetPublishOptions =
    {   
        /// Common tool options
        CommonOptions: DotnetOptions -> DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Target framework to compile for (--framework)
        Framework: string option;
        /// Target runtime to publish for (--runtime)
        Runtime: string option;
        /// Output path (--output)
        OutputPath: string option;
        /// Defines the value for the $(VersionSuffix) property in the project(--version-suffix)
        VersionSuffix: string option;
        /// Set the verbosity level of the command. (--verbosity)
        Verbosity: DotnetVerbosity option
    }

    /// Parameter default values.
    static member Default = {
        CommonOptions = id
        Configuration = Release
        Framework = None
        Runtime = None
        OutputPath = None
        VersionSuffix = None
        Verbosity = None
    }

/// [omit]
let private buildPublishArgs (param: DotNetPublishOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.OutputPath |> Option.toList |> argList2 "output"
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        param.Verbosity |> Option.toList |> Seq.map (fun v -> v.ToString()) |> argList2 "verbosity"
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
    let result = Dotnet param.CommonOptions args    
    if not result.OK then failwithf "dotnet publish failed with code %i" result.ExitCode
    traceEndTask "Dotnet:publish" project


/// dotnet build command options
type DotNetBuildOptions =
    {   
        /// Common tool options
        CommonOptions: DotnetOptions -> DotnetOptions;
        /// Pack configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Target framework to compile for (--framework)
        Framework: string option;
        /// Target runtime to publish for (--runtime)
        Runtime: string option;
        /// Output path (--output)
        OutputPath: string option;
        /// Disables incremental build (--no-incremental)
        NoIncremental: bool;
        /// Set this flag to ignore project-to-project references and only build the root project (--no-dependencies)
        NoDependencies: bool;
        /// Defines the value for the $(VersionSuffix) property in the project(--version-suffix)
        VersionSuffix: string option;
        /// Set the verbosity level of the command. (--verbosity)
        Verbosity: DotnetVerbosity option
    }

    /// Parameter default values.
    static member Default = {
        CommonOptions = id
        Configuration = Release
        Framework = None
        Runtime = None
        OutputPath = None
        NoIncremental = false
        NoDependencies = false
        VersionSuffix = None
        Verbosity = None
    }


/// [omit]
let private buildBuildArgs (param: DotNetBuildOptions) =
    [  
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Runtime |> Option.toList |> argList2 "runtime"
        param.OutputPath |> Option.toList |> argList2 "output"
        param.NoIncremental |> argOption "no-incremental" 
        param.NoDependencies |> argOption "no-dependencies" 
        param.VersionSuffix |> Option.toList |> argList2 "version-suffix"
        param.Verbosity |> Option.toList |> Seq.map (fun v -> v.ToString()) |> argList2 "verbosity"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet build command
/// ## Parameters
///
/// - 'setParams' - set compile command parameters
/// - 'projectFile' - projectFile to build
let DotnetBuild setParams projectFile =    
    traceStartTask "Dotnet:build" projectFile
    let param = DotNetBuildOptions.Default |> setParams    
    let args = sprintf "build %s %s" projectFile (buildBuildArgs param)
    let result = Dotnet param.CommonOptions args    
    if not result.OK then failwithf "dotnet build failed with code %i" result.ExitCode
    traceEndTask "Dotnet:build" projectFile


/// Execute dotnet msbuild command using FAKE msbuild options
/// ## Parameters
///
/// - 'setMsbuildParams' - set msbuild command parameters
/// - 'setOptions' - set dotnet execution options
/// - 'project' - project to compile
let DotnetMsbuild setMsbuildParams setOptions projectFile = 
    traceStartTask "Dotnet:msbuild" projectFile

    // build args
    let args = MSBuildDefaults |> setMsbuildParams |> serializeMSBuildParams
    let result = Dotnet setOptions (sprintf "msbuild %s %s" projectFile args) 
    if not result.OK then failwithf "dotnet msbuild failed with code %i" result.ExitCode
    traceEndTask "Dotnet:msbuild" projectFile