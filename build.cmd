@echo off
pushd %~dp0

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

echo * starting FAKE build...
rem add --break option for debugging
packages\FAKE\tools\Fake.exe build.fsx %*

popd