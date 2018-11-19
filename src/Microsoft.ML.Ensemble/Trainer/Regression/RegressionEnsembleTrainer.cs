﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.Ensemble.EntryPoints;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.CommandLine;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.Ensemble;
using Microsoft.ML.Runtime.Ensemble.OutputCombiners;
using Microsoft.ML.Runtime.Ensemble.Selector;
using Microsoft.ML.Runtime.Internal.Internallearn;
using Microsoft.ML.Trainers.Online;

[assembly: LoadableClass(typeof(RegressionEnsembleTrainer), typeof(RegressionEnsembleTrainer.Arguments),
    new[] { typeof(SignatureRegressorTrainer), typeof(SignatureTrainer) },
    RegressionEnsembleTrainer.UserNameValue,
    RegressionEnsembleTrainer.LoadNameValue)]

[assembly: LoadableClass(typeof(RegressionEnsembleTrainer), typeof(RegressionEnsembleTrainer.Arguments), typeof(SignatureModelCombiner),
    "Regression Ensemble Model Combiner", RegressionEnsembleTrainer.LoadNameValue)]

namespace Microsoft.ML.Runtime.Ensemble
{
    using TScalarPredictor = IPredictorProducing<Single>;
    /*internal*/public sealed class RegressionEnsembleTrainer : EnsembleTrainerBase<Single, TScalarPredictor,
       IRegressionSubModelSelector, IRegressionOutputCombiner>,
       IModelCombiner
    {
        public const string LoadNameValue = "EnsembleRegression";
        public const string UserNameValue = "Regression Ensemble (bagging, stacking, etc)";

        public sealed class Arguments : ArgumentsBase
        {
            [Argument(ArgumentType.Multiple, HelpText = "Algorithm to prune the base learners for selective Ensemble", ShortName = "pt", SortOrder = 4)]
            [TGUI(Label = "Sub-Model Selector(pruning) Type", Description = "Algorithm to prune the base learners for selective Ensemble")]
            public ISupportRegressionSubModelSelectorFactory SubModelSelectorType = new AllSelectorFactory();

            [Argument(ArgumentType.Multiple, HelpText = "Output combiner", ShortName = "oc", SortOrder = 5)]
            [TGUI(Label = "Output combiner", Description = "Output combiner type")]
            public ISupportRegressionOutputCombinerFactory OutputCombiner = new MedianFactory();

            // REVIEW: If we make this public again it should be an *estimator* of this type of predictor, rather than the (deprecated) ITrainer.
            [Argument(ArgumentType.Multiple, HelpText = "Base predictor type", ShortName = "bp,basePredictorTypes", SortOrder = 1, Visibility = ArgumentAttribute.VisibilityType.CmdLineOnly, SignatureType = typeof(SignatureRegressorTrainer))]
            public IComponentFactory<ITrainer<TScalarPredictor>>[] BasePredictors;

            /*internal*/public override IComponentFactory<ITrainer<TScalarPredictor>>[] GetPredictorFactories() => BasePredictors;

            public Arguments()
            {
                BasePredictors = new[]
                {
                    ComponentFactoryUtils.CreateFromFunction(
                        env => new OnlineGradientDescentTrainer(env, DefaultColumnNames.Label, DefaultColumnNames.Features))
                };
            }
        }

        private readonly ISupportRegressionOutputCombinerFactory _outputCombiner;

        public RegressionEnsembleTrainer(IHostEnvironment env, Arguments args)
            : base(args, env, LoadNameValue)
        {
            SubModelSelector = args.SubModelSelectorType.CreateComponent(Host);
            _outputCombiner = args.OutputCombiner;
            Combiner = args.OutputCombiner.CreateComponent(Host);
        }

        private RegressionEnsembleTrainer(IHostEnvironment env, Arguments args, PredictionKind predictionKind)
            : this(env, args)
        {
            Host.CheckParam(predictionKind == PredictionKind.Regression, nameof(PredictionKind));
        }

        public override PredictionKind PredictionKind => PredictionKind.Regression;

        /*private*/ protected override TScalarPredictor CreatePredictor(List<FeatureSubsetModel<TScalarPredictor>> models)
        {
            return new EnsemblePredictor(Host, PredictionKind, CreateModels<TScalarPredictor>(models), Combiner);
        }

        public IPredictor CombineModels(IEnumerable<IPredictor> models)
        {
            Host.CheckValue(models, nameof(models));
            Host.CheckParam(models.All(m => m is TScalarPredictor), nameof(models));

            var combiner = _outputCombiner.CreateComponent(Host);
            var p = models.First();

            var predictor = new EnsemblePredictor(Host, p.PredictionKind,
                    models.Select(k => new FeatureSubsetModel<TScalarPredictor>((TScalarPredictor)k)).ToArray(), combiner);

            return predictor;
        }
    }
}
