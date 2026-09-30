using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThreadBack.Core;

public sealed class LocalGenerator : IDisposable
{
    private Process? process;
    private ModelMemoryLimit? memoryLimit;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(12) };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string root;
    private bool started;
    private string? activeBackend;
    private bool UseQwen3 => File.Exists(Path.Combine(root, "models", "Qwen3-4B-Q4_K_M.gguf"));
    public bool IsLoaded => process is { HasExited: false };
    public ModelBackend BackendMode { get; set; } = ModelBackend.Cpu;
    public int MemoryLimitGb { get; set; }
    public string Backend => activeBackend ?? ((UseQwen3 ? "Qwen3 4B" : "Qwen2.5 1.5B") + $" · Q4_K_M · {BackendMode} · llama.cpp b10964");
    public long PeakModelMemoryBytes { get { if (process is null || process.HasExited) return 0; process.Refresh(); return process.PeakWorkingSet64; } }

    public LocalGenerator(string root) { this.root = root; }

    public static string FindRoot()
    {
        var configured = Environment.GetEnvironmentVariable("THREADBACK_ROOT");
        if (!string.IsNullOrEmpty(configured)) return configured;
        var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreadBack", "runtime-root.txt");
        if (File.Exists(configPath))
        {
            var configuredRoot = File.ReadAllText(configPath).Trim();
            if (Directory.Exists(Path.Combine(configuredRoot, "models"))) return configuredRoot;
        }
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "models")) && Directory.Exists(Path.Combine(d.FullName, ".tools"))) return d.FullName;
        return AppContext.BaseDirectory;
    }

    private async Task StartAsync(CancellationToken token)
    {
        if (started && process is { HasExited: false }) return;
        memoryLimit?.Dispose(); memoryLimit = null;
        var executable = ModelRuntime.Executable(root, BackendMode);
        var model = Path.Combine(root, "models", UseQwen3 ? "Qwen3-4B-Q4_K_M.gguf" : "qwen2.5-1.5b-instruct-q4_k_m.gguf");
        if (!File.Exists(model)) throw new FileNotFoundException("Local AI is not installed yet. Run scripts/Setup.ps1 -Component Model. You can still save your evidence.");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        foreach (var argument in new[] { "-m", model, "--host", "127.0.0.1", "--port", port.ToString(), "-c", "8192", "-np", "1", "-t", Math.Min(8, Math.Max(2, Environment.ProcessorCount / 2)).ToString(), "--no-webui", "--log-disable" }) info.ArgumentList.Add(argument);
        ModelRuntime.Configure(info, BackendMode);
        if (MemoryLimitGb > 0) { info.ArgumentList.Add("--load-mode"); info.ArgumentList.Add("none"); }
        if (UseQwen3) { info.ArgumentList.Add("--jinja"); info.ArgumentList.Add("--chat-template-kwargs"); info.ArgumentList.Add("{\"enable_thinking\":false}"); }
        info.Environment["LLAMA_API_KEY"] = key;
        if (process is { HasExited: false }) process.Kill(true);
        process?.Dispose();
        process = Process.Start(info) ?? throw new IOException("The local model process did not start.");
        try { if (MemoryLimitGb != 0) memoryLimit = new ModelMemoryLimit(process, MemoryLimitGb); }
        catch { process.Kill(true); process.Dispose(); process = null; throw; }
        activeBackend = (UseQwen3 ? "Qwen3 4B" : "Qwen2.5 1.5B") + $" · Q4_K_M · {BackendMode} · llama.cpp b10964";
        // Drain without retaining prompts, model output, or captured user content in logs.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        endpoint = new Uri($"http://127.0.0.1:{port}/");
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromMinutes(2))
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited) throw new IOException(MemoryLimitGb > 0 ? $"The model stopped while loading. The {MemoryLimitGb} GB memory limit may be too small; choose a higher limit or No limit in Model settings." : "The local model stopped during startup. Check the pinned runtime and available memory.");
            try { using var response = await client.GetAsync(new Uri(endpoint, "health"), token); if (response.IsSuccessStatusCode) { started = true; return; } }
            catch (HttpRequestException) { }
            await Task.Delay(400, token);
        }
        throw new TimeoutException("Loading the local model took too long. Your evidence is safe.");
    }
    private Uri endpoint = new("http://127.0.0.1/");

    public async Task<(ResumeBrief Brief, double Seconds)> GenerateAsync(Capsule capsule, CancellationToken token, IProgress<string>? progress = null)
    {
        CapsuleRules.ValidateInput(capsule);
        await gate.WaitAsync(token);
        try
        {
            progress?.Report("Loading the local AI");
            await StartAsync(token);
            progress?.Report($"Connecting {capsule.Evidence.Count} sources to your goal");
            var timer = Stopwatch.StartNew();
            var prompt = HandoffSynthesis.BuildPrompt(capsule);
            const string instructions = HandoffSynthesis.Instructions;
            string? failure = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                progress?.Report(attempt == 0 ? "Drafting your resume capsule locally" : "Checking source links and revising the draft locally");
                var request = new
                {
                    messages = new[] { new { role = "system", content = instructions + (failure is null ? "" : " Your previous output was invalid: " + failure + " Correct it using the original evidence.") }, new { role = "user", content = prompt } },
                    temperature = 0, max_tokens = 2200,
                    response_format = new { type = "json_schema", json_schema = new { name = "resume_capsule", strict = true, schema = HandoffSynthesis.BuildSchema() } }
                };
                using var response = await client.PostAsJsonAsync(new Uri(endpoint, "v1/chat/completions"), request, CapsuleRules.Json, token);
                if (!response.IsSuccessStatusCode) throw new IOException($"Local generation failed ({(int)response.StatusCode}). Shorten the notes and retry; your evidence is unchanged.");
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                var content = payload.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                try
                {
                    return (HandoffSynthesis.Parse(content, capsule), timer.Elapsed.TotalSeconds);
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException) { failure = ex.Message; }
            }
            throw new InvalidDataException("The model could not produce a valid evidence-linked capsule. Your evidence is retained. Try a clearer handoff note.");
        }
        catch (OperationCanceledException) { StopWorker(); throw; }
        finally { gate.Release(); }
    }

    private void StopWorker()
    {
        if (process is { HasExited: false }) { try { process.Kill(true); } catch (InvalidOperationException) { } }
        process?.Dispose(); process = null; memoryLimit?.Dispose(); memoryLimit = null; started = false;
    }
    public void Unload() => StopWorker();
    public void Dispose() { StopWorker(); client.Dispose(); }
}



