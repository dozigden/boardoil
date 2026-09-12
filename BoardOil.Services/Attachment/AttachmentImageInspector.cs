using System.Buffers.Binary;
using BoardOil.Abstractions.Attachment;

namespace BoardOil.Services.Attachment;

internal static class AttachmentImageInspector
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] Vp8FrameStart = [0x9d, 0x01, 0x2a];

    public static AttachmentImageInfo Inspect(Stream content, AttachmentStorageOptions options)
    {
        if (!content.CanSeek)
        {
            throw new InvalidDataException("Attachment image inspection requires a seekable stream.");
        }

        var initialPosition = content.Position;
        try
        {
            content.Position = 0;
            var info = TryReadPng(content) ?? TryReadJpeg(content) ?? TryReadWebP(content) ?? TryReadGif(content)
                ?? throw new UnsupportedAttachmentImageException();
            ValidateDimensions(info, options);
            return info;
        }
        finally
        {
            content.Position = initialPosition;
        }
    }

    private static AttachmentImageInfo? TryReadPng(Stream content)
    {
        content.Position = 0;
        Span<byte> header = stackalloc byte[24];
        if (!TryReadExactly(content, header)
            || !header[..8].SequenceEqual(PngSignature)
            || BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) != 13
            || !header[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }

        return CreateInfo("image/png", BinaryPrimitives.ReadUInt32BigEndian(header[16..20]),
            BinaryPrimitives.ReadUInt32BigEndian(header[20..24]));
    }

    private static AttachmentImageInfo? TryReadGif(Stream content)
    {
        content.Position = 0;
        Span<byte> header = stackalloc byte[10];
        if (!TryReadExactly(content, header)
            || (!header[..6].SequenceEqual("GIF87a"u8) && !header[..6].SequenceEqual("GIF89a"u8)))
        {
            return null;
        }

        return CreateInfo("image/gif", BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]),
            BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]));
    }

    private static AttachmentImageInfo? TryReadWebP(Stream content)
    {
        content.Position = 0;
        Span<byte> header = stackalloc byte[30];
        var length = ReadUpTo(content, header);
        if (length < 21 || !header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var riffLength = (long)BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]) + 8;
        var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(header[16..20]);
        if (riffLength > content.Length || 20L + chunkLength > content.Length)
        {
            throw new UnsupportedAttachmentImageException();
        }
        if (header[12..16].SequenceEqual("VP8X"u8))
        {
            if (length < 30 || chunkLength < 10) { throw new UnsupportedAttachmentImageException(); }
            var width = 1u + ReadUInt24LittleEndian(header[24..27]);
            var height = 1u + ReadUInt24LittleEndian(header[27..30]);
            return CreateInfo("image/webp", width, height);
        }

        if (header[12..16].SequenceEqual("VP8L"u8))
        {
            if (length < 25 || chunkLength < 5 || header[20] != 0x2f) { throw new UnsupportedAttachmentImageException(); }
            var width = 1u + (uint)(header[21] | ((header[22] & 0x3f) << 8));
            var height = 1u + (uint)((header[22] >> 6) | (header[23] << 2) | ((header[24] & 0x0f) << 10));
            return CreateInfo("image/webp", width, height);
        }

        if (header[12..16].SequenceEqual("VP8 "u8))
        {
            if (length < 30 || chunkLength < 10 || !header[23..26].SequenceEqual(Vp8FrameStart))
            {
                throw new UnsupportedAttachmentImageException();
            }
            var width = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(header[26..28]) & 0x3fff);
            var height = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(header[28..30]) & 0x3fff);
            return CreateInfo("image/webp", width, height);
        }

        throw new UnsupportedAttachmentImageException();
    }

    private static AttachmentImageInfo? TryReadJpeg(Stream content)
    {
        content.Position = 0;
        if (content.ReadByte() != 0xff || content.ReadByte() != 0xd8)
        {
            return null;
        }

        while (content.Position < content.Length)
        {
            var prefix = content.ReadByte();
            if (prefix != 0xff) { continue; }

            int marker;
            do { marker = content.ReadByte(); } while (marker == 0xff);
            if (marker < 0 || marker is 0xd9 or 0xda) { break; }
            if (marker is 0x01 or >= 0xd0 and <= 0xd7) { continue; }

            var segmentLength = ReadUInt16BigEndian(content);
            if (segmentLength < 2 || content.Length - content.Position < segmentLength - 2)
            {
                throw new UnsupportedAttachmentImageException();
            }

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 7 || content.ReadByte() < 0) { throw new UnsupportedAttachmentImageException(); }
                var height = ReadUInt16BigEndian(content);
                var width = ReadUInt16BigEndian(content);
                return CreateInfo("image/jpeg", width, height);
            }

            content.Position += segmentLength - 2;
        }

        throw new UnsupportedAttachmentImageException();
    }

    private static bool IsStartOfFrame(int marker) => marker is
        0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;

    private static ushort ReadUInt16BigEndian(Stream content)
    {
        Span<byte> bytes = stackalloc byte[2];
        if (!TryReadExactly(content, bytes)) { throw new UnsupportedAttachmentImageException(); }
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes) =>
        (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16));

    private static AttachmentImageInfo CreateInfo(string contentType, uint width, uint height)
    {
        if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
        {
            throw new UnsupportedAttachmentImageException();
        }
        return new(contentType, (int)width, (int)height);
    }

    private static void ValidateDimensions(AttachmentImageInfo info, AttachmentStorageOptions options)
    {
        if (info.Width > options.MaxImageEdgeLength || info.Height > options.MaxImageEdgeLength
            || (long)info.Width * info.Height > options.MaxImagePixelCount)
        {
            throw new AttachmentImageDimensionsException(options.MaxImagePixelCount, options.MaxImageEdgeLength);
        }
    }

    private static bool TryReadExactly(Stream content, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = content.Read(buffer[offset..]);
            if (read == 0) { return false; }
            offset += read;
        }
        return true;
    }

    private static int ReadUpTo(Stream content, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = content.Read(buffer[offset..]);
            if (read == 0) { break; }
            offset += read;
        }
        return offset;
    }
}

internal sealed record AttachmentImageInfo(string ContentType, int Width, int Height);

public sealed class UnsupportedAttachmentImageException : Exception;

public sealed class AttachmentImageDimensionsException(long maxPixelCount, int maxEdgeLength)
    : Exception($"Attachment image must be no more than {maxPixelCount} pixels with each edge no longer than {maxEdgeLength} pixels.");
