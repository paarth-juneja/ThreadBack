using System.Diagnostics;

namespace ThreadBack.Core;

public enum ModelBackend { Cpu, VulkanGpu, OpenVinoNpu }

public static class ModelRuntime
{
    public static string Executable(string root, ModelBackend backend)
    {
        var folder = backend switch
        {
            ModelBackend.VulkanGpu => "llama-vulkan",
            ModelBackend.OpenVinoNpu => "llama-openvino",
            _ => "llama"
        };
        var path = Path.Combine(root, ".tools", folder, "llama-server.exe");
        if (!File.Exists(path)) throw new FileNotFoundException($"The {backend} model backend is not installed. Choose CPU in Model settings.", path);
        return path;
    }

    public static void Configure(ProcessStartInfo info, ModelBackend backend)
    {
        switch (backend)
        {
            case ModelBackend.VulkanGpu:
                foreach (var arg in new[] { "--device", "Vulkan0", "--gpu-layers", "8" }) info.ArgumentList.Add(arg);
                break;
            case ModelBackend.OpenVinoNpu:
                info.Environment["GGML_OPENVINO_DEVICE"] = "NPU";
                foreach (var arg in new[] { "--device", "OPENVINO0", "--gpu-layers", "999" }) info.ArgumentList.Add(arg);
                break;
        }
    }
}
