using System.Security.Cryptography;
using System.Text;

namespace BoardOil.Api.Configuration;

public static class JwtSigningKeyProvider
{
    public const string FormerPublishedSigningKey = "replace-this-with-a-strong-32-char-min-signing-key";
    public const string FormerPublishedDevelopmentSigningKey = "boardoil-dev-signing-key-change-me-1234567890";

    private const int SigningKeyByteCount = 64;
    private const int MinimumSigningKeyCharacterCount = 32;

    public static string Resolve(IConfiguration configuration, string generatedKeyPath)
    {
        var configuredKey = configuration["BoardOilAuth:SigningKey"];
        if (IsUniqueExplicitKey(configuredKey))
        {
            return configuredKey!;
        }

        return GetOrCreateGeneratedKey(generatedKeyPath);
    }

    private static bool IsUniqueExplicitKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return false;
        }

        var trimmedKey = configuredKey.Trim();
        return !string.Equals(trimmedKey, FormerPublishedSigningKey, StringComparison.Ordinal)
            && !string.Equals(trimmedKey, FormerPublishedDevelopmentSigningKey, StringComparison.Ordinal);
    }

    private static string GetOrCreateGeneratedKey(string generatedKeyPath)
    {
        if (File.Exists(generatedKeyPath))
        {
            return ReadGeneratedKey(generatedKeyPath);
        }

        var directoryPath = Path.GetDirectoryName(generatedKeyPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("The generated signing-key path must include a directory.");
        }

        Directory.CreateDirectory(directoryPath);
        var generatedKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SigningKeyByteCount));
        var temporaryPath = $"{generatedKeyPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            WriteGeneratedKey(temporaryPath, generatedKey);
            try
            {
                File.Move(temporaryPath, generatedKeyPath);
                return generatedKey;
            }
            catch (IOException) when (File.Exists(generatedKeyPath))
            {
                return ReadGeneratedKey(generatedKeyPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ReadGeneratedKey(string generatedKeyPath)
    {
        var generatedKey = File.ReadAllText(generatedKeyPath).Trim();
        if (generatedKey.Length < MinimumSigningKeyCharacterCount)
        {
            throw new InvalidOperationException(
                $"The generated signing key at '{generatedKeyPath}' is invalid. Delete the file to regenerate it or configure BoardOilAuth:SigningKey explicitly.");
        }

        return generatedKey;
    }

    private static void WriteGeneratedKey(string path, string signingKey)
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.CreateNew,
            Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(path, options);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(signingKey);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}
