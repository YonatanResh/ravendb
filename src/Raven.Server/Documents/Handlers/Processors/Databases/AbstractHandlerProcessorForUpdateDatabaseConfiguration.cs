﻿using System;
using System.Net;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Security;
using Raven.Client.Util;
using Raven.Server.ServerWide.Context;
using Raven.Server.Web;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Server.Documents.Handlers.Processors.Databases;

internal abstract class AbstractHandlerProcessorForUpdateDatabaseConfiguration<T, TRequestHandler> : AbstractHandlerProcessor<TRequestHandler, TransactionOperationContext>
    where T : class
    where TRequestHandler : RequestHandler
{
    private readonly bool _isBlittable;

    protected AbstractHandlerProcessorForUpdateDatabaseConfiguration([NotNull] TRequestHandler requestHandler)
        : base(requestHandler, requestHandler.ServerStore.ContextPool)
    {
        _isBlittable = typeof(T) == typeof(BlittableJsonReaderObject);
    }

    protected abstract string GetDatabaseName();

    protected virtual HttpStatusCode GetResponseStatusCode() => HttpStatusCode.OK;

    protected virtual T GetConfiguration(BlittableJsonReaderObject configuration)
    {
        if (_isBlittable)
            return configuration as T;

        throw new InvalidOperationException($"In order to convert to '{typeof(T).Name}' please override this method.");
    }

    protected virtual void OnBeforeUpdateConfiguration(ref T configuration, JsonOperationContext context)
    {
    }

    protected abstract Task<(long Index, object Result)> OnUpdateConfiguration(TransactionOperationContext context, string databaseName, T configuration, string raftRequestId);

    protected virtual void OnBeforeResponseWrite(DynamicJsonValue responseJson, T configuration, long index)
    {
    }

    protected abstract ValueTask WaitForIndexNotificationAsync(long index);

    public override async ValueTask ExecuteAsync()
    {
        using (RequestHandler.ServerStore.ContextPool.AllocateOperationContext(out TransactionOperationContext context))
        {
            var databaseName = GetDatabaseName();
            var canAccessDatabase = await RequestHandler.CanAccessDatabaseAsync(databaseName, requireAdmin: true, requireWrite: true);
            if (canAccessDatabase == false)
                throw new AuthorizationException($"Cannot modify configuration of '{databaseName}' database due to insufficient privileges.");

            var configurationJson = await context.ReadForMemoryAsync(RequestHandler.RequestBodyStream(), GetType().Name);
            var configuration = GetConfiguration(configurationJson);

            if (ResourceNameValidator.IsValidResourceName(databaseName, RequestHandler.ServerStore.Configuration.Core.DataDirectory.FullPath, out string errorMessage) == false)
                throw new BadRequestException(errorMessage);

            await RequestHandler.ServerStore.EnsureNotPassiveAsync();

            OnBeforeUpdateConfiguration(ref configuration, context);

            var (index, _) = await OnUpdateConfiguration(context, databaseName, configuration, RequestHandler.GetRaftRequestIdFromQuery());

            await WaitForIndexNotificationAsync(index);

            RequestHandler.HttpContext.Response.StatusCode = (int)GetResponseStatusCode();

            await using (var writer = new AsyncBlittableJsonTextWriter(context, RequestHandler.ResponseBodyStream()))
            {
                var json = new DynamicJsonValue
                {
                    ["RaftCommandIndex"] = index
                };

                OnBeforeResponseWrite(json, configuration, index);

                context.Write(writer, json);
            }
        }
    }
}