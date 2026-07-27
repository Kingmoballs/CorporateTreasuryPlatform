[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$platformAdminFirstName = "Adebola"
$platformAdminLastName = "Mobolaji"
$platformAdminEmail =
    "mobolajisamuel07@gmail.com"

$repositoryRoot =
    Split-Path -Parent $PSScriptRoot

$apiProject =
    Join-Path $repositoryRoot `
        "Treasury.Api\Treasury.Api.csproj"

Write-Host "Building the API before bootstrap..."

dotnet build $apiProject --no-restore

if ($LASTEXITCODE -ne 0)
{
    throw "The API build failed. PlatformAdmin was not created."
}

$securePassword =
    Read-Host `
        "Enter the PlatformAdmin password" `
        -AsSecureString

$secureConfirmation =
    Read-Host `
        "Confirm the PlatformAdmin password" `
        -AsSecureString

$passwordCredential =
    [System.Management.Automation.PSCredential]::new(
        "bootstrap",
        $securePassword)

$confirmationCredential =
    [System.Management.Automation.PSCredential]::new(
        "bootstrap",
        $secureConfirmation)

$plainPassword =
    $passwordCredential.GetNetworkCredential().Password

$plainConfirmation =
    $confirmationCredential.GetNetworkCredential().Password

if ($plainPassword -cne $plainConfirmation)
{
    throw "The password confirmation does not match."
}

$hasUppercase =
    [System.Text.RegularExpressions.Regex]::IsMatch(
        $plainPassword,
        "[A-Z]")

$hasLowercase =
    [System.Text.RegularExpressions.Regex]::IsMatch(
        $plainPassword,
        "[a-z]")

$hasNumber =
    [System.Text.RegularExpressions.Regex]::IsMatch(
        $plainPassword,
        "[0-9]")

$hasSpecialCharacter =
    [System.Text.RegularExpressions.Regex]::IsMatch(
        $plainPassword,
        "[^A-Za-z0-9]")

if ($plainPassword.Length -lt 12 -or
    $plainPassword.Length -gt 128 -or
    -not $hasUppercase -or
    -not $hasLowercase -or
    -not $hasNumber -or
    -not $hasSpecialCharacter)
{
    throw (
        "The password must contain 12-128 characters, " +
        "including uppercase, lowercase, numeric and " +
        "special characters."
    )
}

$bootstrapSettings =
    [ordered]@{
        "PlatformAdminBootstrap__Enabled" =
            "true"
        "PlatformAdminBootstrap__FirstName" =
            $platformAdminFirstName
        "PlatformAdminBootstrap__LastName" =
            $platformAdminLastName
        "PlatformAdminBootstrap__Email" =
            $platformAdminEmail
        "PlatformAdminBootstrap__Password" =
            $plainPassword
    }

$previousSettings = @{}

try
{
    foreach ($name in $bootstrapSettings.Keys)
    {
        $existing =
            Get-Item `
                -LiteralPath "Env:$name" `
                -ErrorAction SilentlyContinue

        $previousSettings[$name] =
            if ($null -eq $existing)
            {
                $null
            }
            else
            {
                $existing.Value
            }

        Set-Item `
            -LiteralPath "Env:$name" `
            -Value $bootstrapSettings[$name]
    }

    Write-Host (
        "Creating and verifying PlatformAdmin " +
        "$platformAdminEmail..."
    )

    dotnet run `
        --project $apiProject `
        --no-build `
        -- `
        --BootstrapPlatformAdminOnly=true

    if ($LASTEXITCODE -ne 0)
    {
        throw (
            "PlatformAdmin bootstrap failed. Review the " +
            "error above; no password was saved by this script."
        )
    }

    Write-Host (
        "PlatformAdmin created and verified successfully. " +
        "The bootstrap settings have been disabled."
    ) -ForegroundColor Green
}
finally
{
    foreach ($name in $bootstrapSettings.Keys)
    {
        if ($null -eq $previousSettings[$name])
        {
            Remove-Item `
                -LiteralPath "Env:$name" `
                -ErrorAction SilentlyContinue
        }
        else
        {
            Set-Item `
                -LiteralPath "Env:$name" `
                -Value $previousSettings[$name]
        }
    }

    $plainPassword = $null
    $plainConfirmation = $null
    $securePassword = $null
    $secureConfirmation = $null
    $passwordCredential = $null
    $confirmationCredential = $null
}
