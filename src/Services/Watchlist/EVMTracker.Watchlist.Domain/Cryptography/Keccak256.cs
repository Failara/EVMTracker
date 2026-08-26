using System.Buffers.Binary;
using System.Numerics;

namespace EVMTracker.Watchlist.Domain.Cryptography;

internal static class Keccak256
{
    private const int RateInBytes = 136;
    private const int HashSizeInBytes = 32;

    private static ReadOnlySpan<int> RotationOffsets =>
    [
        0, 1, 62, 28, 27,
        36, 44, 6, 55, 20,
        3, 10, 43, 25, 39,
        41, 45, 15, 21, 8,
        18, 2, 61, 56, 14,
    ];

    private static ReadOnlySpan<ulong> RoundConstants =>
    [
        0x0000000000000001UL, 0x0000000000008082UL,
        0x800000000000808AUL, 0x8000000080008000UL,
        0x000000000000808BUL, 0x0000000080000001UL,
        0x8000000080008081UL, 0x8000000000008009UL,
        0x000000000000008AUL, 0x0000000000000088UL,
        0x0000000080008009UL, 0x000000008000000AUL,
        0x000000008000808BUL, 0x800000000000008BUL,
        0x8000000000008089UL, 0x8000000000008003UL,
        0x8000000000008002UL, 0x8000000000000080UL,
        0x000000000000800AUL, 0x800000008000000AUL,
        0x8000000080008081UL, 0x8000000000008080UL,
        0x0000000080000001UL, 0x8000000080008008UL,
    ];

    public static byte[] Hash(ReadOnlySpan<byte> input)
    {
        Span<ulong> state = stackalloc ulong[25];

        while (input.Length >= RateInBytes)
        {
            AbsorbBlock(state, input[..RateInBytes]);
            Permute(state);
            input = input[RateInBytes..];
        }

        Span<byte> finalBlock = stackalloc byte[RateInBytes];
        input.CopyTo(finalBlock);
        finalBlock[input.Length] = 0x01;
        finalBlock[^1] |= 0x80;

        AbsorbBlock(state, finalBlock);
        Permute(state);

        byte[] hash = new byte[HashSizeInBytes];
        for (int index = 0; index < HashSizeInBytes / sizeof(ulong); index++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(
                hash.AsSpan(index * sizeof(ulong), sizeof(ulong)),
                state[index]);
        }

        return hash;
    }

    private static void AbsorbBlock(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        for (int index = 0; index < RateInBytes / sizeof(ulong); index++)
        {
            state[index] ^= BinaryPrimitives.ReadUInt64LittleEndian(
                block.Slice(index * sizeof(ulong), sizeof(ulong)));
        }
    }

    private static void Permute(Span<ulong> state)
    {
        Span<ulong> columnParity = stackalloc ulong[5];
        Span<ulong> transformed = stackalloc ulong[25];

        foreach (ulong roundConstant in RoundConstants)
        {
            for (int x = 0; x < 5; x++)
            {
                columnParity[x] = state[x] ^
                                  state[x + 5] ^
                                  state[x + 10] ^
                                  state[x + 15] ^
                                  state[x + 20];
            }

            for (int x = 0; x < 5; x++)
            {
                ulong difference = columnParity[(x + 4) % 5] ^
                                   BitOperations.RotateLeft(columnParity[(x + 1) % 5], 1);

                for (int y = 0; y < 5; y++)
                {
                    state[x + (5 * y)] ^= difference;
                }
            }

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int sourceIndex = x + (5 * y);
                    int destinationIndex = y + (5 * ((2 * x + 3 * y) % 5));
                    transformed[destinationIndex] = BitOperations.RotateLeft(
                        state[sourceIndex],
                        RotationOffsets[sourceIndex]);
                }
            }

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    int index = x + (5 * y);
                    state[index] = transformed[index] ^
                                   (~transformed[((x + 1) % 5) + (5 * y)] &
                                    transformed[((x + 2) % 5) + (5 * y)]);
                }
            }

            state[0] ^= roundConstant;
        }
    }
}
