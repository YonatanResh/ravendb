﻿using FastTests;
using Raven.Server.Documents.Queries;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_11284 : NoDisposalNeeded
    {
        public RavenDB_11284(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Should_cache_metadata_of_queries_without_parameters()
        {
            var cache = new QueryMetadataCache();

            Assert.False(cache.TryGetMetadata(new IndexQueryServerSide("from Users order by Name"), addSpatialProperties: false, out var metadataHash, out var metadata));

            Assert.NotEqual((ulong)0, metadataHash);

            cache.MaybeAddToCache(new QueryMetadata("from Users order by Name", null, metadataHash), "test");

            Assert.True(cache.TryGetMetadata(new IndexQueryServerSide("from Users order by Name"), addSpatialProperties: false, out metadataHash, out metadata));
        }
    }
}
