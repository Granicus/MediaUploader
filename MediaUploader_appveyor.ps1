# To run this script locally you need to allow unsigned script to run in powershell via:
# Set-ExecutionPolicy Unrestricted

param([String] $SourceFolder,[String] $BuildConfig, [String] $ClusterName, [String] $SiteName)

# Set sourceFolder relative to path of this currently running script.
$scriptpath = $MyInvocation.MyCommand.Path
$sourceFolder = Split-Path $scriptpath

if($env:APPVEYOR_BUILD_FOLDER)
{
  Write-Host "Starting GranicusMediaUploader build..."

  Write-Host "Appveyor environment variables:"
  Write-Host "Appveyor_ProjectName = $env:APPVEYOR_PROJECT_NAME"
  Write-Host "Appveyor_ProjectVersion = $env:APPVEYOR_BUILD_VERSION"
  Write-Host "Appveyor_ProjectBuildNumber = $env:APPVEYOR_BUILD_NUMBER"
  Write-Host "BuildFolder = $env:APPVEYOR_BUILD_FOLDER"
  Write-Host "SrcFolder = $env:APPVEYOR_BUILD_FOLDER"
  Write-Host "OutFolder = $env:APPVEYOR_BUILD_FOLDER"
  Write-Host "TempFolder = $env:TEMP"
  Write-Host "CommitId = $env:APPVEYOR_REPO_COMMIT"
  Write-Host "RepositoryType = $env:APPVEYOR_REPO_PROVIDER"
  Write-Host "RepositoryName = $env:APPVEYOR_REPO_NAME"
  Write-Host "RepositoryBranch = $env:APPVEYOR_REPO_BRANCH"
  Write-Host "-------------- End of Appveyor environment variables ------------------------"
  Write-Host ""
}
else
{
  #TODO: Check if git is available and if the .git directory exists - if so, use it to replace the defaults.
}

Write-Host "ScriptFolder = $sourceFolder"

$msbuild = "`"${Env:ProgramFiles(x86)}" + "\MSBuild\12.0\Bin\MSBuild.exe`""

Write-Host "MSBUILD = $msbuild"

$build_command = "$msbuild $sourceFolder\MediaUploader.proj"
Write-Host "$build_command"
Invoke-Expression "& $build_command"
$return_value = $LastExitCode

if ($return_value -ne 0)
{
  Write-Host "Process returned: $return_value"
  throw "Error while building solution."
}
