using NUnit.Framework;
using Rca.Loader.AssemblyManagement;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for assembly management data classes: <see cref="AssemblyInfo"/>, 
    /// <see cref="LoadedAssembliesInfo"/>, and <see cref="SignalInfo"/>.
    /// </summary>
    [TestFixture]
    public class AssemblyManagementTests
    {
        /// <summary>
        /// Verifies that AssemblyInfo can be instantiated with default values.
        /// </summary>
        [Test]
        public void AssemblyInfo_DefaultConstruction_ShouldHaveEmptyValues()
        {
            var info = new AssemblyInfo();

            Assert.That(info.Path, Is.Empty);
            Assert.That(info.Hash, Is.Empty);
        }

        /// <summary>
        /// Verifies that AssemblyInfo properties can be set and retrieved.
        /// </summary>
        [Test]
        public void AssemblyInfo_Properties_CanBeSetAndRetrieved()
        {
            var info = new AssemblyInfo
            {
                Path = @"C:\Test\Assembly.dll",
                Hash = "abc123def456"
            };

            Assert.That(info.Path, Is.EqualTo(@"C:\Test\Assembly.dll"));
            Assert.That(info.Hash, Is.EqualTo("abc123def456"));
        }

        /// <summary>
        /// Verifies that LoadedAssembliesInfo can be instantiated with default values.
        /// </summary>
        [Test]
        public void LoadedAssembliesInfo_DefaultConstruction_ShouldInitializeAllProperties()
        {
            var info = new LoadedAssembliesInfo();

            Assert.That(info.LoaderComponents, Is.Not.Null);
            Assert.That(info.RuntimeAssembly, Is.Not.Null);
            Assert.That(info.LoadedRuntimeAssembly, Is.Not.Null);
            Assert.That(info.LastMSBuildSignal, Is.Not.Null);
        }

        /// <summary>
        /// Verifies that LoadedAssembliesInfo initializes AssemblyInfo objects with empty values.
        /// </summary>
        [Test]
        public void LoadedAssembliesInfo_DefaultConstruction_ShouldHaveEmptyAssemblyInfo()
        {
            var info = new LoadedAssembliesInfo();

            Assert.That(info.LoaderComponents.Path, Is.Empty);
            Assert.That(info.LoaderComponents.Hash, Is.Empty);
            Assert.That(info.RuntimeAssembly.Path, Is.Empty);
            Assert.That(info.RuntimeAssembly.Hash, Is.Empty);
            Assert.That(info.LoadedRuntimeAssembly.Path, Is.Empty);
            Assert.That(info.LoadedRuntimeAssembly.Hash, Is.Empty);
        }

        /// <summary>
        /// Verifies that LoadedAssembliesInfo properties can be set and retrieved.
        /// </summary>
        [Test]
        public void LoadedAssembliesInfo_Properties_CanBeSetAndRetrieved()
        {
            var info = new LoadedAssembliesInfo
            {
                LoaderComponents = new AssemblyInfo 
                { 
                    Path = @"C:\Loader\", 
                    Hash = "loader123" 
                },
                RuntimeAssembly = new AssemblyInfo 
                { 
                    Path = @"C:\Runtime\Rca.Runtime.dll", 
                    Hash = "runtime456" 
                },
                LoadedRuntimeAssembly = new AssemblyInfo 
                { 
                    Path = @"C:\Runtime\Rca.Runtime.dll", 
                    Hash = "runtime456" 
                },
                LastMSBuildSignal = new SignalInfo 
                { 
                    Time = "12:34:56", 
                    Event = "runtime outdated" 
                }
            };

            Assert.That(info.LoaderComponents.Path, Is.EqualTo(@"C:\Loader\"));
            Assert.That(info.LoaderComponents.Hash, Is.EqualTo("loader123"));
            Assert.That(info.RuntimeAssembly.Path, Is.EqualTo(@"C:\Runtime\Rca.Runtime.dll"));
            Assert.That(info.RuntimeAssembly.Hash, Is.EqualTo("runtime456"));
            Assert.That(info.LoadedRuntimeAssembly.Path, Is.EqualTo(@"C:\Runtime\Rca.Runtime.dll"));
            Assert.That(info.LoadedRuntimeAssembly.Hash, Is.EqualTo("runtime456"));
            Assert.That(info.LastMSBuildSignal.Time, Is.EqualTo("12:34:56"));
            Assert.That(info.LastMSBuildSignal.Event, Is.EqualTo("runtime outdated"));
        }

        /// <summary>
        /// Verifies that SignalInfo can be instantiated with default values.
        /// </summary>
        [Test]
        public void SignalInfo_DefaultConstruction_ShouldHaveDefaultValues()
        {
            var signal = new SignalInfo();

            Assert.That(signal.Time, Is.Empty);
            Assert.That(signal.Event, Is.EqualTo("no changes"));
        }

        /// <summary>
        /// Verifies that SignalInfo properties can be set and retrieved.
        /// </summary>
        [Test]
        public void SignalInfo_Properties_CanBeSetAndRetrieved()
        {
            var signal = new SignalInfo
            {
                Time = "14:25:30",
                Event = "both loader and runtime outdated"
            };

            Assert.That(signal.Time, Is.EqualTo("14:25:30"));
            Assert.That(signal.Event, Is.EqualTo("both loader and runtime outdated"));
        }

        /// <summary>
        /// Verifies that SignalInfo supports all documented event types.
        /// </summary>
        [Test]
        [TestCase("no changes")]
        [TestCase("only runtime outdated")]
        [TestCase("only loader outdated")]
        [TestCase("both loader and runtime outdated")]
        public void SignalInfo_Event_SupportsDocumentedEventTypes(string eventType)
        {
            var signal = new SignalInfo { Event = eventType };

            Assert.That(signal.Event, Is.EqualTo(eventType));
        }

        /// <summary>
        /// Verifies that multiple AssemblyInfo instances are independent.
        /// </summary>
        [Test]
        public void AssemblyInfo_MultipleInstances_ShouldBeIndependent()
        {
            var info1 = new AssemblyInfo { Path = "Path1", Hash = "Hash1" };
            var info2 = new AssemblyInfo { Path = "Path2", Hash = "Hash2" };

            Assert.That(info1.Path, Is.Not.EqualTo(info2.Path));
            Assert.That(info1.Hash, Is.Not.EqualTo(info2.Hash));

            info1.Path = "Modified";
            Assert.That(info2.Path, Is.EqualTo("Path2"), "Modifying info1 should not affect info2");
        }

        /// <summary>
        /// Verifies that LoadedAssembliesInfo tracks discovered vs loaded runtime correctly.
        /// </summary>
        [Test]
        public void LoadedAssembliesInfo_ShouldDistinguishDiscoveredAndLoadedRuntime()
        {
            var info = new LoadedAssembliesInfo
            {
                RuntimeAssembly = new AssemblyInfo 
                { 
                    Path = @"C:\New\Rca.Runtime.dll", 
                    Hash = "newhash" 
                },
                LoadedRuntimeAssembly = new AssemblyInfo 
                { 
                    Path = @"C:\Old\Rca.Runtime.dll", 
                    Hash = "oldhash" 
                }
            };

            Assert.That(info.RuntimeAssembly.Hash, Is.Not.EqualTo(info.LoadedRuntimeAssembly.Hash),
                "Discovered and loaded runtime should be distinguishable");
        }
    }
}

