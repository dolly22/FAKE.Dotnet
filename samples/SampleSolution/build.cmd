@echo off
pushd %~dp0\scripts

echo * bootstrapping paket package manager...
.paket\paket.bootstrapper.exe 2.50.8 --prefer-nuget
if errorlevel 1 (
  exit /b %errorlevel%
)

echo * restoring build dependencies...
.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

popd

pushd %~dp0

echo * bootstrap complete, nuget restore...
scripts\packages\NuGet.CommandLine\tools\nuget.exe restore -NonInteractive

echo * starting FAKE build...
rem add --break option for debugging
scripts\packages\FAKE.Core\tools\Fake.exe scripts\build.fsx %*

popd