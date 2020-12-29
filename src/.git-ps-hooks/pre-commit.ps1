#echo "$PWD"
#echo "$PSScriptRoot"
#echo "$gitRepositoryRoot"
#cd $PSScriptRoot
#cd ..
#$solutions = Resolve-Path  "$PWD\*.sln" | Select -ExpandProperty Path
#foreach($solution in $solutions)
#{
#echo $solution
#dotnet build $solution
#}
#echo $LASTEXITCODE 
#echo "Hello 1 from powershell commit"
#sleep 1 
#echo "Hello 2 from pre commit"
#sleep 1 
#echo "Hello 3 from pre commit"

#exit $LASTEXITCODE 