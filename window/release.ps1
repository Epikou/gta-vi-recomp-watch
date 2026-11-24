# Publie une version : compile, calcule l'empreinte, cree la version GitHub avec ses deux fichiers.
#
#   1. changer le numero dans Updater.cs (const Version)
#   2. commit + push
#   3. powershell -File window\release.ps1
#
# Les programmes deja installes lisent version.txt de la derniere version publiee ; si le numero est
# plus recent que le leur, ils telechargent GtaPcWatch.exe, controlent son empreinte et se remplacent.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$m = Select-String -Path "$here\Updater.cs" -Pattern 'const string Version = "(\d+\.\d+\.\d+)"'
if (-not $m) { throw "numero de version introuvable dans Updater.cs" }
$version = $m.Matches[0].Groups[1].Value

cmd /c "`"$here\build.cmd`""
if ($LASTEXITCODE -ne 0) { throw "la compilation a echoue" }

$exe = "$here\GtaPcWatch.exe"
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLower()
$out = Join-Path $env:TEMP "gtapcwatch-release-$version"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item $exe "$out\GtaPcWatch.exe" -Force
[IO.File]::WriteAllText("$out\version.txt", "$version $hash`n", (New-Object Text.UTF8Encoding $false))

gh release create "v$version" "$out\GtaPcWatch.exe" "$out\version.txt" --repo Epikou/gta-vi-recomp-watch --title "v$version" --generate-notes
if ($LASTEXITCODE -ne 0) { throw "la publication a echoue" }
"Version $version publiee, empreinte $hash"
