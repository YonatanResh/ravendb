﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Parquet;
using Parquet.Data;
using Raven.Client.Documents.Changes;
using Raven.Client.Documents.Operations.Counters;
using Raven.Client.Documents.Operations.ETL;
using Raven.Server.Documents.ETL.Providers.S3;
using Raven.Server.Documents.Replication.ReplicationItems;
using Raven.Server.Documents.TimeSeries;
using Raven.Server.ServerWide;
using Raven.Server.ServerWide.Context;
using Sparrow;

namespace Raven.Server.Documents.ETL.Providers.Parquet
{
    public class ParquetEtl : EtlProcess<ToParquetItem, RowGroups, ParquetEtlConfiguration, ParquetEtlConnectionString>
    {
        public const string ParquetEtlTag = "Parquet ETL";

        public readonly S3EtlMetricsCountersManager S3Metrics = new S3EtlMetricsCountersManager();

        private Timer _timer;
        private const long MinTimeToWait = 1000;

        public ParquetEtl(Transformation transformation, ParquetEtlConfiguration configuration, DocumentDatabase database, ServerStore serverStore)
            : base(transformation, configuration, database, serverStore, ParquetEtlTag)
        {
            Metrics = S3Metrics;
            var etlFrequency = configuration.ETLFrequency;
            var dueTime = GetDueTime(etlFrequency);

            _timer = new Timer(_ => _waitForChanges.Set(), null, dueTime, etlFrequency);
        }

        private TimeSpan GetDueTime(TimeSpan etlFrequency)
        {
            var state = GetProcessState(Database, Configuration.Name, Transformation.Name);
            if (state.LastBatchTime <= 0) 
                return TimeSpan.Zero;
            
            // todo test this

            var nowMs = Database.Time.GetUtcNow().EnsureMilliseconds().Ticks / 10_000;
            var timeSinceLastBatch = nowMs - state.LastBatchTime;

            var dueTime = etlFrequency.TotalMilliseconds - timeSinceLastBatch;

            if (dueTime < MinTimeToWait)
                return TimeSpan.Zero;

            return TimeSpan.FromMilliseconds(dueTime);
        }

        public override EtlType EtlType => EtlType.Parquet;

        private static readonly IEnumerator<ToParquetItem> EmptyEnumerator = Enumerable.Empty<ToParquetItem>().GetEnumerator();

        protected override IEnumerator<ToParquetItem> ConvertDocsEnumerator(DocumentsOperationContext context, IEnumerator<Document> docs, string collection)
        {
            return new DocumentsToParquetItems(docs, collection);
        }

        protected override IEnumerator<ToParquetItem> ConvertTombstonesEnumerator(DocumentsOperationContext context, IEnumerator<Tombstone> tombstones, string collection, bool trackAttachments)
        {
            return EmptyEnumerator; // todo
            throw new NotSupportedException("Tombstones aren't supported by S3 ETL");
        }

        protected override IEnumerator<ToParquetItem> ConvertAttachmentTombstonesEnumerator(DocumentsOperationContext context, IEnumerator<Tombstone> tombstones, List<string> collections)
        {
            throw new NotSupportedException("Attachment tombstones aren't supported by S3 ETL");
        }

        protected override IEnumerator<ToParquetItem> ConvertCountersEnumerator(DocumentsOperationContext context, IEnumerator<CounterGroupDetail> counters, string collection)
        {
            throw new NotSupportedException("Counters aren't supported by S3 ETL");
        }

        protected override IEnumerator<ToParquetItem> ConvertTimeSeriesEnumerator(DocumentsOperationContext context, IEnumerator<TimeSeriesSegmentEntry> timeSeries, string collection)
        {
            // todo
            throw new NotImplementedException();
        }

        protected override IEnumerator<ToParquetItem> ConvertTimeSeriesDeletedRangeEnumerator(DocumentsOperationContext context, IEnumerator<TimeSeriesDeletedRangeItem> timeSeries, string collection)
        {
            throw new NotSupportedException("Time series deletes aren't supported by S3 ETL");
        }

        protected override bool ShouldUpdateOnLastBatch => true;


        public override void NotifyAboutWork(DatabaseChange change)
        {
            // intentionally not setting _waitForChanges here
            // _waitForChanges is being set by the timer
        }

        protected override bool ShouldTrackAttachmentTombstones()
        {
            return false;
        }

        protected override bool ShouldFilterOutHiLoDocument()
        {
            return true;
        }

        public override bool ShouldTrackCounters() => false;

        public override bool ShouldTrackTimeSeries() => false;

        protected override EtlTransformer<ToParquetItem, RowGroups> GetTransformer(DocumentsOperationContext context)
        {
            return new ParquetDocumentTransformer(Transformation, Database, context, Configuration);
        }

        protected override int LoadInternal(IEnumerable<RowGroups> records, DocumentsOperationContext context)
        {
            var count = 0;

            foreach (var rowGroups in records)
            {
                var fields = rowGroups.Fields.Values;
                var path = GetTempPath();

                // todo split to multiple files if needed
                using (Stream fileStream = File.OpenWrite(path))
                {
                    using (var parquetWriter = new ParquetWriter(new Schema(fields), fileStream))
                    {
                        foreach (var rowGroup in rowGroups.Groups)
                        {
                            // create a new row group in the file
                            using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                            {
                                foreach (var kvp in rowGroup.Data)
                                {
                                    var data = kvp.Value;
                                    var field = rowGroups.Fields[kvp.Key];
                                    Array array = default;

                                    // todo handle more types
                                    switch (field.DataType)
                                    {
                                        case DataType.Unspecified:
                                            // todo
                                            break;
                                        case DataType.Boolean:
                                            array = ((List<bool>)data).ToArray();
                                            break;
                                        case DataType.Int32:
                                        case DataType.Int64:
                                            array = ((List<long>)data).ToArray();
                                            break;
                                        case DataType.String:
                                            array = ((List<string>)data).ToArray();
                                            break;
                                        case DataType.Float:
                                        case DataType.Double:
                                        case DataType.Decimal:
                                            array = ((List<double>)data).ToArray();
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }

                                    groupWriter.WriteColumn(new DataColumn(field, array));

                                    count += data.Count;

                                    // todo log stats
                                }
                            }
                        }
                    }
                }

                // todo upload parquet file to s3/bucketName/TableName/PartitionKey/ and delete file from disc 
            }

            return count;
        }

        private string GetTempPath()
        {
            var dir = Configuration.TempDirectoryPath ?? Path.GetTempPath();
            var fileName = $"{Database.Name}_{Guid.NewGuid()}.parquet"; 

            return Path.Combine(dir, fileName);
        }
    }
}