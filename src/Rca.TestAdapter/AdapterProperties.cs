using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Rca.TestAdapter;

/// <summary>
/// Shared test adapter properties used to pass metadata between discovery and execution.
/// </summary>
internal static class AdapterProperties
{
    /// <summary>
    /// Contains the absolute path to the test assembly located in the Runtime deploy folder
    /// (e.g., %LOCALAPPDATA%\RCA\Runtime\<timestamp>\Rca.Integration.Revit.Tests.dll).
    /// When present, the executor uses this path instead of the TestCase.Source.
    /// </summary>
    public static readonly TestProperty RuntimeAssemblyPath =
        TestProperty.Register(
            "Rca.RuntimeAssemblyPath",
            "RCA Runtime Assembly Path",
            "RCA",
            "Absolute path to the runtime-deployed integration test assembly",
            typeof(string),
            validateValueCallback: null,
            attributes: TestPropertyAttributes.None,
            owner: typeof(AdapterProperties));
}
