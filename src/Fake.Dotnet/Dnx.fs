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

// Dnvm executable path
let private dnvmPath = dnxInstallDir @@ "bin" @@ "dnvm.cmd"

// Temporary path of installer script
let private tempInstallerScript = Path.GetTempPath() @@ "dnvminstall.ps1"

let private downloadInstaller fileName =  
    let installScript = Http.RequestStream dnvmInstaller
    use outFile = File.OpenWrite(fileName)
    installScript.ResponseStream.CopyTo(outFile)
    trace (sprintf "downloaded dnvm installer to %s" fileName)
    fileName


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


type DnvmOptions =
    {
        /// Path to dnvm.cmd
        ToolPath: string;
        /// Command working directory
        WorkingDirectory: string;
        /// Automatically install dnvm if not found
        AutoInstall: bool
    }

    static member Default = {
        ToolPath = dnvmPath
        WorkingDirectory = currentDirectory
        AutoInstall = true
    }

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
let DnvmUpgrade setOptions =    
    traceStartTask "Dnvm" "upgrade"
    let result = Dnvm setOptions "upgrade"    
    if not result.OK then failwithf "dnvm upgrade failed with code %i" result.ExitCode
    traceEndTask "Dnvm" "upgrade"

type DnvmRuntimeOptions =
    {
        /// Common tool options
        Dnvm: DnvmOptions;
        /// Runtime alias
        VersionOrAlias: string;
    }

    static member Default = {
        Dnvm = DnvmOptions.Default
        VersionOrAlias = "default"
    }

/// dnvm exec command
let DnvmExec setOptions command =    
    traceStartTask "Dnvm" "exec"    
    let options = DnvmRuntimeOptions.Default |> setOptions
    let args = sprintf "exec %s %s" options.VersionOrAlias command
    let result = Dnvm (fun o -> options.Dnvm) args
    if not result.OK then failwithf "dnvm exec failed with code %i" result.ExitCode
    traceEndTask "Dnvm" "exec"

/// dnu command
let Dnu setOptions command =    
    traceStartTask "Dnu" "command"    
    let args = sprintf "dnu %s" command
    DnvmExec setOptions args
    traceEndTask "Dnu" "command"


type DnuRestoreParams =
    {
        /// Dnvm runtime options
        Runtime: DnvmRuntimeOptions;
    }

    static member Default = {
        Runtime = DnvmRuntimeOptions.Default
    }

// dnu restore command
let DnuRestore setParams project =    
    traceStartTask "Dnu:restore" project
    let options = DnuRestoreParams.Default |> setParams
    let args = sprintf "restore %s" project
    Dnu (fun o -> options.Runtime) args
    traceEndTask "Dnu:restore" project

let GlobalJsonSdk project =
    let data = ReadFileAsString project
    let info = JsonValue.Parse(data)
    info?sdk?version.AsString()    