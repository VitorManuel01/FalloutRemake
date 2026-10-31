<#
.SYNOPSIS
    FalloutRemake — Script de Recuperação Sob Demanda de Assets (Versão Transacional e Segura).
    Copia um modelo GLB e todas as suas texturas associadas de D:\FalloutRemakeAssets
    para C:\Dev\FalloutRemake\Assets preservando a estrutura de diretórios e validando
    integridade criptográfica (SHA-256) pós-cópia com rollback transacional integral.

.DESCRIPTION
    Desenvolvido para Codex e Claude recuperarem assets específicos sem sobrecarregar
    o projeto Godot com milhares de arquivos não utilizados.
    
    Características de Segurança:
    - NÃO sobrescreve arquivos existentes por padrão (exige -Overwrite explícito).
    - Validação transacional: se ocorrer qualquer falha durante a cópia ou na conferência
      de hashes SHA-256, os novos arquivos criados são removidos e os arquivos pré-existentes
      sobrescritos têm seus conteúdos anteriores integralmente restaurados.
    - Valida hashes SHA-256 de todos os arquivos copiados contra a origem no repositório.
    - Restaura texturas WebP associadas (prefixo [stem]*.webp).
    - Restaura arquivos .import correspondentes quando disponíveis na origem.
    - Suporta caminho de destino customizado (-DestinationAssetsPath) para testes isolados em sandbox.

.PARAMETER ModelRelativePath
    Caminho relativo do modelo GLB no repositório externo.
    Exemplo: "Architecture/Shacks/shackwallflat01.glb"

.PARAMETER DestinationAssetsPath
    Caminho raiz de destino dos assets.
    Padrão: "C:\Dev\FalloutRemake\Assets"

.PARAMETER ExternalRepoPath
    Caminho do repositório externo de assets.
    Padrão: "D:\FalloutRemakeAssets"

.PARAMETER Overwrite
    Permite sobrescrever arquivos caso já existam no destino, ativando backup transacional.

.PARAMETER SkipImportFiles
    Se especificado, não copia os arquivos .import de sidecar (o Godot criará no carregamento).

.EXAMPLE
    # Restauração padrão com validação SHA-256:
    .\tools\Restore-Asset.ps1 -ModelRelativePath "Architecture/Shacks/shackwallflat01.glb"

.EXAMPLE
    # Restauração forçando sobrescrita se já existir:
    .\tools\Restore-Asset.ps1 -ModelRelativePath "Architecture/Shacks/shackwallflat01.glb" -Overwrite
#>
[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ModelRelativePath,

    [Parameter(Mandatory = $false)]
    [string]$DestinationAssetsPath = "C:\Dev\FalloutRemake\Assets",

    [Parameter(Mandatory = $false)]
    [string]$ExternalRepoPath = "D:\FalloutRemakeAssets",

    [Parameter(Mandatory = $false)]
    [switch]$Overwrite,

    [Parameter(Mandatory = $false)]
    [switch]$SkipImportFiles
)

$ErrorActionPreference = "Stop"

# 1. Normalização de caminhos
$NormRel = $ModelRelativePath.Replace("/", "\").TrimStart("\")
if (-not $NormRel.ToLower().EndsWith(".glb")) {
    Write-Error "O caminho informado deve apontar para um arquivo .glb: $ModelRelativePath"
}

$SourceGlb = Join-Path $ExternalRepoPath $NormRel

if (-not (Test-Path -LiteralPath $SourceGlb)) {
    Write-Error "Asset GLB não encontrado no repositório externo: $SourceGlb"
}

$DestGlb = Join-Path $DestinationAssetsPath $NormRel
$DestDir = Split-Path -Path $DestGlb -Parent

# 2. Verificar se arquivo já existe no destino sem -Overwrite
if ((Test-Path -LiteralPath $DestGlb) -and (-not $Overwrite)) {
    Write-Error "O arquivo de destino já existe e a flag -Overwrite não foi especificada: $DestGlb`nOperação cancelada para proteção de dados."
}

# 3. Mapear arquivos a serem copiados
$SourceDir = Split-Path -Path $SourceGlb -Parent
$Stem = [System.IO.Path]::GetFileNameWithoutExtension($SourceGlb)

$FilesToCopy = [System.Collections.Generic.List[psobject]]::new()

# Adicionar o GLB
$FilesToCopy.Add([PSCustomObject]@{
    Type = "GLB"
    Source = $SourceGlb
    Dest = $DestGlb
    Relative = $NormRel
})

# Adicionar .import do GLB
if (-not $SkipImportFiles) {
    $GlbImport = "$SourceGlb.import"
    if (Test-Path -LiteralPath $GlbImport) {
        $FilesToCopy.Add([PSCustomObject]@{
            Type = "GLB.import"
            Source = $GlbImport
            Dest = "$DestGlb.import"
            Relative = "$NormRel.import"
        })
    }
}

# Adicionar texturas associadas ($Stem*.webp)
$Textures = Get-ChildItem -LiteralPath $SourceDir -Filter "$Stem*.webp" -File -ErrorAction SilentlyContinue
foreach ($tex in $Textures) {
    $TexDest = Join-Path $DestDir $tex.Name
    $TexRel = Join-Path (Split-Path -Path $NormRel -Parent) $tex.Name
    
    $FilesToCopy.Add([PSCustomObject]@{
        Type = "Texture"
        Source = $tex.FullName
        Dest = $TexDest
        Relative = $TexRel
    })

    if (-not $SkipImportFiles) {
        $TexImport = "$($tex.FullName).import"
        if (Test-Path -LiteralPath $TexImport) {
            $FilesToCopy.Add([PSCustomObject]@{
                Type = "Texture.import"
                Source = $TexImport
                Dest = "$TexDest.import"
                Relative = "$TexRel.import"
            })
        }
    }
}

# Verificar se alguma textura existente impediria cópia sem -Overwrite
if (-not $Overwrite) {
    $ExistingDestFiles = $FilesToCopy | Where-Object { Test-Path -LiteralPath $_.Dest }
    if ($ExistingDestFiles.Count -gt 0) {
        $ExistingList = ($ExistingDestFiles | ForEach-Object { $_.Dest }) -join "`n  "
        Write-Error "Arquivos de destino já existem e a flag -Overwrite não foi especificada:`n  $ExistingList`nOperação cancelada para proteção de dados."
    }
}

# 4. Criar diretório de destino
if (-not (Test-Path -LiteralPath $DestDir)) {
    New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
}

# 5. Backup transacional de arquivos pré-existentes quando -Overwrite é utilizado
$BackupDir = $null
$BackedUpFiles = [System.Collections.Generic.Dictionary[string, string]]::new()

if ($Overwrite) {
    $PreExistingFiles = $FilesToCopy | Where-Object { Test-Path -LiteralPath $_.Dest }
    if ($PreExistingFiles.Count -gt 0) {
        $BackupDir = Join-Path ([System.IO.Path]::GetTempPath()) ("FalloutRemake_RestoreBackup_" + [System.Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
        
        $backupIdx = 0
        foreach ($p in $PreExistingFiles) {
            $backupIdx++
            $backupFile = Join-Path $BackupDir "backup_$backupIdx"
            Copy-Item -LiteralPath $p.Dest -Destination $backupFile -Force
            $BackedUpFiles[$p.Dest] = $backupFile
        }
    }
}

Write-Host "Iniciando recuperação de '$NormRel' ($($FilesToCopy.Count) arquivos mapeados)..."

# Função auxiliar para computar SHA-256
function Get-FileSha256([string]$Path) {
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $bytes = $hasher.ComputeHash($stream)
        return ([System.BitConverter]::ToString($bytes) -replace "-","").ToUpperInvariant()
    } finally {
        $stream.Close()
        $stream.Dispose()
        $hasher.Dispose()
    }
}

# 6. Executar cópia com validação criptográfica e rollback transacional
$CopiedFiles = [System.Collections.Generic.List[string]]::new()
$TotalBytesCopied = 0

try {
    foreach ($item in $FilesToCopy) {
        $SrcHash = Get-FileSha256 -Path $item.Source
        $SrcSize = (Get-Item -LiteralPath $item.Source).Length

        # Copiar
        Copy-Item -LiteralPath $item.Source -Destination $item.Dest -Force
        $CopiedFiles.Add($item.Dest)
        $TotalBytesCopied += $SrcSize

        # Validar destino
        if (-not (Test-Path -LiteralPath $item.Dest)) {
            throw "Falha de I/O: arquivo não foi criado no destino: $($item.Dest)"
        }

        $DestHash = Get-FileSha256 -Path $item.Dest
        if ($DestHash -ne $SrcHash) {
            throw "DIVERGÊNCIA DE INTEGRIDADE SHA-256 em $($item.Relative)! Origem: $SrcHash vs Destino: $DestHash"
        }

        Write-Host "  [OK] $($item.Type): $($item.Relative) ($SrcSize bytes) | SHA-256: $DestHash"
    }
} catch {
    Write-Warning "Falha durante o processo de restauração: $_"
    Write-Warning "Iniciando rollback transacional seguro..."
    
    # 1. Deletar arquivos recém-criados que NÃO existiam antes
    foreach ($f in $CopiedFiles) {
        if (-not $BackedUpFiles.ContainsKey($f)) {
            if (Test-Path -LiteralPath $f) {
                Remove-Item -LiteralPath $f -Force -ErrorAction SilentlyContinue
            }
        }
    }
    
    # 2. Restaurar o conteúdo original exato dos arquivos pré-existentes que foram sobrescritos
    foreach ($origDest in $BackedUpFiles.Keys) {
        $backupPath = $BackedUpFiles[$origDest]
        if (Test-Path -LiteralPath $backupPath) {
            Copy-Item -LiteralPath $backupPath -Destination $origDest -Force -ErrorAction SilentlyContinue
            Write-Host "  [ROLLBACK RESTAURADO] Conteúdo pré-existente restaurado em: $origDest"
        }
    }
    
    if ($BackupDir -and (Test-Path -LiteralPath $BackupDir)) {
        Remove-Item -LiteralPath $BackupDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    Write-Error "Restauração abortada com rollback transacional executado. Causa: $_"
}

# 7. Limpar backup temporário em caso de sucesso
if ($BackupDir -and (Test-Path -LiteralPath $BackupDir)) {
    Remove-Item -LiteralPath $BackupDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`nRestauração concluída com sucesso!"
Write-Host "Total de arquivos restaurados: $($FilesToCopy.Count)"
Write-Host "Total de bytes validados:      $TotalBytesCopied bytes"
Write-Host "Destino:                       $DestGlb"
