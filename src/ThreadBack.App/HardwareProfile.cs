using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ThreadBack.App;

public sealed record HardwareProfile(string GpuName, string NpuName, double RamGb, string GpuRuntimeDevice, string NpuRuntimeDevice, bool NpuModelValidated)
{
    public bool GpuAvailable => GpuName.Length > 0 && GpuRuntimeDevice.Length > 0;
    public bool NpuAvailable => NpuName.Length > 0 && NpuRuntimeDevice.Length > 0 && NpuModelValidated;

    public static HardwareProfile Probe(string root)
    {
        var gpu = ""; var npu = ""; var ram = 0.0;
        try
        {
            const string script = "$gpu = @(Get-CimInstance Win32_VideoController -ErrorAction Stop | Where-Object Name | ForEach-Object Name) -join ', '; $npu = @(Get-PnpDevice -PresentOnly -Class ComputeAccelerator -ErrorAction SilentlyContinue | Where-Object Status -eq 'OK' | ForEach-Object FriendlyName) -join ', '; $ram = (Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).TotalPhysicalMemory; [pscustomobject]@{ gpu=$gpu; npu=$npu; ram=$ram } | ConvertTo-Json -Compress";
            var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            if (process.WaitForExit(8000) && process.ExitCode == 0)
            {
                using var json = JsonDocument.Parse(output);
                gpu = json.RootElement.GetProperty("gpu").GetString() ?? "";
                npu = json.RootElement.GetProperty("npu").GetString() ?? "";
                ram = json.RootElement.GetProperty("ram").GetDouble() / (1024 * 1024 * 1024);
            }
            else if (!process.HasExited) process.Kill(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception) { }
        var vulkan = ProbeDevice(Path.Combine(root, ".tools", "llama-vulkan", "llama-server.exe"), "Vulkan0:");
        var openvino = ProbeDevice(Path.Combine(root, ".tools", "llama-openvino", "llama-server.exe"), "OpenVINO");
        var validated = File.Exists(Path.Combine(root, "artifacts", "verification", "npu-model-verified.json"));
        return new(gpu, npu, ram, vulkan, openvino, validated);
    }
    private static string ProbeDevice(string executable, string prefix)
    {
        if (!File.Exists(executable)) return "";
        try
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
            if (prefix == "OpenVINO") info.Environment["GGML_OPENVINO_DEVICE"] = "NPU";
            info.ArgumentList.Add("--list-devices");
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(6000)) { process.Kill(true); return ""; }
            if (process.ExitCode != 0) return "";
            return output.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Contains(prefix, StringComparison.OrdinalIgnoreCase)) ?? "";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { return ""; }
    }
}
