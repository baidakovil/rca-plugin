using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Rca.TestAdapter;

internal interface ITestCasePublisher
{
  void PublishDiscoveredTests(
      IEnumerable<TestCase> testCases,
      string hostSource,
      string runtimeAssemblyPath,
      ITestCaseDiscoverySink discoverySink);
}

internal sealed class RcaTestCasePublisher : ITestCasePublisher
{
  public void PublishDiscoveredTests(
      IEnumerable<TestCase> testCases,
      string hostSource,
      string runtimeAssemblyPath,
      ITestCaseDiscoverySink discoverySink)
  {
    if (testCases is null)
      throw new ArgumentNullException(nameof(testCases));
    if (string.IsNullOrEmpty(hostSource))
      throw new ArgumentException("Host source must be provided.", nameof(hostSource));
    if (string.IsNullOrEmpty(runtimeAssemblyPath))
      throw new ArgumentException("Runtime assembly path must be provided.", nameof(runtimeAssemblyPath));
    if (discoverySink is null)
      throw new ArgumentNullException(nameof(discoverySink));

    foreach (var testCase in testCases)
    {
      testCase.Source = hostSource;
      testCase.SetPropertyValue(AdapterProperties.RuntimeAssemblyPath, runtimeAssemblyPath);
      testCase.Traits.Add(new Trait("Adapter", "RCA"));
      discoverySink.SendTestCase(testCase);
    }
  }
}


