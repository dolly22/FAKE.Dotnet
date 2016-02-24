# FAKE - dotnet cli

"FAKE - dotnet cli" is FAKE build automation system extension for [dotnet cli](http://github.com/dotnet/cli).

See the [API documentation](http://dolly22.github.io/fake.dotnet/index.html).


### Simple Example
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


# Build the project

* Windows: Run *build.cmd*

[![NuGet Status](http://img.shields.io/nuget/v/FAKE.dotnet.svg?style=flat)](https://www.nuget.org/packages/FAKE.Dotnet/)
[![Nuget](https://img.shields.io/nuget/dt/FAKE.dotnet.svg)](http://nuget.org/packages/FAKE.Dotnet)
