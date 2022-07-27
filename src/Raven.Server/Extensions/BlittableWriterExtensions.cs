using System;
using Raven.Server.ServerWide;
using Sparrow.Json;

namespace Raven.Server.Extensions;

public static class BlittableWriterExtensions
{
    public static void AddPropertiesForDebug(this AsyncBlittableJsonTextWriter writer, ServerStore serverStore, bool skipLastComma = false)
    {
        writer.WritePropertyName(nameof(DateTime));
        writer.WriteDateTime(DateTime.UtcNow, true);
        writer.WriteComma();
        writer.WritePropertyName(nameof(serverStore.Server.WebUrl));
        writer.WriteString(serverStore.Server.WebUrl);
        writer.WriteComma();
        writer.WritePropertyName(nameof(serverStore.NodeTag));
        writer.WriteString(serverStore.NodeTag);

        if (skipLastComma == false)
            writer.WriteComma();
    }
}
