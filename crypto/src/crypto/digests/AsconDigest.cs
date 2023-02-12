﻿using System;
#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

using Org.BouncyCastle.Crypto.Utilities;
using Org.BouncyCastle.Utilities;

namespace Org.BouncyCastle.Crypto.Digests
{
    public sealed class AsconDigest
        : IDigest
    {
        public enum AsconParameters
        {
            AsconHash,
            AsconHashA,
            AsconXof,
            AsconXofA,
        }

        private readonly AsconParameters m_asconParameters;
        private readonly int ASCON_PB_ROUNDS;

        private ulong x0;
        private ulong x1;
        private ulong x2;
        private ulong x3;
        private ulong x4;

        private readonly byte[] m_buf = new byte[8];
        private int m_bufPos = 0;

        public AsconDigest(AsconParameters parameters)
        {
            m_asconParameters = parameters;
            switch (parameters)
            {
            case AsconParameters.AsconHash:
                ASCON_PB_ROUNDS = 12;
                break;
            case AsconParameters.AsconHashA:
                ASCON_PB_ROUNDS = 8;
                break;
            case AsconParameters.AsconXof:
                ASCON_PB_ROUNDS = 12;
                break;
            case AsconParameters.AsconXofA:
                ASCON_PB_ROUNDS = 8;
                break;
            default:
                throw new ArgumentException("Invalid parameter settings for Ascon Hash");
            }
            Reset();
        }

        public string AlgorithmName
        {
            get
            {
                switch (m_asconParameters)
                {
                case AsconParameters.AsconHash:     return "Ascon-Hash";
                case AsconParameters.AsconHashA:    return "Ascon-HashA";
                case AsconParameters.AsconXof:      return "Ascon-Xof";
                case AsconParameters.AsconXofA:     return "Ascon-XofA";
                default: throw new InvalidOperationException();
                }
            }
        }

        public int GetDigestSize() => 32;

        public int GetByteLength() => 8;

        public void Update(byte input)
        {
            m_buf[m_bufPos] = input;
            if (++m_bufPos == 8)
            {
                x0 ^= Pack.BE_To_UInt64(m_buf, 0);
                P(ASCON_PB_ROUNDS);
                m_bufPos = 0;
            }
        }

        public void BlockUpdate(byte[] input, int inOff, int inLen)
        {
            Check.DataLength(input, inOff, inLen, "input buffer too short");

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            BlockUpdate(input.AsSpan(inOff, inLen));
#else
            if (inLen < 1)
                return;

            int available = 8 - m_bufPos;
            if (inLen < available)
            {
                Array.Copy(input, inOff, m_buf, m_bufPos, inLen);
                m_bufPos += inLen;
                return;
            }

            int inPos = 0;
            if (m_bufPos > 0)
            {
                Array.Copy(input, inOff, m_buf, m_bufPos, available);
                inPos += available;
                x0 ^= Pack.BE_To_UInt64(m_buf, 0);
                P(ASCON_PB_ROUNDS);
            }

            int remaining;
            while ((remaining = inLen - inPos) >= 8)
            {
                x0 ^= Pack.BE_To_UInt64(input, inOff + inPos);
                P(ASCON_PB_ROUNDS);
                inPos += 8;
            }

            Array.Copy(input, inOff + inPos, m_buf, 0, remaining);
            m_bufPos = remaining;
#endif
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public void BlockUpdate(ReadOnlySpan<byte> input)
        {
            int available = 8 - m_bufPos;
            if (input.Length < available)
            {
                input.CopyTo(m_buf.AsSpan(m_bufPos));
                m_bufPos += input.Length;
                return;
            }

            if (m_bufPos > 0)
            {
                input[..available].CopyTo(m_buf.AsSpan(m_bufPos));
                x0 ^= Pack.BE_To_UInt64(m_buf);
                P(ASCON_PB_ROUNDS);
                input = input[available..];
            }

            while (input.Length >= 8)
            {
                x0 ^= Pack.BE_To_UInt64(input);
                P(ASCON_PB_ROUNDS);
                input = input[8..];
            }

            input.CopyTo(m_buf);
            m_bufPos = input.Length;
        }
#endif

        public int DoFinal(byte[] output, int outOff)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return DoFinal(output.AsSpan(outOff));
#else
            Check.OutputLength(output, outOff, 32, "output buffer is too short");

            m_buf[m_bufPos] = 0x80;
            x0 ^= Pack.BE_To_UInt64(m_buf, 0) & (ulong.MaxValue << (56 - (m_bufPos << 3)));

            P(12);
            Pack.UInt64_To_BE(x0, output, outOff);

            for (int i = 0; i < 3; ++i)
            {
                outOff += 8;

                P(ASCON_PB_ROUNDS);
                Pack.UInt64_To_BE(x0, output, outOff);
            }

            Reset();
            return 32;
#endif
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public int DoFinal(Span<byte> output)
        {
            Check.OutputLength(output, 32, "output buffer is too short");

            m_buf[m_bufPos] = 0x80;
            x0 ^= Pack.BE_To_UInt64(m_buf) & (ulong.MaxValue << (56 - (m_bufPos << 3)));

            P(12);
            Pack.UInt64_To_BE(x0, output);

            for (int i = 0; i < 3; ++i)
            {
                output = output[8..];

                P(ASCON_PB_ROUNDS);
                Pack.UInt64_To_BE(x0, output);
            }

            Reset();
            return 32;
        }
#endif

        public void Reset()
        {
            Array.Clear(m_buf, 0, m_buf.Length);
            m_bufPos = 0;

            switch (m_asconParameters)
            {
            case AsconParameters.AsconHashA:
                x0 = 92044056785660070UL;
                x1 = 8326807761760157607UL;
                x2 = 3371194088139667532UL;
                x3 = 15489749720654559101UL;
                x4 = 11618234402860862855UL;
                break;
            case AsconParameters.AsconHash:
                x0 = 17191252062196199485UL;
                x1 = 10066134719181819906UL;
                x2 = 13009371945472744034UL;
                x3 = 4834782570098516968UL;
                x4 = 3787428097924915520UL;
                break;
            case AsconParameters.AsconXof:
                x0 = 13077933504456348694UL;
                x1 = 3121280575360345120UL;
                x2 = 7395939140700676632UL;
                x3 = 6533890155656471820UL;
                x4 = 5710016986865767350UL;
                break;
            case AsconParameters.AsconXofA:
                x0 = 4940560291654768690UL;
                x1 = 14811614245468591410UL;
                x2 = 17849209150987444521UL;
                x3 = 2623493988082852443UL;
                x4 = 12162917349548726079UL;
                break;
            default:
                throw new InvalidOperationException();
            }
        }

        private void P(int nr)
        {
            if (nr == 12)
            {
                ROUND(0xf0UL);
                ROUND(0xe1UL);
                ROUND(0xd2UL);
                ROUND(0xc3UL);
            }
            ROUND(0xb4UL);
            ROUND(0xa5UL);
            ROUND(0x96UL);
            ROUND(0x87UL);
            ROUND(0x78UL);
            ROUND(0x69UL);
            ROUND(0x5aUL);
            ROUND(0x4bUL);
        }

#if NETSTANDARD1_0_OR_GREATER || NETCOREAPP1_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void ROUND(ulong C)
        {
            ulong t0 = x0 ^ x1 ^ x2 ^ x3 ^ C ^ (x1 & (x0 ^ x2 ^ x4 ^ C));
            ulong t1 = x0 ^ x2 ^ x3 ^ x4 ^ C ^ ((x1 ^ x2 ^ C) & (x1 ^ x3));
            ulong t2 = x1 ^ x2 ^ x4 ^ C ^ (x3 & x4);
            ulong t3 = x0 ^ x1 ^ x2 ^ C ^ (~x0 & (x3 ^ x4));
            ulong t4 = x1 ^ x3 ^ x4 ^ ((x0 ^ x4) & x1);
            x0 = t0 ^ Longs.RotateRight(t0, 19) ^ Longs.RotateRight(t0, 28);
            x1 = t1 ^ Longs.RotateRight(t1, 39) ^ Longs.RotateRight(t1, 61);
            x2 = ~(t2 ^ Longs.RotateRight(t2, 1) ^ Longs.RotateRight(t2, 6));
            x3 = t3 ^ Longs.RotateRight(t3, 10) ^ Longs.RotateRight(t3, 17);
            x4 = t4 ^ Longs.RotateRight(t4, 7) ^ Longs.RotateRight(t4, 41);
        }
    }
}
