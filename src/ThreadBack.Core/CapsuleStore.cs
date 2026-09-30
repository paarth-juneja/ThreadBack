using System.Security.Cryptography;
using System.Text.Json;

namespace ThreadBack.Core;

public sealed record CapsuleListing(List<Capsule> Capsules, int UnreadableFiles);

public sealed class CapsuleStore
{
    public string DirectoryPath { get; }
    public CapsuleStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreadBack", "Capsules");
        Directory.CreateDirectory(DirectoryPath);
    }
    private string FilePath(Guid id) => Path.Combine(DirectoryPath, id.ToString("N") + ".tbc");

    public void Save(Capsule capsule)
    {
        capsule.UpdatedAt = DateTimeOffset.UtcNow;
        var plain = JsonSerializer.SerializeToUtf8Bytes(capsule, CapsuleRules.Json);
        byte[] cipher;
        try { cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var target = FilePath(capsule.Id);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { file.Write(cipher); file.Flush(true); }
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public Capsule Load(Guid id)
    {
        var plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath(id)), null, DataProtectionScope.CurrentUser);
        try
        {
            var result = JsonSerializer.Deserialize<Capsule>(plain, CapsuleRules.Json) ?? throw new InvalidDataException("Empty capsule.");
            if (result.SchemaVersion != 1 || result.Id != id) throw new InvalidDataException("Unsupported capsule format.");
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public CapsuleListing List()
    {
        List<Capsule> capsules = [];
        int unreadable = 0;
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.tbc"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id)) { unreadable++; continue; }
            try { capsules.Add(Load(id)); }
            catch (Exception ex) when (ex is CryptographicException or IOException or JsonException or InvalidDataException) { unreadable++; }
        }
        return new(capsules.OrderByDescending(c => c.UpdatedAt).ToList(), unreadable);
    }

    public void Delete(Guid id) => File.Delete(FilePath(id));
}
