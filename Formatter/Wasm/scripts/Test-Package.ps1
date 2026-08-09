# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string] $PackageDirectory
)

function Stop-Workflow
{
    param (
        [Parameter(Mandatory)]
        [string] $Message
    )

    "::error::$Message"
    throw $Message
}

function Assert-PackageField
{
    param (
        [Parameter(Mandatory)]
        [string] $Name,

        [AllowNull()]
        [object] $Actual,

        [Parameter(Mandatory)]
        [string] $Expected
    )

    if ([string] $Actual -cne $Expected)
    {
        $actualValue = if ($null -eq $Actual)
        {
            "<missing>"
        }
        else
        {
            "'$Actual'"
        }
        Stop-Workflow "$Name must be '$Expected'; found $actualValue."
    }
}

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container))
{
    Stop-Workflow "Package directory '$PackageDirectory' does not exist."
}

$packagePath = Join-Path $PackageDirectory "package.json"
try
{
    $package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
}
catch
{
    Stop-Workflow "Reading package metadata from '$packagePath' failed: $($_.Exception.Message)"
}

Assert-PackageField "types" $package.types "./index.d.ts"
Assert-PackageField "exports[.].types" $package.exports.".".types "./index.d.ts"
Assert-PackageField "exports[.].default" $package.exports.".".default "./index.mjs"
Assert-PackageField "exports[./package.json]" $package.exports."./package.json" "./package.json"
Assert-PackageField "imports[#pkg]" $package.imports."#pkg" "./package.json"

Push-Location $PackageDirectory
try
{
    @'
import { format } from "./index.mjs";
const first = await format("IF($x-EQ 1){'yes'}");
const second = await format(first.text);
if (first.errors.length || second.text !== first.text) process.exit(1);
'@ | node --input-type=module

    if ($LASTEXITCODE -ne 0)
    {
        Stop-Workflow "The Node.js formatter validation failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}
