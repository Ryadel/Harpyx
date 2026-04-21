using System.Buffers.Binary;

namespace Harpyx.Infrastructure.Services;

internal static class EmbeddingVectorSerializer
{
    public static byte[] Serialize(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)), vector[i]);
        }

        return bytes;
    }

    public static float[] Deserialize(byte[] bytes)
    {
        if (bytes.Length % sizeof(float) != 0)
            throw new InvalidOperationException("Invalid embedding payload.");

        var vector = new float[bytes.Length / sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float), sizeof(float)));
        }

        return vector;
    }
}
