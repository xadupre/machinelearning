﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.ImageAnalytics;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ML.StaticPipelineTesting
{
    public sealed class ImageAnalyticsTests : BaseTestClassWithConsole
    {
        public ImageAnalyticsTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void SimpleImageSmokeTest()
        {
            var env = new MLContext(0);

            var reader = TextLoader.CreateReader(env,
                ctx => ctx.LoadText(0).LoadAsImage().AsGrayscale().Resize(10, 8).ExtractPixels());

            var schema = reader.AsDynamic.GetOutputSchema();
            Assert.True(schema.TryGetColumnIndex("Data", out int col), "Could not find 'Data' column");
            var type = schema.GetColumnType(col);
            var vecType = type as VectorType;
            Assert.True(vecType?.Size > 0, $"Type was supposed to be known size vector but was instead '{type}'");
            Assert.Equal(NumberType.R4, vecType.ItemType);
            Assert.Equal(3, vecType.Dimensions.Length);
            Assert.Equal(3, vecType.Dimensions[0]);
            Assert.Equal(8, vecType.Dimensions[1]);
            Assert.Equal(10, vecType.Dimensions[2]);

            var readAsImage = TextLoader.CreateReader(env,
                ctx => ctx.LoadText(0).LoadAsImage());
            var est = readAsImage.MakeNewEstimator().Append(r => r.AsGrayscale().Resize(10, 8).ExtractPixels());
            var pipe= readAsImage.Append(est);
        }
    }
}
