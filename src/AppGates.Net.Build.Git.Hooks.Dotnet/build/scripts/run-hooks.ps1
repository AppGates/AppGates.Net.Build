param (
    [Parameter(Mandatory=$true)]
    [string]
    $gitRepositoryRoot,

    [Parameter(Mandatory=$true)]
    [string]
    $hookName,
     
    [Parameter(Mandatory=$true)]
    [string]
    $DebugMode,

    [parameter(Mandatory=$true)]
    [ValidateNotNull()]
    $hookScripts

 )
 
 try
 {
    if($Debug -or $DebugMode -eq "True")
    {
        $DebugPreference = "Continue"
        Write-Debug "Debugging..."
    }

    Write-Debug "Script process started"
    Write-Debug "gitRepositoryRoot: $gitRepositoryRoot"
    Write-Debug "hookName: $hookName"
    Write-Debug "hookScripts: $hookScripts"



    foreach($hookScript in $hookScripts.Split(","))
    {
        $hookScriptContext = (get-item $hookScript ).Directory.Parent.FullName;
        Write-Debug "hookScriptContext: $hookScriptContext"
        cd $hookScriptContext
        iex $hookScript
        if ((Test-Path variable:LASTEXITCODE) -and $LASTEXITCODE -ne 0)
        {
	        Write-Host "The $hookName script $hookScript failed with error code $LASTEXITCODE." -ForegroundColor Red;

            if (!$psISE)
            {
	            Write-Host "Press any key to exit...";
	            $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
            }
            exit $LASTEXITCODE
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