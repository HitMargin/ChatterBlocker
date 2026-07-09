$ErrorActionPreference = 'Continue'
$OutputEncoding = [System.Text.Encoding]::UTF8

try {
    $result = & dotnet build 'd:\Projects\ChatterBlocker\ChatterBlocker\ChatterBlocker.csproj' -c Release 2>&1
    $result | Out-File -FilePath 'd:\Projects\ChatterBlocker\build_output.txt' -Encoding UTF8
    $LASTEXITCODE | Out-File -FilePath 'd:\Projects\ChatterBlocker\build_exitcode.txt' -Encoding UTF8
}
catch {
    $_.Exception.Message | Out-File -FilePath 'd:\Projects\ChatterBlocker\build_output.txt' -Encoding UTF8
    '99' | Out-File -FilePath 'd:\Projects\ChatterBlocker\build_exitcode.txt' -Encoding UTF8
}