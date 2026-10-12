using System.Text;
using System.Text.Json;

namespace Salep.ClientGenerator.Generation;

/// <summary>A formatting-preserving v1-to-v2 upgrade, applied only after generation preflight succeeds.</summary>
internal sealed record ConfigurationUpgrade(string Path, byte[] Original, byte[] Updated)
{
    public static ConfigurationUpgrade? Read(string path, int targetVersion = ScribanGenerator.ConfigurationVersion)
    {
        var bytes = ReadBytes(path);
        using var textReader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var text = textReader.ReadToEnd();
        var encoding = textReader.CurrentEncoding;
        var utf8 = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(utf8);
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1 || !reader.ValueTextEquals("version")) continue;
            reader.Read();
            if (reader.GetInt32() == targetVersion) return null;
            if (reader.GetInt32() is not (1 or ScribanGenerator.ConfigurationVersion)) throw new InvalidDataException("Unsupported configuration version.");
            var start = checked((int)reader.TokenStartIndex);
            var end = checked((int)reader.BytesConsumed);
            // v2 adds template overrides; all v1 fields, defaults and references remain valid.
            var replacement = Encoding.UTF8.GetBytes(targetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
            byte[] updatedUtf8 = [..utf8.AsSpan(0, start), ..replacement, ..utf8.AsSpan(end)];
            var updated = encoding.GetBytes(Encoding.UTF8.GetString(updatedUtf8));
            var preamble = encoding.GetPreamble();
            return new(path, bytes, bytes.AsSpan().StartsWith(preamble) ? [..preamble, ..updated] : updated);
        }
        throw new InvalidDataException("Configuration version is required.");
    }

    internal static byte[] ReadBytes(string path)
    {
        // Atomic replacement must coexist with validators and build input hashing on Windows too.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        using var contents = new MemoryStream();
        stream.CopyTo(contents);
        return contents.ToArray();
    }

    public bool Apply()
    {
        var current = ReadBytes(Path);
        if (current.AsSpan().SequenceEqual(Updated)) return false;
        if (!current.AsSpan().SequenceEqual(Original)) throw new IOException($"Configuration changed during upgrade: '{Path}'. Rerun generation.");
        Replace(Updated);
        return true;
    }

    public void Rollback()
    {
        // Never overwrite a subsequent user edit while recovering a failed publication.
        if (ReadBytes(Path).AsSpan().SequenceEqual(Updated)) Replace(Original);
    }

    private void Replace(byte[] bytes)
    {
        var temporary = Path + ".salep-upgrade-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, File.GetUnixFileMode(Path));
            File.Move(temporary, Path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
