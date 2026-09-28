# sync_packets.ps1
# Copia shared/packets/ -> scripts/packets/ de cada projeto Godot do repo.
#
# Quando rodar:
#   - Antes de testar qualquer projeto se voce editou algo em shared/packets/.
#   - Sempre que mudar a assinatura/encode/decode de um pacote (precisa rebatizar
#     nos 3 lados juntos para nao quebrar contrato).
#
# Como funciona:
#   - Source: shared/packets/ na raiz do repo (este script).
#   - Targets: server/, professor/, client/prototype/ (cada um com sua subpasta
#     scripts/packets/).
#   - Cria scripts/packets/ se nao existir; ignora target inexistente (warning
#     verde) - assim funciona durante a migracao quando alguns projetos ainda
#     nao foram criados.
#   - Copia recursivo + sobrescreve (Force). NAO espelha (renomear um pacote em
#     shared/ nao remove a copia velha do target - apague manualmente se quiser).

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

# Cada item: Source (subpasta de shared/), e os targets onde a copia deve ir.
# Source skipada se nao existir; target skipado se a raiz do projeto nao existe.
$syncs = @(
    @{
        Source  = "shared\packets"
        Targets = @(
            @{ Name = "server";    Path = "server\scripts\packets" },
            @{ Name = "professor"; Path = "professor\scripts\packets" },
            @{ Name = "client";    Path = "client\prototype\scripts\packets" }
        )
    },
    @{
        Source  = "shared\data"
        Targets = @(
            @{ Name = "professor"; Path = "professor\data" },
            @{ Name = "client";    Path = "client\prototype\data" }
        )
    }
)

function Get-ProjectRoot([string]$fullTarget) {
    # client/prototype/scripts/packets -> client/prototype (3 levels up para
    # quem tem prototype/, 2 para quem nao tem). Heuristica: procura pela
    # primeira pasta acima que contenha project.godot.
    $cur = Split-Path -Path $fullTarget -Parent
    while ($null -ne $cur -and $cur.Length -gt $repoRoot.Length) {
        if (Test-Path (Join-Path $cur "project.godot")) { return $cur }
        $cur = Split-Path -Path $cur -Parent
    }
    return $null
}

foreach ($sync in $syncs) {
    $source = Join-Path $repoRoot $sync.Source
    if (-not (Test-Path $source)) {
        Write-Host "  [skip src] $($sync.Source) nao existe" -ForegroundColor Yellow
        continue
    }
    foreach ($target in $sync.Targets) {
        $fullTarget = Join-Path $repoRoot $target.Path
        $projectRoot = Get-ProjectRoot $fullTarget
        if ($null -eq $projectRoot) {
            Write-Host "  [skip]   $($target.Name) ($($sync.Source)): project.godot nao encontrado em $($target.Path)" -ForegroundColor Yellow
            continue
        }
        if (-not (Test-Path $fullTarget)) {
            New-Item -ItemType Directory -Force -Path $fullTarget | Out-Null
        }
        Copy-Item -Path "$source\*" -Destination $fullTarget -Recurse -Force
        Write-Host "  [ok]     $($target.Name) ($($sync.Source)) -> $($target.Path)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "sync concluido." -ForegroundColor Cyan
