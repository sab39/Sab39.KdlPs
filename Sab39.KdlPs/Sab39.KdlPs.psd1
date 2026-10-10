@{
    RootModule = 'Sab39.KdlPs.dll'
    ModuleVersion = '0.1.0'
    GUID = 'b9b0084e-8fa3-4fe3-82fb-d6decfc63850'
    Author = 'Stuart Ballard'
    Description = 'Reads and writes KDL documents.'

    PowerShellVersion = '7.6'
    CompatiblePSEditions = @('Core')

    FormatsToProcess = @('Sab39.KdlPs.format.ps1xml')

    # Explicit lists rather than wildcards, so PowerShell can tell what the module exports without loading it.
    CmdletsToExport = @('Get-KdlNode')
    FunctionsToExport = @()
    AliasesToExport = @()
    VariablesToExport = @()
}
