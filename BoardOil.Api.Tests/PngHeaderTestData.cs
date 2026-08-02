using System.Buffers.Binary;

namespace BoardOil.Api.Tests;

internal static class PngHeaderTestData
{
    public static byte[] Create(int width, int height)
    {
        byte[] header =
        [
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
            0x00, 0x00, 0x00, 0x0d,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x08, 0x06, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(20, 4), height);
        return header;
    }
}
