<#
.SYNOPSIS
    Converte modelos NIF e materiais de Fallout: New Vegas para Fallout 4 de forma automatizada e reproduzível.
.DESCRIPTION
    Wrapper PowerShell para o conversor CLI headless de NIF (tools/fnv_to_fo4_converter.py)
    baseado no motor de conversão NifSkope do projeto FNV to FO4 (Nexus 75425).
.PARAMETER InputFiles
    Caminho para um ou mais arquivos .nif originais de Fallout New Vegas.
.PARAMETER OutputDir
    Diretório de destino para os assets convertidos para Fallout 4 (meshes e materials).
.PARAMETER WorkDir
    Diretório sandbox de processamento temporário.
.PARAMETER Manifest
    Caminho opcional para salvar o manifesto JSON de execução com métricas e auditoria.
.EXAMPLE
    .\tools\Convert-FNVtoFO4.ps1 -InputFiles "D:\...\ladder.nif" -OutputDir "D:\...\output"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]]$InputFiles,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$OutputDir,

    [string]$WorkDir = "D:\Downloads\Fallout Tools\automation_sandbox",

    [string]$Manifest = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonScript = Join-Path $scriptDir "fnv_to_fo4_converter.py"

if (-not (Test-Path $pythonScript)) {
    Write-Error "Script Python não encontrado em: $pythonScript"
    exit 1
}

$cmdArgs = @("python", "`"$pythonScript`"")
foreach ($f in $InputFiles) {
    $cmdArgs += "`"$f`""
}
$cmdArgs += "-o", "`"$OutputDir`""
if ($WorkDir) {
    $cmdArgs += "-w", "`"$WorkDir`""
}
if ($Manifest) {
    $cmdArgs += "-m", "`"$Manifest`""
}

Write-Host "Iniciando conversão automatizada FNV to FO4..." -ForegroundColor Cyan
$cmdLine = $cmdArgs -join " "
Write-Verbose "Executando: $cmdLine"

Invoke-Expression $cmdLine
if ($LASTEXITCODE -ne 0) {
    Write-Error "A conversão falhou com código de saída $LASTEXITCODE."
    exit $LASTEXITCODE
}

Write-Host "Conversão concluída com sucesso." -ForegroundColor Green
