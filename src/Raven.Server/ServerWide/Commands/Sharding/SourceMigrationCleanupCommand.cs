﻿using System;
using System.Linq;
using Raven.Client.ServerWide;
using Sparrow.Json.Parsing;

namespace Raven.Server.ServerWide.Commands.Sharding
{
    public class SourceMigrationCleanupCommand : UpdateDatabaseCommand
    {
        public int Bucket;
        public long MigrationIndex;
        public string Node;

        public SourceMigrationCleanupCommand(){}

        public SourceMigrationCleanupCommand(int bucket, long migrationIndex, string node, string database, string raftId) : base(database, raftId)
        {
            Bucket = bucket;
            MigrationIndex = migrationIndex;
            Node = node;
        }

        public override void UpdateDatabaseRecord(DatabaseRecord record, long etag)
        {
            if (record.BucketMigrations.TryGetValue(Bucket, out var migration) == false)
                throw new InvalidOperationException($"Bucket '{Bucket}' not found in the migration buckets");

            if (migration.MigrationIndex != MigrationIndex)
                throw new InvalidOperationException($"Wrong migration index. Expected: '{MigrationIndex}', Actual: '{migration.MigrationIndex}'");

            if (migration.Status != MigrationStatus.OwnershipTransferred)
                throw new InvalidOperationException($"Expected status is '{MigrationStatus.Moved}', Actual '{migration.Status}'");

            if (migration.ConfirmedSourceCleanup.Contains(Node) == false)
                migration.ConfirmedSourceCleanup.Add(Node);

            var shardTopology = record.Shards[migration.SourceShard];
            if (shardTopology.AllNodes.All(migration.ConfirmedSourceCleanup.Contains))
            {
                record.BucketMigrations.Remove(Bucket);
            }
        }

        public override void FillJson(DynamicJsonValue json)
        {
            json[nameof(Node)] = Node;
            json[nameof(Bucket)] = Bucket;
            json[nameof(MigrationIndex)] = MigrationIndex;
        }
    }
}