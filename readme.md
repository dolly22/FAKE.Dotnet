# FAKE.Dotnet

"FAKE.Dotnet" is FAKE build automation system extension for [dotnet cli](http://github.com/dotnet/cli) and it's predecessor [DNX](https://github.com/aspnet/dnx).

See the [API documentation](http://dolly22.github.io/fake.dotnet/index.html) for all available helpers.

## dotnet cli usage

* DotnetCliInstall - install dotnet cli if needed
* Dotnet - generic dotnet command helper
* DotnetRestore - dotnet restore dependencies helper
* DotnetPack - dotnet pack helper

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

* DnvmInstall - install dnvm if needed
* Dnvm - generic dnvm command helper
* DnvmUpgrade - dnvm upgrade command helper
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
