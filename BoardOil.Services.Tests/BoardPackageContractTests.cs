using BoardOil.Contracts.Board;
using BoardOil.Services.Board;
using Xunit;

namespace BoardOil.Services.Tests;

public sealed class BoardPackageContractTests
{
    [Fact]
    public void ValidateManifest_WhenUsingCurrentSchema_ShouldSucceed()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion,
            "0.2.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);

        var validationError = BoardPackageContract.ValidateManifest(manifest);

        Assert.Null(validationError);
        Assert.True(BoardPackageContract.IsSupportedSchemaVersion(manifest.SchemaVersion));
        Assert.True(BoardPackageContract.IsSupportedSchemaVersion(BoardPackageContract.MinSupportedSchemaVersion));
    }

    [Fact]
    public void CreateManifest_ShouldIncludeBoardAndArchiveEntries()
    {
        var manifest = BoardPackageContract.CreateManifest("0.2.0");

        Assert.Contains(manifest.Entries, x => x.Kind == BoardPackageContract.BoardEntryKind && x.Path == BoardPackageContract.BoardEntryPath);
        Assert.Contains(manifest.Entries, x => x.Kind == BoardPackageContract.ArchiveEntryKind && x.Path == BoardPackageContract.ArchiveEntryPath);
    }

    [Fact]
    public void IsSupportedSchemaVersion_WhenOutsideSupportedWindow_ShouldReturnFalse()
    {
        var belowMinimum = BoardPackageContract.MinSupportedSchemaVersion - 1;
        var aboveCurrent = BoardPackageContract.CurrentSchemaVersion + 1;

        Assert.False(BoardPackageContract.IsSupportedSchemaVersion(belowMinimum));
        Assert.False(BoardPackageContract.IsSupportedSchemaVersion(aboveCurrent));
    }

    [Fact]
    public void ValidateManifest_WhenSchemaVersionIsFuture_ShouldFailWithClearError()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion + 1,
            "999.0.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath)]);

        var validationError = BoardPackageContract.ValidateManifest(manifest);

        Assert.NotNull(validationError);
        Assert.Equal(400, validationError!.StatusCode);
        Assert.NotNull(validationError.ValidationErrors);
        Assert.True(validationError.ValidationErrors!.ContainsKey("manifest.schemaVersion"));
        Assert.Contains(
            "Maximum supported version is",
            validationError.ValidationErrors["manifest.schemaVersion"][0]);
    }

    [Fact]
    public void ValidateManifest_WhenBoardEntryPathIsWrong_ShouldFail()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion,
            "0.2.0",
            [new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, "board-data.json")]);

        var validationError = BoardPackageContract.ValidateManifest(manifest);

        Assert.NotNull(validationError);
        Assert.NotNull(validationError!.ValidationErrors);
        Assert.True(validationError.ValidationErrors!.ContainsKey("manifest.entries"));
    }

    [Fact]
    public void ValidateManifest_WhenArchiveEntryPathIsWrong_ShouldFail()
    {
        var manifest = new BoardPackageManifestDto(
            BoardPackageContract.PackageFormat,
            BoardPackageContract.CurrentSchemaVersion,
            "0.2.0",
            [
                new BoardPackageManifestEntryDto(BoardPackageContract.BoardEntryKind, BoardPackageContract.BoardEntryPath),
                new BoardPackageManifestEntryDto(BoardPackageContract.ArchiveEntryKind, "archive-data.json")
            ]);

        var validationError = BoardPackageContract.ValidateManifest(manifest);

        Assert.NotNull(validationError);
        Assert.NotNull(validationError!.ValidationErrors);
        Assert.True(validationError.ValidationErrors!.ContainsKey("manifest.entries"));
    }
}
