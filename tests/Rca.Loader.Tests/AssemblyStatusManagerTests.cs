using NUnit.Framework;
using Rca.Loader.AssemblyManagement;
using Rca.Loader.Infrastructure;
using System;
using System.IO;

namespace Rca.Loader.Tests
{
    [TestFixture]
    public class AssemblyStatusManagerTests
    {
        private AssemblyStatusManager? _statusManager;
        private string? _testDeployFolder;
        
        [SetUp]
        public void Setup()
        {
            _statusManager = new AssemblyStatusManager();
            
            // Create a test deploy folder
            _testDeployFolder = Path.Combine(Path.GetTempPath(), "RCA_Test", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(_testDeployFolder);
        }
        
        [TearDown]
        public void TearDown()
        {
            if (_testDeployFolder != null && Directory.Exists(_testDeployFolder))
            {
                try
                {
                    Directory.Delete(_testDeployFolder, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        
        /// <summary>
        /// This test demonstrates the bug: after ProcessMsBuildSignal updates CurrentInfo,
        /// IsRuntimeOutdated() returns false because it compares the NEW hash with itself.
        /// 
        /// Expected behavior:
        /// - Before ProcessMsBuildSignal: IsRuntimeOutdated() should detect the new version
        /// - After ProcessMsBuildSignal: Hash should be updated, but we should REMEMBER it's outdated
        /// - UI should show "OUTDATED" status until actual reload happens
        /// </summary>
        [Test]
        public void ProcessMsBuildSignal_ShouldDetectRuntimeOutdated_EvenAfterHashUpdate()
        {
            // Arrange: Simulate initial state with old hash
            _statusManager!.CurrentInfo.RuntimeAssembly.Hash = "old_hash_12345678";
            _statusManager.CurrentInfo.RuntimeAssembly.Path = Path.Combine(_testDeployFolder!, "Rca.Runtime.dll");
            
            // Create a new DLL with different hash in deploy folder
            var newRuntimePath = Path.Combine(_testDeployFolder!, LoaderConstants.RuntimeFileName);
            CreateMockDllWithHash(newRuntimePath, "new_hash_87654321");
            
            // Act: Process MSBuild signal (this updates CurrentInfo.RuntimeAssembly.Hash)
            _statusManager.ProcessMsBuildSignal(_testDeployFolder!);
            
            // Assert: After ProcessMsBuildSignal, hash should be updated
            Assert.That(_statusManager.CurrentInfo.RuntimeAssembly.Hash, Is.EqualTo("new_hash_87654321"),
                "Hash should be updated in CurrentInfo");
            
            // BUG: IsRuntimeOutdated() now compares new_hash with new_hash → returns FALSE
            // Expected: Should return TRUE because we haven't actually LOADED the new runtime yet
            var isOutdated = _statusManager.IsRuntimeOutdated();
            
            Assert.That(isOutdated, Is.True,
                "Runtime should be marked as OUTDATED even after hash update, until actual reload happens");
        }
        
        /// <summary>
        /// Test the full cycle: ProcessMsBuildSignal → Shows outdated → Reload → No longer outdated
        /// </summary>
        [Test]
        public void FullReloadCycle_ShouldWorkCorrectly()
        {
            // Arrange: Initial state
            _statusManager!.CurrentInfo.RuntimeAssembly.Hash = "old_hash";
            
            var newRuntimePath = Path.Combine(_testDeployFolder!, LoaderConstants.RuntimeFileName);
            CreateMockDllWithHash(newRuntimePath, "new_hash");
            
            // Act 1: MSBuild signal arrives
            _statusManager.ProcessMsBuildSignal(_testDeployFolder!);
            
            // Assert 1: Should detect as outdated
            Assert.That(_statusManager.IsRuntimeOutdated(), Is.True,
                "After MSBuild signal, runtime should be detected as outdated");
            
            // Act 2: Simulate actual reload (UpdateHashesAfterReload)
            _statusManager.UpdateHashesAfterReload(newRuntimePath);
            
            // Assert 2: After reload, should no longer be outdated
            Assert.That(_statusManager.IsRuntimeOutdated(), Is.False,
                "After reload, runtime should no longer be outdated");
        }
        
        private void CreateMockDllWithHash(string path, string hash)
        {
            // Create a minimal mock DLL file
            // In real scenario, AttributeInjector would embed metadata
            // For this test, we just need the file to exist
            File.WriteAllText(path, $"Mock DLL with hash: {hash}");
            
            // TODO: If needed, create actual DLL with embedded metadata using Mono.Cecil
            // For now, we'll need to modify AttributeMetadataLoader to support test mode
        }
    }
}
