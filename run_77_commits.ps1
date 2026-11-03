$items = (git status --porcelain -u)
$total = $items.Count
Write-Host "Iniciando proceso para $total cambios..."

$i = 1
foreach ($line in $items) {
    $status = $line.Substring(0, 2).Trim()
    $path = $line.Substring(3).Trim('"')
    $leaf = Split-Path $path -Leaf

    if ($status -eq "D") {
        $msg = "chore(cleanup): remove $leaf"
        git rm "$path"
    } elseif ($status -eq "??") {
        $msg = "feat(assets): add $leaf"
        git add "$path"
    } else {
        $msg = "refactor: update $leaf"
        git add "$path"
    }

    Write-Host "[$i/$total] Commit y push: $leaf"
    git commit -m "$msg"
    git push origin main
    $i++
}
Write-Host "Completados los $total commits y pushes exitosamente."
