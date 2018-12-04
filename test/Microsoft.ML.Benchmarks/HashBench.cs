﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using BenchmarkDotNet.Attributes;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Api;
using Microsoft.ML.Runtime.Learners;
using System.Linq;
using Microsoft.ML.Transforms.Conversions;

namespace Microsoft.ML.Benchmarks
{
    public class HashBench
    {
        private sealed class Row : IRow
        {
            public Schema Schema { get; }

            public long Position { get; set; }

            public long Batch => 0;
            public ValueGetter<UInt128> GetIdGetter()
                => (ref UInt128 val) => val = new UInt128((ulong)Position, 0);

            private readonly Delegate _getter;

            public bool IsColumnActive(int col)
            {
                if (col != 0)
                    throw new Exception();
                return true;
            }

            public ValueGetter<TValue> GetGetter<TValue>(int col)
            {
                if (col != 0)
                    throw new Exception();
                if (_getter is ValueGetter<TValue> typedGetter)
                    return typedGetter;
                throw new Exception();
            }

            public static Row Create<T>(ColumnType type, ValueGetter<T> getter)
            {
                if (type.RawType != typeof(T))
                    throw new Exception();
                return new Row(type, getter);
            }

            private Row(ColumnType type, Delegate getter)
            {
                var builder = new SchemaBuilder();
                builder.AddColumn("Foo", type, null);
                Schema = builder.GetSchema();
                _getter = getter;
            }
        }

        private const int Count = 100_000;

        private readonly IHostEnvironment _env = new MLContext();

        private Row _inRow;
        private ValueGetter<uint> _getter;
        private ValueGetter<VBuffer<uint>> _vecGetter;

        private void InitMap<T>(T val, ColumnType type, int hashBits = 20, ValueGetter<T> getter = null)
        {
            if (getter == null)
                getter = (ref T dst) => dst = val;
            _inRow = Row.Create(type, getter);
            // One million features is a nice, typical number.
            var info = new HashingTransformer.ColumnInfo("Foo", "Bar", hashBits: hashBits);
            var xf = new HashingTransformer(_env, new[] { info });
            var mapper = xf.GetRowToRowMapper(_inRow.Schema);
            mapper.OutputSchema.TryGetColumnIndex("Bar", out int outCol);
            var outRow = mapper.GetRow(_inRow, c => c == outCol, out var _);
            if (type is VectorType)
                _vecGetter = outRow.GetGetter<VBuffer<uint>>(outCol);
            else
                _getter = outRow.GetGetter<uint>(outCol);
        }

        /// <summary>
        /// All the scalar mappers have the same output type.
        /// </summary>
        private void RunScalar()
        {
            uint val = default;
            for (int i = 0; i < Count; ++i)
            {
                _getter(ref val);
                ++_inRow.Position;
            }
        }

        private void InitDenseVecMap<T>(T[] vals, PrimitiveType itemType, int hashBits = 20)
        {
            var vbuf = new VBuffer<T>(vals.Length, vals);
            InitMap(vbuf, new VectorType(itemType, vals.Length), hashBits, vbuf.CopyTo);
        }

        /// <summary>
        /// All the vector mappers have the same output type.
        /// </summary>
        private void RunVector()
        {
            VBuffer<uint> val = default;
            for (int i = 0; i < Count; ++i)
            {
                _vecGetter(ref val);
                ++_inRow.Position;
            }
        }

        [GlobalSetup(Target = nameof(HashScalarString))]
        public void SetupHashScalarString()
        {
            InitMap("Hello".AsMemory(), TextType.Instance);
        }

        [Benchmark]
        public void HashScalarString()
        {
            RunScalar();
        }

        [GlobalSetup(Target = nameof(HashScalarFloat))]
        public void SetupHashScalarFloat()
        {
            InitMap(5.0f, NumberType.R4);
        }

        [Benchmark]
        public void HashScalarFloat()
        {
            RunScalar();
        }

        [GlobalSetup(Target = nameof(HashScalarDouble))]
        public void SetupHashScalarDouble()
        {
            InitMap(5.0, NumberType.R8);
        }

        [Benchmark]
        public void HashScalarDouble()
        {
            RunScalar();
        }

        [GlobalSetup(Target = nameof(HashScalarKey))]
        public void SetupHashScalarKey()
        {
            InitMap(6u, new KeyType(typeof(uint), 0, 100));
        }

        [Benchmark]
        public void HashScalarKey()
        {
            RunScalar();
        }



        [GlobalSetup(Target = nameof(HashVectorString))]
        public void SetupHashVectorString()
        {
            var tokens = "Hello my friend, stay awhile and listen! ".Split().Select(token => token.AsMemory()).ToArray();
            InitDenseVecMap(tokens, TextType.Instance);
        }

        [Benchmark]
        public void HashVectorString()
        {
            RunVector();
        }

        [GlobalSetup(Target = nameof(HashVectorFloat))]
        public void SetupHashVectorFloat()
        {
            InitDenseVecMap(new[] { 1f, 2f, 3f, 4f, 5f }, NumberType.R4);
        }

        [Benchmark]
        public void HashVectorFloat()
        {
            RunVector();
        }

        [GlobalSetup(Target = nameof(HashVectorDouble))]
        public void SetupHashVectorDouble()
        {
            InitDenseVecMap(new[] { 1d, 2d, 3d, 4d, 5d }, NumberType.R8);
        }

        [Benchmark]
        public void HashVectorDouble()
        {
            RunVector();
        }

        [GlobalSetup(Target = nameof(HashVectorKey))]
        public void SetupHashVectorKey()
        {
            InitDenseVecMap(new[] { 1u, 2u, 0u, 4u, 5u }, new KeyType(typeof(uint), 0, 100));
        }

        [Benchmark]
        public void HashVectorKey()
        {
            RunVector();
        }
    }
}
