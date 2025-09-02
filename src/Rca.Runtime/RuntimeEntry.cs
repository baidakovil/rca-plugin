using System;
using Rca.Loader.Contracts; // restore shared interface reference

namespace Rca.Runtime;

public class RuntimeEntry : IRuntime
{
    public void Initialize()
    {
        // Initialize runtime services here (placeholder)
        Console.WriteLine("RCA Runtime initialized (shared IRuntime)");
    }

    public void Shutdown()
    {
        Console.WriteLine("RCA Runtime shutdown");
    }
}
