namespace LabelVerification.Tests;

[CollectionDefinition(nameof(OcrSequentialCollection), DisableParallelization = true)]
public sealed class OcrSequentialCollection : ICollectionFixture<OcrEngineFixture>;
