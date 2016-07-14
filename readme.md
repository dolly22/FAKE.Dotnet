# FAKE.Dotnet

[![NuGet Status](http://img.shields.io/nuget/v/FAKE.dotnet.svg?style=flat)](https://www.nuget.org/packages/FAKE.Dotnet/)

"FAKE.Dotnet" is FAKE build automation system extension for [.NET Core CLI](http://github.com/dotnet/cli) and it's predecessor [DNX](https://github.com/aspnet/dnx). It contains helpers to download and install specific versions .NET Core SDK with CLI. Currently supports only windows.

See the [API documentation](http://dolly22.github.io/FAKE.Dotnet/apidocs) for all available helpers and [Release Notes](https://github.com/dolly22/FAKE.Dotnet/blob/master/release_notes.md) for version details

## .NET Core CLI (SDK) helper usage

.NET Core SDK is downloaded and installed using [dotnet-install.ps1](https://github.com/dotnet/cli/blob/rel/1.0.0/scripts/obtain/dotnet-install.ps1) powershell script. By default SDK is installed to `%LOCALAPPDATA%\Microsoft\dotnet` folder (and version subfolders).

* DotnetDownloadInstaller - just download .NET Core SDK installer script
* DotnetCliInstall - install .NET Core SDK (when needed)
* Dotnet - generic dotnet CLI command helper
* DotnetRestore - dotnet restore packages helper
* DotnetPack - dotnet pack helper
* DotnetPublish - dotnet publish helper
* DotnetCompile - dotnet compile helper
* GlobalJsonSdk - determine sdk tooling version from global.json file

### example

There are sample projects and build script for [preview2](https://github.com/dolly22/FAKE.Dotnet/blob/master/samples/DotnetSamplePreview2/scripts/build.fsx) and [preview3](https://github.com/dolly22/FAKE.Dotnet/blob/master/samples/DotnetSamplePreview3/scripts/build.fsx) tooling

```fsharp
#r "tools/FAKE.Dotnet/tools/Fake.Dotnet.dll" // include Fake.Dotnet lib
open Fake.Dotnet

Target "Initialize" (fun _ ->
	DotnetCliInstall id
)

Target "BuildProjects" (fun _ ->
	  !! "src/*/project.json"
	  |> Seq.iter(fun proj ->
		  DotnetRestore id proj
		  DotnetPack (fun c ->
			  { c with
				  Configuration = Debug;
				  OutputPath = Some (currentDirectory @@ "artifacts")
			  }) proj
	  )
)

"Initialize"            // define the dependencies
	  ==> "BuildProjects"

Run "BuildProjects"
```

## DNX usage

* DnvmToolInstall - install dnvm if needed
* Dnvm - generic dnvm command helper
* DnvmInstall - dnvm install command helper
* DnvmUpgrade - dnvm upgrade command helper
* DnvmUpdateSelf - dnvm update-self command helper
* DnvmExec - dnvm exec command helper
* Dnu - generic dnu command helper
* DnuRestore - dnu restore dependencies helper
* DnuPublish - dnu publish command helper
* DnuPack - dnu pack command helper


### Example usage - DNX
```fsharp
#r "tools/FAKE.Dotnet/tools/Fake.Dotnet.dll" // include Fake.Dotnet lib
open Fake.Dotnet

Target "Initialize" (fun _ ->
	DnvmUpgrade id
)

Target "BuildProjects" (fun _ ->
	  !! "src/*/project.json"
	  |> Seq.iter(fun proj ->
		  DnuRestore id proj
		  DnuPack (fun c ->
			  { c with
				  Configuration = Debug;
				  OutputPath = Some (currentDirectory @@ "artifacts")
			  }) proj
	  )
)

"Initialize"            // define the dependencies
	  ==> "BuildProjects"

Run "BuildProjects"
```


# Build the project

* Windows: Run *build.cmd*
