$localSdk=Join-Path (Split-Path $PSScriptRoot -Parent) 'tools/dotnet/dotnet.exe'
if(Test-Path -LiteralPath $localSdk){$localSdk}
else {
    $sdk=Get-Command dotnet -ErrorAction SilentlyContinue
    if(!$sdk){throw '請安裝 .NET 10 SDK（版本見 global.json），並確認 dotnet 位於 PATH。'}
    $sdk.Source
}
