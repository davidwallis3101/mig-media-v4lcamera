[CmdLetBinding()]
param
(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$TargetDir,

    [Parameter(Mandatory = $true, Position = 1)]
    [string]$ProjectName,

    [Parameter(Mandatory = $true, Position = 2)]
    [string]$TargetFileName,

    [Parameter(Mandatory = $true, Position = 3)]
    [string]$ProjectDir

)

$TargetDir = $TargetDir.Replace('"',"")
$ProjectDir = $ProjectDir.Trim()

function Test-Net45 {
    if (Test-Path ‘HKLM:SOFTWAREMicrosoftNET Framework SetupNDPv4Full’) {
        if (Get-ItemProperty ‘HKLM:SOFTWAREMicrosoftNET Framework SetupNDPv4Full’ -Name Release -ErrorAction SilentlyContinue) {
            return $True
        }
    }
    return $False
}

if (Test-Net45 -eq $false) {Write-Error ".Net 4.5 is required to create the archive." -ErrorAction stop}
Add-Type -assembly "system.io.compression.filesystem" -ErrorAction stop

$destination = "$($ProjectDir)Output\$($ProjectName).zip"

# Determining assembly version which will be used version the interface
$file = "$($TargetDir)\$($TargetFileName)"

$assembly = [System.Reflection.Assembly]::LoadFile($file)
$v = $assembly.GetName().Version;
$version = [string]::Format("{0}.{1:00}.{2:00}.{3}",$v.Major, $v.Minor, $v.Build, $v.Revision)

Write-Host "Replacing Date and Version in $TargetDir\Package.json"
(Get-Content "$TargetDir\Package.json").replace('!DO_NOT_EDIT_RELEASEDATE!', (Get-Date)) | Set-Content "$TargetDir\Package.json"
(Get-Content "$TargetDir\Package.json").replace('!DO_NOT_EDIT_VERSION!', $version) | Set-Content "$TargetDir\Package.json"

Write-Host "Replacing Date and Version in $TargetDir\Readme.TXT"
(Get-Content "$TargetDir\Readme.TXT").replace('!DO_NOT_EDIT_RELEASEDATE!', (Get-Date)) | Set-Content "$TargetDir\Readme.TXT"
(Get-Content "$TargetDir\Readme.TXT").replace('!DO_NOT_EDIT_VERSION!', $version) | Set-Content "$TargetDir\Readme.TXT"

Write-Host "Replace Version in $TargetDir\configlet.html"
(Get-Content "$TargetDir\configlet.html").replace('!DO_NOT_EDIT_VERSION!', $version) | Set-Content "$TargetDir\configlet.html"

#Create Output folder if it doesn't exist
If ((Test-path "$($ProjectDir)Output") -eq $false) { New-Item -ItemType Directory -Path "$($ProjectDir)Output" -Verbose -Force | Out-Null }

If(Test-path $destination) {
    Write-Host "Removing existing interface archive"
    Remove-item $destination -Verbose -Force
}

Write-Host "Creating new interface archive from $TargetDir"
[io.compression.zipfile]::CreateFromDirectory($TargetDir, $destination)

Write-Host ("Interface zip for version {0} created, path: {1}`n" -f $version, $destination)