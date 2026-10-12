using Xunit;

// Host startup shares Serilog's static bootstrap logger. Individual concurrency tests
// still run parallel requests against one host, with independent DbContexts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
