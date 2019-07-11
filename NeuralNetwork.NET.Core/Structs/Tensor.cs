﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using NeuralNetwork.NET.Core.Enums;

namespace NeuralNetwork.NET.Core.Structs
{
    /// <summary>
    /// A readonly struct that holds the info on an unmanaged memory area that has been allocated
    /// </summary>
    [DebuggerTypeProxy(typeof(_TensorProxy))]
    [DebuggerDisplay("N: {N}, CHW: {CHW}, Size: {Size}")]
    internal readonly struct Tensor
    {
        /// <summary>
        /// The number of rows in the current <see cref="Tensor"/>
        /// </summary>
        public readonly int N;

        /// <summary>
        /// The size of the CHW channels in the current <see cref="Tensor"/>
        /// </summary>
        public readonly int CHW;

        /// <summary>
        /// The total size (the number of <see cref="float"/> values) in the current <see cref="Tensor"/>
        /// </summary>
        public int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => N * CHW;
        }

        /// <summary>
        /// The <see cref="float"/> buffer with the underlying data for the current instance
        /// </summary>
        [NotNull]
        private readonly float[] Data;

        /// <summary>
        /// Gets a <see cref="Span{T}"/> instance for the current <see cref="Tensor"/> data
        /// </summary>
        public Span<float> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Data.AsSpan(0, Size);
        }

        // Private constructor
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Tensor([NotNull] float[] data, int n, int chw)
        {
            Data = data;
            N = n;
            CHW = chw;
        }

        /// <summary>
        /// Creates a new <see cref="Tensor"/> instance with the specified shape
        /// </summary>
        /// <param name="n">The height of the <see cref="Tensor"/></param>
        /// <param name="chw">The width of the <see cref="Tensor"/></param>
        /// <param name="mode">The desired allocation mode to use when creating the new <see cref="Tensor"/> instance</param>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Tensor New(int n, int chw, AllocationMode mode = AllocationMode.Default)
        {
            var data = ArrayPool<float>.Shared.Rent(n * chw);
            var tensor = new Tensor(data, n, chw);
            if (mode == AllocationMode.Clean) tensor.Span.Clear();

            return tensor;
        }

        /// <summary>
        /// Creates a new instance with the same shape as the input <see cref="Tensor"/>
        /// </summary>
        /// <param name="tensor">The <see cref="Tensor"/> to use to copy the shape</param>
        /// <param name="mode">The desired allocation mode to use when creating the new <see cref="Tensor"/> instance</param>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Tensor Like(in Tensor tensor, AllocationMode mode = AllocationMode.Default) => New(tensor.N, tensor.CHW, mode);

        #region Tools

        /// <summary>
        /// Creates a new instance by wrapping the current memory area
        /// </summary>
        /// <param name="n">The height of the final <see cref="Tensor"/></param>
        /// <param name="chw">The width of the final <see cref="Tensor"/></param>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tensor Reshape(int n, int chw)
        {
            if (n * chw != Size) throw new ArgumentException("Invalid input resized shape");
            return new Tensor(Data, n, chw);
        }

        /// <summary>
        /// Checks whether or not the current instance has the same shape of the input <see cref="Tensor"/>
        /// </summary>
        /// <param name="tensor">The instance to compare</param>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchShape(in Tensor tensor) => N == tensor.N && CHW == tensor.CHW;

        /// <summary>
        /// Checks whether or not the current instance has the same shape as the input arguments
        /// </summary>
        /// <param name="n">The height of the <see cref="Tensor"/></param>
        /// <param name="chw">The width of the <see cref="Tensor"/></param>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MatchShape(int n, int chw) => N == n && CHW == chw;

        /// <summary>
        /// Overwrites the contents of the current instance with the input <see cref="Tensor"/>
        /// </summary>
        /// <param name="tensor">The input <see cref="Tensor"/> to copy</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Overwrite(in Tensor tensor)
        {
            if (tensor.N != N || tensor.CHW != CHW) throw new ArgumentException("The input tensor doesn't have the same size as the target");

            tensor.Span.CopyTo(Span);
        }

        /// <summary>
        /// Duplicates the current instance to an output <see cref="Tensor"/>
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Tensor Duplicate()
        {
            var tensor = New(N, CHW);
            Span.CopyTo(tensor.Span);

            return tensor;
        }

        #endregion

        #region Debug

        /// <summary>
        /// A proxy type to debug instances of the <see cref="Tensor"/> <see langword="struct"/>
        /// </summary>
        private readonly struct _TensorProxy
        {
            /// <summary>
            /// Gets a preview of the underlying memory area wrapped by this instance
            /// </summary>
            [NotNull]
            [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
            [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
            public IEnumerable<float[]> RowsPreview { get; }

            /// <summary>
            /// The maximum number of rows to display in the debugger
            /// </summary>
            private const int MaxRows = 10;

            /// <summary>
            /// The maximum number of total items to display in the debugger
            /// </summary>
            private const int MaxItems = 30000;

            [SuppressMessage("ReSharper", "UnusedMember.Local")]
            public _TensorProxy(Tensor obj)
            {
                // Iterator to delay the creation of the debugger display rows until requested by the user
                IEnumerable<float[]> ExtractRows()
                {
                    int
                        cappedRows = MaxItems / obj.CHW,
                        rows = Math.Min(MaxRows, cappedRows);
                    for (int i = 0; i < rows; i++)
                        yield return obj.Span.Slice(i * obj.CHW, obj.CHW).ToArray();
                }

                RowsPreview = ExtractRows();
            }
        }

        #endregion
    }
}
