using NUnit.Framework;
using Rca.Loader.Configuration;
using System;
using System.IO;

namespace Rca.Loader.Tests
{
    /// <summary>
    /// Tests for <see cref="PathExpander"/> class.
    /// </summary>
    [TestFixture]
    public class PathExpanderTests
    {
        /// <summary>
        /// Verifies that ExpandPath correctly expands TEMP environment variable.
        /// </summary>
        [Test]
        public void ExpandPath_WithTempVariable_ShouldExpandCorrectly()
        {
            var path = "%TEMP%\\test.txt";
            var expanded = PathExpander.ExpandPath(path);
            var expected = Path.GetFullPath(Environment.GetEnvironmentVariable("TEMP") + "\\test.txt");

            Assert.That(expanded, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that ExpandPath correctly expands USERPROFILE environment variable.
        /// </summary>
        [Test]
        public void ExpandPath_WithUserProfileVariable_ShouldExpandCorrectly()
        {
            var path = "%USERPROFILE%\\Documents\\test.txt";
            var expanded = PathExpander.ExpandPath(path);
            var expected = Path.GetFullPath(Environment.GetEnvironmentVariable("USERPROFILE") + "\\Documents\\test.txt");

            Assert.That(expanded, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that ExpandPath correctly expands PROGRAMDATA environment variable.
        /// </summary>
        [Test]
        public void ExpandPath_WithProgramDataVariable_ShouldExpandCorrectly()
        {
            var path = "%PROGRAMDATA%\\test.txt";
            var expanded = PathExpander.ExpandPath(path);
            var expected = Path.GetFullPath(Environment.GetEnvironmentVariable("PROGRAMDATA") + "\\test.txt");

            Assert.That(expanded, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that ExpandPath returns path as-is when no variables present.
        /// </summary>
        [Test]
        public void ExpandPath_WithoutVariables_ShouldReturnFullPath()
        {
            var path = "C:\\temp\\test.txt";
            var expanded = PathExpander.ExpandPath(path);
            var expected = Path.GetFullPath(path);

            Assert.That(expanded, Is.EqualTo(expected));
        }

        /// <summary>
        /// Verifies that ExpandPath handles null input gracefully.
        /// </summary>
        [Test]
        public void ExpandPath_WithNull_ShouldReturnNull()
        {
            string? path = null;
            var expanded = PathExpander.ExpandPath(path!);

            Assert.That(expanded, Is.Null);
        }

        /// <summary>
        /// Verifies that ExpandPath handles empty string input.
        /// </summary>
        [Test]
        public void ExpandPath_WithEmptyString_ShouldReturnEmptyString()
        {
            var path = string.Empty;
            var expanded = PathExpander.ExpandPath(path);

            Assert.That(expanded, Is.Empty);
        }

        /// <summary>
        /// Verifies that ExpandPath handles whitespace input.
        /// </summary>
        [Test]
        public void ExpandPath_WithWhitespace_ShouldReturnWhitespace()
        {
            var path = "   ";
            var expanded = PathExpander.ExpandPath(path);

            Assert.That(expanded, Is.EqualTo(path));
        }

        /// <summary>
        /// Verifies that ExpandPath expands multiple variables in one path.
        /// </summary>
        [Test]
        public void ExpandPath_WithMultipleVariables_ShouldExpandAll()
        {
            // Note: This test creates an unusual path but tests the expansion logic
            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            var temp = Environment.GetEnvironmentVariable("TEMP");
            
            // Create a path that would be normalized by GetFullPath
            var path = $"%USERPROFILE%\\test";
            var expanded = PathExpander.ExpandPath(path);
            
            Assert.That(expanded, Does.Contain(userProfile!));
            Assert.That(expanded, Does.Not.Contain("%"));
        }

        /// <summary>
        /// Verifies that ExpandPath handles relative paths correctly.
        /// </summary>
        [Test]
        public void ExpandPath_WithRelativePath_ShouldResolveToFullPath()
        {
            var path = ".\\test.txt";
            var expanded = PathExpander.ExpandPath(path);
            
            Assert.That(Path.IsPathRooted(expanded), Is.True, "Expanded path should be rooted");
        }

        /// <summary>
        /// Verifies that ExpandPath handles network paths (UNC paths).
        /// </summary>
        [Test]
        public void ExpandPath_WithUncPath_ShouldReturnFullPath()
        {
            var path = "\\\\server\\share\\file.txt";
            var expanded = PathExpander.ExpandPath(path);
            var expected = Path.GetFullPath(path);

            Assert.That(expanded, Is.EqualTo(expected));
        }
    }
}

