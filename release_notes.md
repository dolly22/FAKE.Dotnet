#### 2.0.0
* dropped support for DNX projects
* removed Preview2ToolingOptions and Preview2Tooling101Options
* removed GlobalJsonSdk helper
* added shortcut versions helpers for 1.0.0 and 1.0.1 .NET Core SDKs (NetCore100SdkOptions, NetCore101SdkOptions)
* renamed DotnetCompile to DotnetBuild
* updated Dotnet* helpers to match current rtw cli options (now based on msbuild)
* multiple renames to rebrand some DotnetCli helpers to DotnetSdk
* make CommonOptions function

#### 1.1.1
* add preview2 tooling .NET Core 1.0.1 shortcut (Preview2Tooling101Options)

#### 1.1.0
* added explicit download installer helper (DotnetDownloadInstaller)
* add preview2 tooling shortcut (see preview2 sample)

#### 1.0.0-rc2 - TBD
* Initial version released to nuget
* compatible with donet cli rc2
