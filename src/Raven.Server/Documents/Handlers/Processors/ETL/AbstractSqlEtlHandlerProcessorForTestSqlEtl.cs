﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Sparrow.Json;

namespace Raven.Server.Documents.Handlers.Processors.ETL
{
    internal abstract class AbstractSqlEtlHandlerProcessorForTestSqlEtl<TRequestHandler, TOperationContext> : AbstractDatabaseHandlerProcessor<TRequestHandler, TOperationContext>
        where TRequestHandler : AbstractDatabaseRequestHandler<TOperationContext>
        where TOperationContext : JsonOperationContext
    {
        public AbstractSqlEtlHandlerProcessorForTestSqlEtl([NotNull] TRequestHandler requestHandler) : base(requestHandler)
        {
        }

        protected abstract ValueTask GetAndWriteSqlEtlScriptTestResultAsync(TOperationContext context, BlittableJsonReaderObject sqlScript);

        public override async ValueTask ExecuteAsync()
        {
            using (ContextPool.AllocateOperationContext(out TOperationContext context))
            {
                var dbDoc = await context.ReadForMemoryAsync(RequestHandler.RequestBodyStream(), "TestSqlEtlScript");
                await GetAndWriteSqlEtlScriptTestResultAsync(context, dbDoc);
            }
        }
    }
}
