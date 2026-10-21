using System.Collections.Generic;
using MicMax.Core.Settings;

namespace MicMax.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IRegistryAccessor"/> so Registry round-trip behavior
/// can be tested without touching the real HKCU hive.
/// </summary>
public sealed class FakeRegistryAccessor : IRegistryAccessor
{
    public Dictionary<string, string> Values { get; } = new();

    public string? Read(string name) =>
        Values.TryGetValue(name, out var value) ? value : null;

    public void Write(string name, string value) => Values[name] = value;

    public void Delete(string name) => Values.Remove(name);
}
