# Restart-Noah.ps1

$client = "NoahClient"
$server = "NoahServer"
$util   = "NoahServerUtil"   # présent à partir de Noah 4.117
$proc   = "noah4"

$timeoutSec = 60      # timeout global (ex: 120)
$pollMs     = 400     # polling global

function Wait-Status([string]$name, [string]$wanted) {
  $end = (Get-Date).AddSeconds($timeoutSec)
  while((Get-Date) -lt $end) {
    $s = Get-Service -Name $name -ErrorAction SilentlyContinue
    if($s -and $s.Status.ToString() -eq $wanted) { return $true }
    Start-Sleep -Milliseconds $pollMs
  }
  return $false
}

# 0) Vérifier que les services existent
if(-not (Get-Service -Name $client -ErrorAction SilentlyContinue)) { exit 10 } # client absent
if(-not (Get-Service -Name $server -ErrorAction SilentlyContinue)) { exit 11 } # serveur absent

# Service optionnel (Noah >= 4.117)
$hasUtil = $null -ne (Get-Service -Name $util -ErrorAction SilentlyContinue)

# 1) Fermer Noah si lancé
$p = Get-Process -Name $proc -ErrorAction SilentlyContinue
if($p){ $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }; Start-Sleep -Seconds 2 }
$p = Get-Process -Name $proc -ErrorAction SilentlyContinue
if($p){ $p | Stop-Process -Force; Start-Sleep -Seconds 1 }

Start-Sleep -Seconds 1

# 2) Stop Client
Stop-Service $client -Force -ErrorAction SilentlyContinue
if(-not (Wait-Status $client "Stopped")) { exit 1 }

# 3) Stop Server
Stop-Service $server -Force -ErrorAction SilentlyContinue
if(-not (Wait-Status $server "Stopped")) { exit 2 }

# 4) Stop NoahServerUtil (si présent)
if($hasUtil) 
{
  Stop-Service $util -Force -ErrorAction SilentlyContinue
  if(-not (Wait-Status $util "Stopped")) { exit 3 }
}

Start-Sleep -Seconds 1

# 5) Start NoahServerUtil (si présent)
if($hasUtil) 
{
  Start-Service $util -ErrorAction SilentlyContinue
  if(-not (Wait-Status $util "Running")) { exit 4 }
}

# 6) Start Server
Start-Service $server -ErrorAction SilentlyContinue
if(-not (Wait-Status $server "Running")) { exit 5 }


# 7) Start Client
Start-Service $client -ErrorAction SilentlyContinue
if(-not (Wait-Status $client "Running")) { exit 6 }

exit 0