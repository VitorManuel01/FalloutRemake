using System;
using System.Collections.Generic;
using Godot;

namespace FalloutRemake.Preview;

// Release managed references on the main thread. Nodes keep their native resource references.
internal sealed class ResourceScope : IDisposable
{
    private readonly List<RefCounted> _resources = new();
    public T Own<T>(T resource) where T : RefCounted { _resources.Add(resource); return resource; }
    public void Dispose()
    {
        for (int i = _resources.Count - 1; i >= 0; i--) _resources[i].Dispose();
        _resources.Clear();
    }
}
