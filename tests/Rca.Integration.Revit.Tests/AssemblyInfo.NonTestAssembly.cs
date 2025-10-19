using NUnit.Framework;

// Mark this assembly so that the NUnit3TestAdapter skips discovery for it.
// RCA integration tests are discovered and executed by the custom Rca.TestAdapter instead.
[assembly: NonTestAssembly]
