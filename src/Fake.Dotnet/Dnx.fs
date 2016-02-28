/// DNX helpers
module Fake.Dnx

open Fake
open FSharp.Data
open FSharp.Data.JsonExtensions
open System
open System.IO

/// Dnvm installer script
let private dnvmInstaller = "https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1"

/// Dnx install directory
let private dnxInstallDir = environVar "UserProfile" @@ ".dnx"

/// Dnvm executable path
let private dnvmPath = dnxInstallDir @@ "bin" @@ "dnvm.cmd"

/// Temporary path of installer script
let private tempInstallerScript = Path.GetTempPath() @@ "dnvminstall.ps1"

let private downloadInstaller fileName =  
    let installScript = Http.RequestStream dnvmInstaller
    use outFile = File.OpenWrite(fileName)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dnvm installer to %s" fileName)
    fileName

/// Install DNX if needed
let DnvmInstall forceInstall =
    if not (fileExists dnvmPath) || forceInstall then     
        let installScript = 
            match forceInstall || not(File.Exists(tempInstallerScript)) with
                | true -> downloadInstaller tempInstallerScript
                | false -> tempInstallerScript

        let args = sprintf "-NoProfile -NoLogo -Command \"%s; exit $LastExitCode;\"" installScript
        let exitCode = 
            ExecProcess (fun info ->
                info.FileName <- "powershell"
                info.WorkingDirectory <- Path.GetTempPath()
                info.Arguments <- args
            ) TimeSpan.MaxValue

        if exitCode <> 0 then failwithf "dnvm install failed with code %i" exitCode

/// dnvm command common options
type DnvmOptions =
    {
        /// Path to dnvm.cmd
        ToolPath: string;
        /// Command working directory
        WorkingDirectory: string;
        /// Automatically install dnvm if needed
        AutoInstall: bool
    }

    /// Default options values
    static member Default = {
        ToolPath = dnvmPath
        WorkingDirectory = currentDirectory
        AutoInstall = true
    }


/// Execute generic dnvm command
/// ## Parameters
///
/// - 'setOptions' - set command options
/// - 'args' - command arguments
let Dnvm setOptions args = 
    let options = DnvmOptions.Default |> setOptions   
    if options.AutoInstall then
        DnvmInstall false

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
            info.FileName <- options.ToolPath
            info.WorkingDirectory <- options.WorkingDirectory
            info.Arguments <- args
        ) timeout true errorF messageF

    ProcessResult.New result messages errors

/// dnvm upgrade command
/// ## Parameters
///
/// - 'setOptions' - set command options
let DnvmUpgrade setOptions =    
    traceStartTask "Dnvm" "upgrade"
    let result = Dnvm setOptions "upgrade"    
    if not result.OK then failwithf "dnvm upgrade failed with code %i" result.ExitCode
    traceEndTask "Dnvm" "upgrade"

/// Common options for dnvm command over some specific sdk version
type DnvmRuntimeOptions =
    {
        /// Common tool options
        Dnvm: DnvmOptions;
        /// Runtime alias
        VersionOrAlias: string;
    }

    /// Default command options
    static member Default = {
        Dnvm = DnvmOptions.Default
        VersionOrAlias = "default"
    }

/// dnvm exec command
/// ## Parameters
///
/// - 'setOptions' - set command options
/// - 'command' - command to execute
let DnvmExec setOptions command =    
    traceStartTask "Dnvm" "exec"    
    let options = DnvmRuntimeOptions.Default |> setOptions
    let args = sprintf "exec %s %s" options.VersionOrAlias command
    let result = Dnvm (fun o -> options.Dnvm) args
    if not result.OK then failwithf "dnvm exec failed with code %i" result.ExitCode
    traceEndTask "Dnvm" "exec"

/// dnu command
/// ## Parameters
///
/// - 'setOptions' - set command options
/// - 'command' - command to execute
let Dnu setOptions command =    
    traceStartTask "Dnu" "command"    
    let args = sprintf "dnu %s" command
    DnvmExec setOptions args
    traceEndTask "Dnu" "command"

/// [omit]
let private argList2 name values =
    values
    |> Seq.collect (fun v -> ["--" + name; sprintf @"""%s""" v])
    |> String.concat " "

/// dnu restore command options
type DnuRestoreParams =
    {
        /// Dnvm runtime options
        Runtime: DnvmRuntimeOptions;
    }

    /// Default param values
    static member Default = {
        Runtime = DnvmRuntimeOptions.Default
    }

// dnu restore command
/// ## Parameters
///
/// - 'setParams' - set command options
/// - 'project' - project to restore
let DnuRestore setParams project =    
    traceStartTask "Dnu:restore" project
    let options = DnuRestoreParams.Default |> setParams
    let args = sprintf "restore %s" project
    Dnu (fun o -> options.Runtime) args
    traceEndTask "Dnu:restore" project

/// Build configuration
type BuildConfiguration =
    | Debug
    | Release
    | Custom of string


/// Dnu publish command parameters
type DnuPublishParams =
    {
        /// Dnvm runtime options
        Runtime: DnvmRuntimeOptions;
        /// Configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Output path (--output)
        OutputPath: string option;
        /// No source flag (--no-source)
        NoSource: bool;
        /// Quiet flag (--quiet)
        Quiet: bool;
        /// Include symbols flag (--include-symbols)
        IncludeSymbols: bool;
    }

    /// Default parameter values
    static member Default = {
        Runtime = DnvmRuntimeOptions.Default
        Configuration = Release
        OutputPath = None
        NoSource = false
        Quiet = false
        IncludeSymbols = true
    }

/// [omit]
let private buildPublishArgs (param: DnuPublishParams) =
    [  
        sprintf "--configuration %s" 
            (match param.Configuration with
            | Debug -> "Debug"
            | Release -> "Release"
            | Custom config -> config)
        param.OutputPath |> Option.toList |> argList2 "out"
        (if param.NoSource then "--no-source" else "")
        (if param.Quiet then "--quiet" else "")
        (if param.IncludeSymbols then "--include-symbols" else "")
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "

/// dnu publish command
/// ## Parameters
///
/// - 'setParams' - set command options
/// - 'project' - project to restore
let DnuPublish setParams project =    
    traceStartTask "Dnu:publish" project
    let options = DnuPublishParams.Default |> setParams
    let args = sprintf "publish %s %s" project (buildPublishArgs options)
    Dnu (fun o -> options.Runtime) args
    traceEndTask "Dnu:publish" project

/// Dnu pack command parameters
type DnuPackParams =
    {
        /// Dnvm runtime options
        Runtime: DnvmRuntimeOptions;
        /// Configuration (--configuration)
        Configuration: BuildConfiguration;
        /// Output path (--output)
        OutputPath: string option;
    }

    /// Default parameter values
    static member Default = {
        Runtime = DnvmRuntimeOptions.Default
        Configuration = Release
        OutputPath = None
    }

/// [omit]
let private buildPackArgs (param: DnuPackParams) =
    [  
        sprintf "--configuration %s" 
            (match param.Configuration with
            | Debug -> "Debug"
            | Release -> "Release"
            | Custom config -> config)
        param.OutputPath |> Option.toList |> argList2 "out"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// dnu pack command
/// ## Parameters
///
/// - 'setParams' - set command options
/// - 'project' - project to pack
let DnuPack setParams project =    
    traceStartTask "Dnu:pack" project
    let options = DnuPackParams.Default |> setParams
    let args = sprintf "pack %s %s" project (buildPackArgs options)
    Dnu (fun o -> options.Runtime) args
    traceEndTask "Dnu:pack" project

/// get sdk version from global.json
/// ## Parameters
///
/// - 'project' - global.json path
let GlobalJsonSdk project =
    let data = ReadFileAsString project
    let info = JsonValue.Parse(data)
    info?sdk?version.AsString()    

/// set version suffix for 1.0.0-* format
/// ## Parameters
///
/// - 'version' - version suffix to set
let SetDnxVersionSuffix version =
    setEnvironVar "DNX_BUILD_VERSION" version
    