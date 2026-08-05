using Xunit;

// API tests share one configured database. Serial execution makes their initial
// state deterministic both for the local InMemory provider and CI PostgreSQL.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
