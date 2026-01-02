$old='C:\Program Files\dotnet\dotnet.exe'
$new='C:\Program Files\dotnet'
$userPath=[Environment]::GetEnvironmentVariable('Path','User')
if ($null -eq $userPath) { $userPath='' }
$parts = $userPath -split ';' | Where-Object {$_ -ne ''}
$changed=$false
for ($i=0; $i -lt $parts.Count; $i++) {
    if ($parts[$i] -eq $old) { $parts[$i]=$new; $changed=$true }
}
if (-not ($parts -contains $new)) { $parts += $new; $changed=$true }
if ($changed) {
    $newUserPath=($parts -join ';')
    [Environment]::SetEnvironmentVariable('Path',$newUserPath,'User')
    Write-Output 'UPDATED_USER_PATH'
    Write-Output $newUserPath
} else {
    Write-Output 'NO_CHANGE_NEEDED'
}
# Atualiza a sessão atual combinando User + Machine (evita duplicatas)
$machine=[Environment]::GetEnvironmentVariable('Path','Machine')
$user=[Environment]::GetEnvironmentVariable('Path','User')
$combined = (($user -split ';' | Where-Object {$_ -ne ''}) + ($machine -split ';' | Where-Object {$_ -ne ''}))
$seen=@{}
$unique=@()
foreach ($p in $combined) { if (-not $seen.ContainsKey($p)) { $seen[$p]=1; $unique += $p } }
$env:PATH = $unique -join ';'
Write-Output 'SESSION_PATH_UPDATED'
where.exe dotnet
Write-Output '---'
dotnet --info
