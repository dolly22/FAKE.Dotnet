# FAKE.Dotnet

"FAKE.Dotnet" is FAKE build automation system extension for [.NET Core SDK](http://github.com/dotnet/cli) and it's predecessor [DNX](https://github.com/aspnet/dnx). Currently supports only windows.

See the [API documentation](http://dolly22.github.io/FAKE.Dotnet/apidocs) for all available helpers.

## .NET Core SDK helper usage

* DotnetDownloadInstaller - download .NET Core SDK installer
* DotnetCliInstall - install .NET Core SDK if needed
* Dotnet - generic dotnet cli command helper
* DotnetRestore - dotnet restore dependencies helper
* DotnetPack - dotnet pack helper
* DotnetPublish - dotnet publish helper
* DotnetCompile - dotnet compile helper

### example
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

[![NuGet Status](http://img.shields.io/nuget/v/FAKE.dotnet.svg?style=flat)](https://www.nuget.org/packages/FAKE.Dotnet/)
[![Nuget](https://img.shields.io/nuget/dt/FAKE.dotnet.svg)](http://nuget.org/packages/FAKE.Dotnet)
