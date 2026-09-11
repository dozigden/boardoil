using System.IO.Compression;
using System.Text.Json;
using BoardOil.Contracts.Board;
using BoardOil.Services.Board;
using BoardOil.Services.Board.Import;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardPackageImportReaderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void TryReadBoardPackage_WhenSchemaVersionIsFuture_ShouldFailBeforeParsingBoardPayload()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion + 1,
            "999.0.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);

        var reader = new BoardPackageImportReader();
        var result = reader.TryReadBoardPackage(BuildBoardPackageWithRawBoardPayload(manifest, "{"));

        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.ValidationErrors);
        Assert.Contains("manifest.schemaVersion", result.Error.ValidationErrors!.Keys);
        Assert.DoesNotContain("board", result.Error.ValidationErrors.Keys);
    }

    [Fact]
    public void TryReadBoardPackage_WhenPayloadIsNotZip_ShouldReturnValidationError()
    {
        var reader = new BoardPackageImportReader();
        var result = reader.TryReadBoardPackage([0x01, 0x02, 0x03]);

        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error!.ValidationErrors);
        Assert.Contains("file", result.Error.ValidationErrors!.Keys);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryReadBoardPackage_ShouldNotImposeFormerJsonSizeOrEntryCountCaps(bool largeJson)
    {
        var manifest = new BoardPackageManifestDto(BoardPackageContract.PackageFormat, 3, "test",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);
        using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            using (var output = archive.CreateEntry(BoardPackageContract.BoardEntryPath, CompressionLevel.Fastest).Open())
            {
                if (largeJson)
                {
                    var whitespace = Enumerable.Repeat((byte)' ', 65536).ToArray();
                    for (var index = 0; index < 1024; index++) { output.Write(whitespace); }
                }
                output.Write("{}"u8);
            }
            if (!largeJson)
            {
                for (var index = 0; index < 100_000; index++) { archive.CreateEntry("extra/" + index); }
            }
        }
        package.Position = 0;

        var result = new BoardPackageImportReader().TryReadBoardPackage(package);

        Assert.Null(result.Error);
        Assert.NotNull(result.BoardPayload);
    }

    private static byte[] BuildBoardPackageWithRawBoardPayload(BoardPackageManifestDto manifest, string rawBoardPayload)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJsonEntry(archive, BoardPackageContract.ManifestPath, manifest);
            var boardEntry = archive.CreateEntry(BoardPackageContract.BoardEntryPath, CompressionLevel.Optimal);
            using var writer = new StreamWriter(boardEntry.Open());
            writer.Write(rawBoardPayload);
        }

        return stream.ToArray();
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string path, T payload)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
