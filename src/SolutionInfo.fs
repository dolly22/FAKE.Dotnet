namespace System
open System.Reflection

[<assembly: AssemblyVersionAttribute("1.0.0")>]
[<assembly: AssemblyFileVersionAttribute("1.1.0")>]
[<assembly: AssemblyInformationalVersionAttribute("1.1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.0"
    let [<Literal>] InformationalVersion = "1.1.0"
