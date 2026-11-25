using System;

namespace AnimeStudio;
public class LZ4
{
    public static LZ4 Instance => new();
    public virtual int Decompress(ReadOnlySpan<byte> cmp, Span<byte> dec)
    {
        int cmpPos = 0;
        int decPos = 0;

        if (cmp.IsEmpty)
        {
            throw new System.IO.InvalidDataException(CorruptBlockMessage("the block is empty"));
        }

        try
        {
            do
            {
                var (encCount, litCount) = GetLiteralToken(cmp, ref cmpPos);

                //Copy literal chunk
                litCount = GetLength(litCount, cmp, ref cmpPos);

                //A run that does not fit means the data is not a valid LZ4 block (encrypted or an unsupported layout)
                if (litCount > cmp.Length - cmpPos || litCount > dec.Length - decPos)
                {
                    throw new System.IO.InvalidDataException(CorruptBlockMessage($"a literal run of {litCount} bytes at 0x{cmpPos:X} does not fit into the input ({cmp.Length} bytes) or the output ({dec.Length} bytes)"));
                }
                cmp.Slice(cmpPos, litCount).CopyTo(dec.Slice(decPos));

                cmpPos += litCount;
                decPos += litCount;

                if (cmpPos >= cmp.Length)
                {
                    break;
                }

                //Copy compressed chunk
                int back = GetChunkEnd(cmp, ref cmpPos);

                encCount = GetLength(encCount, cmp, ref cmpPos) + 4;

                int encPos = decPos - back;

                if (encPos < 0 || encCount > dec.Length - decPos)
                {
                    throw new System.IO.InvalidDataException(CorruptBlockMessage($"a match of {encCount} bytes at 0x{cmpPos:X} with a distance of {back} reaches outside of the output ({dec.Length} bytes)"));
                }

                if (encCount <= back)
                {
                    dec.Slice(encPos, encCount).CopyTo(dec.Slice(decPos));

                    decPos += encCount;
                }
                else
                {
                    while (encCount-- > 0)
                    {
                        dec[decPos++] = dec[encPos++];
                    }
                }
            } while (cmpPos < cmp.Length &&
                     decPos < dec.Length);
        }
        catch (IndexOutOfRangeException e)
        {
            throw new System.IO.InvalidDataException(CorruptBlockMessage("the block ends in the middle of a token"), e);
        }

        return decPos;
    }

    private static string CorruptBlockMessage(string detail) => $"Corrupt LZ4 block: {detail}. The data is probably still encrypted, or its layout is not supported for this game version yet";
    protected virtual (int encCount, int litCount) GetLiteralToken(ReadOnlySpan<byte> cmp, ref int cmpPos) => ((cmp[cmpPos] >> 0) & 0xf, (cmp[cmpPos++] >> 4) & 0xf);
    protected virtual int GetChunkEnd(ReadOnlySpan<byte> cmp, ref int cmpPos) => cmp[cmpPos++] << 0 | cmp[cmpPos++] << 8;
    protected virtual int GetLength(int length, ReadOnlySpan<byte> cmp, ref int cmpPos)
    {
        byte sum;

        if (length == 0xf)
        {
            do
            {
                length += sum = cmp[cmpPos++];
            } while (sum == 0xff);
        }

        return length;
    }
}