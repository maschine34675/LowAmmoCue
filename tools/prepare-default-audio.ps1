param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../audio/lowammo.wav')
)
$ErrorActionPreference = 'Stop'
if ((Get-FileHash -LiteralPath $InputPath -Algorithm SHA256).Hash -ne 'EC54CFA33DA8A72B17913987170D3F787AC0BFA88F5E87B89B7BE832D4826590') {
    throw 'Input does not match the verified Freesound original.'
}
$sourceBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $InputPath))
$chunkOffset = 12
$dataOffset = -1
while ($chunkOffset + 8 -le $sourceBytes.Length) {
    $chunkTag = [Text.Encoding]::ASCII.GetString($sourceBytes, $chunkOffset, 4)
    $chunkSize = [BitConverter]::ToUInt32($sourceBytes, $chunkOffset + 4)
    if ($chunkTag -eq 'data') { $dataOffset = $chunkOffset + 8; break }
    $chunkOffset += 8 + $chunkSize + ($chunkSize % 2)
}
if ($dataOffset -lt 0 -or $dataOffset + 72000 -gt $sourceBytes.Length) { throw 'PCM data missing.' }
# Canonical PCM24 stereo header followed by the original first 12000 frames.
$targetStream = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath))
$writer = [IO.BinaryWriter]::new($targetStream)
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
    $writer.Write([uint32](36 + 72000))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $writer.Write([uint32]16)
    $writer.Write([uint16]1); $writer.Write([uint16]2)
    $writer.Write([uint32]48000); $writer.Write([uint32]288000)
    $writer.Write([uint16]6); $writer.Write([uint16]24)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
    $writer.Write([uint32]72000)
    $writer.Write($sourceBytes, $dataOffset, 72000)
}
finally { $writer.Dispose() }
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
