<#
.SYNOPSIS 
Builds the Adxstudio Portals MSI.

.EXAMPLE
C:\PS> .\SetStrongName.ps1 'C:\adtfs\AdxstudioXrm\Crm5\Stable\Framework\Adxstudio.Xrm\' 'C:\Windows\Microsoft.NET\Framework\v4.0.30319' 'C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\' 'C:\adtfs\AdxstudioXrm\Crm5\Stable\Framework\Adxstudio.Xrm\*.snk' '..\packages\Microsoft.AspNet.Identity.Core.2.2.0\lib\net45\Microsoft.AspNet.Identity.Core.dll;..\packages\Microsoft.AspNet.Identity.Owin.2.2.0\lib\net45\Microsoft.AspNet.Identity.Owin.dll;..\packages\Microsoft.CrmSdk.CoreAssemblies.6.1.1\lib\net45\Microsoft.Crm.Sdk.Proxy.dll' 'Kentor.OwinCookieSaver.dll'"

  <Target Name="BeforeBuild">
    <GetFrameworkPath>
      <Output TaskParameter="Path" PropertyName="FrameworkPath" />
    </GetFrameworkPath>
    <ItemGroup>
      <!-- specify the assemblies that are known to lack a strong name -->
      <UnstrongName Include="Kentor.OwinCookieSaver.dll" />
      <UnstrongName Include="....dll" />
    </ItemGroup>
    <PropertyGroup>
      <SnkPath>$(ProjectDir)*.snk</SnkPath>
      <AssemblyPath>@(Reference->'%(HintPath)')</AssemblyPath>
      <SetStrongNameScriptPath>$(ProjectDir)..\Adxstudio.Xrm.Build\SetStrongName.ps1</SetStrongNameScriptPath>
      <SetStrongNameCommand>$(SetStrongNameScriptPath) '$(ProjectDir)' '$(FrameworkPath)' '$(SDK40ToolsPath)' '$(SnkPath)' '$(AssemblyPath)' '@(UnstrongName)'</SetStrongNameCommand>
    </PropertyGroup>
    <Exec ContinueOnError="false" Command="powershell -ExecutionPolicy Bypass -NoLogo -NonInteractive -NoProfile -WindowStyle Hidden -Command &quot;$(SetStrongNameCommand)&quot;" />
  </Target>
#>

Param (
	[Parameter(Mandatory=$true, Position=0)]
	$projectDir,

	[Parameter(Mandatory=$true, Position=1)]
	$frameworkPath,

	[Parameter(Mandatory=$true, Position=2)]
	$sdk40ToolsPath,

	[Parameter(Mandatory=$true, Position=3)]
	$snkPath,

	[Parameter(Mandatory=$true, Position=4)]
	$assemblyPath,

	[Parameter(Mandatory=$true, Position=5)]
	$filter
)

$VerbosePreference = "Continue"
$ErrorActionPreference = "Stop"

if (-not (Test-Path $snkPath)) {
	throw "SNK file not found."
}

$snPath = Join-Path $sdk40ToolsPath "sn.exe" -Resolve
$ildasmPath = Join-Path $sdk40ToolsPath "ildasm.exe" -Resolve
$ilasmPath = Join-Path $frameworkPath "ilasm.exe" -Resolve
$snkPath = Resolve-Path $snkPath

Write-Output "Use strong name key: $snkPath"

# get all of the assembly paths (semi-colon separated)
$filters = $filter -split ";" | ? { $_ }
$assemblies = $assemblyPath -split ";" | ? { $_ -and $filters -contains [System.IO.Path]::GetFileName($_) } | % { Join-Path -Resolve $projectDir $_ }

Write-Output "Test assemblies for strong name"
$assemblies

$assemblies | % {
	# test if the assembly has a strong name
	& $snPath -q -vf $_
	
	if ($LastExitCode -eq 1) {
		# assembly does not have a strong name
		$origPath = "$_.orig"
		$ilPath = "$_.il"
		# call ildasm
		& $ildasmPath $_ /out:$ilPath /text
		# rename the original assembly
		Move-Item $_ $origPath -force
		# call ilasm with the SNK
		& $ilasmPath $ilPath /dll /key=$snkPath /output=$_
	}
}
