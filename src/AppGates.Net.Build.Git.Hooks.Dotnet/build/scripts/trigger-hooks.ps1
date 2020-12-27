param (
    [string]
    $hookFilePath = "C:\git\collaboration\infrastructure.versioning\src\AppGates.Net.Build.GitHooks.Powershell\build\hooks\pre-commit",

    [bool]
    $invokeDirect = $false
 )
 try
 {
    if($Debug)
    {
        $DebugMode = $true
        $DebugPreference = "Continue"
        Write-Debug "Debugging..."
    }
    else
    {
        $DebugMode = $false
    }

    $gitRepositoryRoot = $PWD


    Write-Debug "hookFilePath: $hookFilePath"
    Write-Debug "gitRepositoryRoot: $gitRepositoryRoot"
    Write-Debug "invokeDirect: $invokeDirect"


    $hookName = Split-Path $hookFilePath -leaf

    Write-Debug "hookName: $hookName"



    [string[]]$Paths = @($gitRepositoryRoot)
    [string[]]$Excludes = @('bin', 'obj')
    $gitHooksFolderName = ".git-ps-hooks"

    $directories = Get-ChildItem $Paths -Directory -Recurse -Filter $gitHooksFolderName

    $hookScripts = New-Object Collections.Generic.List[string]

    foreach ($directory in $directories) {
        $included = $true
        foreach ($exclude in $Excludes) { 
            if ($directory.FullName -ilike "*\$exclude\*" -or $directory.Name -eq $exclude) { 
                $included = $false
                break
            }
        }
        if ($included) {

            $hookScript = Join-Path -Path $directory.FullName -ChildPath "$hookName.ps1"
   
            if(Test-Path -path $hookScript -PathType Leaf){
                $hookScripts.Add($hookScript)
            }
        }
    }

    if($hookScripts.Count -gt 0)
    {
        $runScript="$PSScriptRoot\run-hooks.ps1"
        $scriptsAsArgument =  $($hookScripts -join ",")

        $runScriptArgs = "$runScript -gitRepositoryRoot $gitRepositoryRoot -hookName $hookName -DebugMode $DebugMode -hookScripts $scriptsAsArgument"
        Write-Debug "runScriptArgs: $runScriptArgs"

        if($invokeDirect)
        { 
            iex $runScriptArgs
            $exitCode =$LASTEXITCODE
        }
        else
        {

            $pinfo = New-Object System.Diagnostics.ProcessStartInfo
            $pinfo.FileName = "pwsh.exe"
            $pinfo.RedirectStandardError = $false
            $pinfo.RedirectStandardOutput = $false
            $pinfo.UseShellExecute = $true
            $pinfo.Arguments = $runScriptArgs
            $p = New-Object System.Diagnostics.Process
            $p.StartInfo = $pinfo
            $started = $p.Start()
            if($started -eq $false)
            {
                Write-Host 'Fatal: Starting powershell window failed.' -ForegroundColor Red
            }
            #Do Other Stuff Here....
            $p.WaitForExit()
            $exitCode = $p.ExitCode
        }
    
        if($exitCode -eq 0)
        {
	        Write-Host "$hookName successful!" -ForegroundColor Green
        } 
        else 
        {
	        Write-Host "$hookName Commit failed." -ForegroundColor Red
            exit $exitCode
        }
    }
}
catch
{
    if (!$psISE)
    {
	    Write-Host "Fatal in git hooks powershell error occurred:  $_.Exception.Message" -ForegroundColor Red;;
	    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
    }

    exit 100
}