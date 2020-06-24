/*
 * This file is part of the Ignorance 1.4.x Mirror Network Transport system.
 * Copyright (c) 2019 Matt Coburn (SoftwareGuy/Coburn64)
 * 
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using UnityEngine;

namespace OiranStudio.Ignorance4
{
    /// <summary>
    /// Cut-down version of the Mirror NetworkWriter. Binary stream Writer. Supports simple types, buffers, arrays, structs, and nested types
    /// Original code is licensed under MIT license.
    /// </summary>
    public class LowFatBufferWriter
    {
        public const int MaxStringLength = 1024 * 32;

        // create writer immediately with it's own buffer so no one can mess with it and so that we can resize it.
        // note: BinaryWriter allocates too much, so we only use a MemoryStream
        // => 1500 bytes by default because on average, most packets will be <= MTU
        byte[] buffer = new byte[1500];

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position;

        int length;

        public int Length
        {
            get => length;
            private set
            {
                EnsureCapacity(value);
                length = value;
                if (Position > length)
                    Position = length;
            }
        }

        void EnsureCapacity(int value)
        {
            if (buffer.Length < value)
            {
                int capacity = Math.Max(value, buffer.Length * 2);
                Array.Resize(ref buffer, capacity);
            }
        }

        // MemoryStream has 3 values: Position, Length and Capacity.
        // Position is used to indicate where we are writing
        // Length is how much data we have written
        // capacity is how much memory we have allocated
        // ToArray returns all the data we have written,  regardless of the current position
        public byte[] ToArray()
        {
            byte[] data = new byte[Length];
            Array.ConstrainedCopy(buffer, 0, data, 0, Length);
            return data;
        }

        // Gets the serialized data in an ArraySegment<byte>
        // this is similar to ToArray(),  but it gets the data in O(1)
        // and without allocations.
        // Do not write anything else or modify the NetworkWriter
        // while you are using the ArraySegment
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer, 0, length);
        }

        // reset both the position and length of the stream,  but leaves the capacity the same
        // so that we can reuse this writer without extra allocations
        public void SetLength(int value)
        {
            Length = value;
        }

        public void WriteByte(byte value)
        {
            if (Position >= Length)
            {
                Length += 1;
            }

            buffer[Position++] = value;
        }


        // for byte arrays with consistent size, where the reader knows how many to read
        // (like a packet opcode that's always the same)
        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            // no null check because we would need to write size info for that too (hence WriteBytesAndSize)
            if (Position + count > Length)
            {
                Length = Position + count;
            }
            Array.ConstrainedCopy(buffer, offset, this.buffer, Position, count);
            Position += count;
        }
    }

    /// <summary>
    /// PooledLowFatBufferWriter to be used with <see cref="LowFatBufferWriterPool">LowFatBufferWriterPool</see>
    /// </summary>
    public class PooledLowFatBufferWriter : LowFatBufferWriter, IDisposable
    {
        public void Dispose()
        {
            LowFatBufferWriterPool.Recycle(this);
        }
    }

    /// <summary>
    /// Pool of LowFat NetworkWriters
    /// <para>Use this pool instead of <see cref="NetworkWriter">NetworkWriter</see> to reduce memory allocation</para>
    /// <para>Use <see cref="Capacity">Capacity</see> to change size of pool</para>
    /// </summary>
    public static class LowFatBufferWriterPool
    {
        /// <summary>
        /// Size of the pool
        /// <para>If pool is too small getting writers will causes memory allocation</para>
        /// <para>Default value: 100 </para>
        /// </summary>
        public static int Capacity
        {
            get => pool.Length;
            set
            {
                // resize the array
                Array.Resize(ref pool, value);

                // if capacity is smaller than before, then we need to adjust
                // 'next' so it doesn't point to an index out of range
                // -> if we set '0' then next = min(_, 0-1) => -1
                // -> if we set '2' then next = min(_, 2-1) =>  1
                next = Mathf.Min(next, pool.Length - 1);
            }
        }

        /// <summary>
        /// Mirror usually only uses up to 4 writes in nested usings,
        /// 100 is a good margin for edge cases when users need a lot writers at
        /// the same time.
        ///
        /// <para>keep in mind, most entries of the pool will be null in most cases</para>
        /// </summary>
        ///
        /// Note: we use an Array instead of a Stack because it's significantly
        ///       faster: https://github.com/vis2k/Mirror/issues/1614
        static LowFatBufferWriter[] pool = new LowFatBufferWriter[100];

        static int next = -1;

        /// <summary>
        /// Get the next writer in the pool
        /// <para>If pool is empty, creates a new Writer</para>
        /// </summary>
        public static LowFatBufferWriter GetWriter()
        {
            if (next == -1)
            {
                return new LowFatBufferWriter();
            }

            LowFatBufferWriter writer = pool[next];
            pool[next] = null;
            next--;

            // reset cached writer length and position
            writer.SetLength(0);
            return writer;
        }

        /// <summary>
        /// Puts writer back into pool
        /// <para>When pool is full, the extra writer is left for the GC</para>
        /// </summary>
        public static void Recycle(LowFatBufferWriter writer)
        {
            if (next < pool.Length - 1)
            {
                next++;
                pool[next] = writer;
            }
            else
            {
                Debug.LogWarning("LowFatBufferWriterPool: I'm bloated. Leaving extra writer for GC to sweep up.");
            }
        }
    }

}
