﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Runtime.InteropServices;

namespace SiliconStudio.Core.Storage
{
    /// <summary>
    /// A hash to uniquely identify data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    [DataContract("ObjectId")]
    public unsafe partial struct ObjectId : IEquatable<ObjectId>, IComparable<ObjectId>
    {
        public static readonly ObjectId Empty = new ObjectId();

        // SHA1 hash size is 160 bits.
        public const int HashSize = 16;
        public const int HashStringLength = HashSize * 2;
        private const int HashSizeInUInt = HashSize / sizeof(uint);
        private const string HexDigits = "0123456789abcdef";
        private uint hash1, hash2, hash3, hash4;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectId"/> struct.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <exception cref="System.ArgumentNullException">hash</exception>
        /// <exception cref="System.InvalidOperationException">ObjectId value doesn't match expected size.</exception>
        public ObjectId(byte[] hash)
        {
            if (hash == null) throw new ArgumentNullException("hash");

            if (hash.Length != HashSize)
                throw new InvalidOperationException("ObjectId value doesn't match expected size.");

            fixed (byte* hashSource = hash)
            {
                var hashSourceCurrent = (uint*)hashSource;
                hash1 = *hashSourceCurrent++;
                hash2 = *hashSourceCurrent++;
                hash3 = *hashSourceCurrent++;
                hash4 = *hashSourceCurrent;
            }
        }

        public ObjectId(uint hash1, uint hash2, uint hash3, uint hash4)
        {
            this.hash1 = hash1;
            this.hash2 = hash2;
            this.hash3 = hash3;
            this.hash4 = hash4;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="ObjectId"/> to <see cref="byte[]"/>.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The result of the conversion.</returns>
        public static explicit operator byte[](ObjectId objectId)
        {
            var result = new byte[HashSize];
            var hashSource = &objectId.hash1;
            fixed (byte* hashDest = result)
            {
                var hashSourceCurrent = (uint*)hashSource;
                var hashDestCurrent = (uint*)hashDest;
                for (int i = 0; i < HashSizeInUInt; ++i)
                    *hashDestCurrent++ = *hashSourceCurrent++;
            }
            return result;
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(ObjectId left, ObjectId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(ObjectId left, ObjectId right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Tries to parse an <see cref="ObjectId"/> from a string.
        /// </summary>
        /// <param name="input">The input hexa string.</param>
        /// <param name="result">The result ObjectId.</param>
        /// <returns><c>true</c> if parsing was successfull, <c>false</c> otherwise</returns>
        public static bool TryParse(string input, out ObjectId result)
        {
            if (input.Length != HashStringLength)
            {
                result = Empty;
                return false;
            }

            var hash = new byte[HashSize];
            for (int i = 0; i < HashStringLength; i += 2)
            {
                char c1 = input[i];
                char c2 = input[i + 1];

                int digit1, digit2;
                if (((digit1 = HexDigits.IndexOf(c1)) == -1)
                    || ((digit2 = HexDigits.IndexOf(c2)) == -1))
                {
                    result = Empty;
                    return false;
                }

                hash[i >> 1] = (byte)((digit1 << 4) | digit2);
            }

            result = new ObjectId(hash);
            return true;
        }

        /// <inheritdoc/>
        public bool Equals(ObjectId other)
        {
            // Compare content
            fixed (uint* xPtr = &hash1)
            {
                var x1 = xPtr;
                var y1 = &other.hash1;

                for (int i = 0; i < HashSizeInUInt; ++i)
                {
                    if (*x1++ != *y1++)
                        return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ObjectId && Equals((ObjectId)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            fixed (uint* objPtr = &hash1)
            {
                var obj1 = (int*)objPtr;
                return *obj1;
            }
        }

        /// <inheritdoc/>
        public int CompareTo(ObjectId other)
        {
            // Compare content
            fixed (uint* xPtr = &hash1)
            {
                var x1 = xPtr;
                var y1 = &other.hash1;

                for (int i = 0; i < HashSizeInUInt; ++i)
                {
                    var compareResult = (*x1++).CompareTo(*y1++);
                    if (compareResult != 0)
                        return compareResult;
                }
            }

            return 0;
        }

        public override string ToString()
        {
            var c = new char[HashStringLength];

            fixed (uint* hashStart = &hash1)
            {
                var hashBytes = (byte*)hashStart;
                for (int i = 0; i < HashStringLength; ++i)
                {
                    int index0 = i >> 1;
                    var b = ((byte)(hashBytes[index0] >> 4));
                    c[i++] = HexDigits[b];

                    b = ((byte)(hashBytes[index0] & 0x0F));
                    c[i] = HexDigits[b];
                }
            }

            return new string(c);
        }

        /// <summary>
        /// Gets a <see cref="Guid"/> from this object identifier.
        /// </summary>
        /// <returns>Guid.</returns>
        public Guid ToGuid()
        {
            fixed (void* hashStart = &hash1)
            {
                return *(Guid*)hashStart;
            }
        }

        /// <summary>
        /// News this instance.
        /// </summary>
        /// <returns>ObjectId.</returns>
        public static ObjectId New()
        {
            return FromBytes(Guid.NewGuid().ToByteArray());
        }

        /// <summary>
        /// Computes a hash from a byte buffer.
        /// </summary>
        /// <param name="buffer">The byte buffer.</param>
        /// <returns>The hash of the object.</returns>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        public static ObjectId FromBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");

            return FromBytes(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Computes a hash from a byte buffer.
        /// </summary>
        /// <param name="buffer">The byte buffer.</param>
        /// <param name="offset">The offset into the buffer.</param>
        /// <param name="count">The number of bytes to read from the buffer starting at offset position.</param>
        /// <returns>The hash of the object.</returns>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        public static ObjectId FromBytes(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");

            var builder = new ObjectIdBuilder();
            builder.Write(buffer, offset, count);
            return builder.ComputeHash();
        }
    }
}