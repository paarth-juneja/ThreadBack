# Read-only check. Run inside the reserved Windows device, not on your own laptop.
$ErrorActionPreference = 'Continue'
$system = Get-CimInstance Win32_ComputerSystem
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor
$report = [ordered]@{
    manufacturer = $system.Manufacturer
    model = $system.Model
    memoryGB = [math]::Round($system.TotalPhysicalMemory / 1GB, 1)
    operatingSystem = $os.Caption
    build = $os.BuildNumber
    processor = ($cpu.Name -join ', ')
    architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { (dotnet --list-runtimes) -join "`n" } else { 'not found' }
    python = if (Get-Command python -ErrorAction SilentlyContinue) { (python --version 2>&1) -join ' ' } else { 'not found' }
    qnnDevices = @(Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.FriendlyName -match 'Hexagon|Neural|NPU|Qualcomm.*AI' } | Select-Object FriendlyName,Status)
}
$report | ConvertTo-Json -Depth 4
