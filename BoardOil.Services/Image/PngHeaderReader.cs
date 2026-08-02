using System.Buffers.Binary;

namespace BoardOil.Services.Image;

internal static class PngHeaderReader
{
    private const int HeaderByteLength = 33;

    private static ReadOnlySpan<byte> Signature =>
    [
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a
    ];

    private static ReadOnlySpan<byte> IhdrChunkType =>
    [
        0x49, 0x48, 0x44, 0x52
    ];

    public static PngDimensions? ReadDimensions(ReadOnlySpan<byte> content)
    {
        if (content.Length < HeaderByteLength
            || !content[..Signature.Length].SequenceEqual(Signature)
            || BinaryPrimitives.ReadUInt32BigEndian(content.Slice(8, 4)) != 13
            || !content.Slice(12, 4).SequenceEqual(IhdrChunkType))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(20, 4));
        return new PngDimensions(width, height);
    }
}

internal readonly record struct PngDimensions(uint Width, uint Height);
