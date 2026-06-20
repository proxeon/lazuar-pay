// apps/lazuar-api/Modules/Ops/Infrastructure/Services/ToolCallAccumulator.cs
using System;
using System.IO;

namespace Modules.Ops.Infrastructure.Services;

internal class ToolCallAccumulator : IDisposable
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MemoryStream ArgumentsStream { get; } = new MemoryStream();
    
    public void Dispose() => ArgumentsStream.Dispose();
}
