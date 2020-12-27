#!/bin/sh

if ! command -v pwsh &> /dev/null
then
    echo "Powershell not found. Will be installed..."
    dotnet tool update --global PowerShell
fi

pwsh $0/../trigger-hooks.ps1 $1
