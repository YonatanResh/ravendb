﻿using System.Threading;
using System.Threading.Tasks;
using FastTests;
using FastTests.Graph;
using FastTests.Server.Replication;
using FastTests.Utils;
using Raven.Client.Documents.Operations.Revisions;
using Raven.Server.Documents;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Issues
{
    public class RavenDB_16961 : ReplicationTestBase
    {
        public RavenDB_16961(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task StripRevisionFlagFromTombstone()
        {
            using (var store = GetDocumentStore())
            {
                await RevisionsHelper.SetupRevisions(store, Server.ServerStore, new RevisionsConfiguration()
                {
                    Default = new RevisionsCollectionConfiguration()
                    {
                        Disabled = false
                    }
                });
                var user = new User() {Name = "Toli"};
                using (var session = store.OpenAsyncSession())
                {
                    for (int i = 0; i < 3; i++)
                    {
                        user.Age = i;
                        await session.StoreAsync(user, "users/1");
                        await session.SaveChangesAsync();
                    }
                    session.Delete("users/1");
                    await session.SaveChangesAsync();
                }

                await RevisionsHelper.SetupRevisions(store, Server.ServerStore, new RevisionsConfiguration());

                var db = await GetDocumentDatabaseInstanceFor(store, store.Database);
                using (var token = new OperationCancelToken(db.Configuration.Databases.OperationTimeout.AsTimeSpan, db.DatabaseShutdown, CancellationToken.None))
                {
                    await db.DocumentsStorage.RevisionsStorage.EnforceConfiguration(_ => { }, token);
                }


                using (db.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext ctx))
                using (ctx.OpenReadTransaction())
                {
                    var tombstone = db.DocumentsStorage.GetDocumentOrTombstone(ctx, "users/1");
                    Assert.False(tombstone.Tombstone.Flags.Contain(DocumentFlags.HasRevisions));
                }

            }
        }

        [Fact]
        public async Task StripRevisionFlagFromTombstoneWithExternalReplication()
        {
            using (var store1 = GetDocumentStore(new Options
            {
                ModifyDatabaseName = s => $"{s}_FooBar-1"
            }))
            using (var store2 = GetDocumentStore(new Options
            {
                ModifyDatabaseName = s => $"{s}_FooBar-2"
            }))
            {
                await SetupReplicationAsync(store1, store2);
                await RevisionsHelper.SetupRevisions(store1, Server.ServerStore, new RevisionsConfiguration()
                {
                    Default = new RevisionsCollectionConfiguration()
                    {
                        Disabled = false
                    }
                });
                var user = new User() { Name = "Toli" };
                using (var session = store1.OpenAsyncSession())
                {
                    for (int i = 0; i < 3; i++)
                    {
                        user.Age = i;
                        await session.StoreAsync(user, "users/1");
                        await session.SaveChangesAsync();
                    }
                    session.Delete("users/1");
                    await session.SaveChangesAsync();
                }

                await RevisionsHelper.SetupRevisions(store1, Server.ServerStore, new RevisionsConfiguration());

                var db = await GetDocumentDatabaseInstanceFor(store1, store1.Database);
                using (var token = new OperationCancelToken(db.Configuration.Databases.OperationTimeout.AsTimeSpan, db.DatabaseShutdown, CancellationToken.None))
                    await db.DocumentsStorage.RevisionsStorage.EnforceConfiguration(_ => { }, token);


                using (db.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext ctx))
                using (ctx.OpenReadTransaction())
                {
                    var tombstone = db.DocumentsStorage.GetDocumentOrTombstone(ctx, "users/1");
                    Assert.False(tombstone.Tombstone.Flags.Contain(DocumentFlags.HasRevisions));
                }

                db = await GetDocumentDatabaseInstanceFor(store2, store2.Database);
                await WaitForValueAsync(async () =>
                    {
                        Task.Delay(15);
                        using (db.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext ctx))
                        using (ctx.OpenReadTransaction())
                        {
                            var tombstone = db.DocumentsStorage.GetDocumentOrTombstone(ctx, "users/1");
                            return tombstone.Tombstone.Flags.Contain(DocumentFlags.HasRevisions);
                        }
                    }, false
                );
            }
        }
    }
}