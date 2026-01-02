using Xunit;

namespace Integration.Tests;

/// <summary>
/// Collection definitions for integration tests.
/// Tests that use METRICS_SQLITE_PATH env var cannot run in parallel 
/// because the env var is global to the process.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection : ICollectionFixture<object>
{
}
