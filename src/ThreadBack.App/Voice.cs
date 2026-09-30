using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace ThreadBack.App;

internal sealed class VoiceRecorder : IDisposable
{
    private readonly WaveInEvent input = new() { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
    private readonly MemoryStream memory = new();
    private readonly WaveFileWriter writer;
    private readonly TaskCompletionSource<byte[]> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool disposed;
    public VoiceRecorder()
    {
        writer = new WaveFileWriter(memory, input.WaveFormat);
        input.DataAvailable += (_, e) => { if (!disposed) writer.Write(e.Buffer, 0, e.BytesRecorded); };
        input.RecordingStopped += (_, e) =>
        {
            if (disposed) return;
            writer.Flush();
            if (e.Exception is not null) stopped.TrySetException(e.Exception); else stopped.TrySetResult(memory.ToArray());
        };
    }
    public void Start() => input.StartRecording();
    public async Task<byte[]> StopAsync() { input.StopRecording(); return await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
    public void Dispose() { disposed = true; input.Dispose(); writer.Dispose(); memory.Dispose(); }
}

internal static class VoiceTranscriber
{
    public static byte[] DecodeM4a(string path)
    {
        using var reader = new MediaFoundationReader(path);
        if (reader.TotalTime.TotalSeconds > 60.5 || reader.TotalTime.TotalSeconds < 0.2)
            throw new InvalidDataException("Use a recording between 0.2 and 60 seconds.");
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1)) { ResamplerQuality = 60 };
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, resampler.WaveFormat))
        {
            var buffer = new byte[32 * 1024];
            int count;
            while ((count = resampler.Read(buffer, 0, buffer.Length)) > 0) writer.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    public static void Validate(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes); using var reader = new WaveFileReader(stream);
        if (reader.TotalTime.TotalSeconds > 60.5 || reader.TotalTime.TotalSeconds < 0.2) throw new InvalidDataException("Use a WAV recording between 0.2 and 60 seconds.");
    }
    public static async Task<string> TranscribeAsync(byte[] bytes, string root, CancellationToken token)
    {
        Validate(bytes);
        var directory = Path.Combine(root, ".tools", "whisper");
        var executable = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "whisper-cli.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
        var model = Path.Combine(root, "models", "ggml-base.en.bin");
        if (executable is null || !File.Exists(model)) throw new FileNotFoundException("Voice model is not installed. Run scripts/Setup-Voice.ps1.");
        var temp = Path.Combine(Path.GetTempPath(), "ThreadBack-voice-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var audio = Path.Combine(temp, "input.wav");
        var output = Path.Combine(temp, "transcript");
        try
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new WaveFileReader(stream))
            using (var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1)))
            { resampler.ResamplerQuality = 60; WaveFileWriter.CreateWaveFile(audio, resampler); }
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-m", model, "-f", audio, "-l", "en", "-otxt", "-of", output, "-nt", "-t", "4" }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info) ?? throw new IOException("Voice worker did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(2));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0 || !File.Exists(output + ".txt")) throw new IOException("Voice transcription failed. Try a clearer recording or type your note.");
            var text = (await File.ReadAllTextAsync(output + ".txt", token)).Trim();
            text = Regex.Replace(text, @"\[(?:BLANK_AUDIO|NO_SPEECH|SILENCE)\]", "", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("No speech was detected. Your audio remains attached.");
            return text;
        }
        finally
        {
            // Only this worker's explicitly named temporary files are removed.
            foreach (var path in new[] { audio, output + ".txt" }) if (File.Exists(path)) File.Delete(path);
            if (!Directory.EnumerateFileSystemEntries(temp).Any()) Directory.Delete(temp);
        }
    }
}
