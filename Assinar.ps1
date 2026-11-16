# Creates a local, self-signed code signing certificate. Does not change trust stores.
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'bin\PCmonitor.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Execute Compilar.ps1 primeiro.' }
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object {
    $_.Subject -eq 'CN=PCmonitor Local' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30)
} | Select-Object -First 1
if (!$cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=PCmonitor Local' -FriendlyName 'PCmonitor - assinatura local' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(1)
}
$result = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -HashAlgorithm SHA256
if (!$result.SignerCertificate -or $result.SignerCertificate.Thumbprint -ne $cert.Thumbprint) { throw 'Nao foi possivel incluir a assinatura esperada.' }
Export-Certificate -Cert $cert -FilePath (Join-Path $PSScriptRoot 'bin\PCmonitor-local.cer') -Type CERT | Out-Null
$result | Format-List Path,Status,StatusMessage,SignerCertificate
Write-Warning 'Certificado autoassinado: pode aparecer raiz nao confiavel. Nao garante aceitacao pelo Defender, SmartScreen ou outros antivirus.'
Write-Host 'Nenhuma chave privada foi exportada e nenhuma raiz confiavel foi instalada.'