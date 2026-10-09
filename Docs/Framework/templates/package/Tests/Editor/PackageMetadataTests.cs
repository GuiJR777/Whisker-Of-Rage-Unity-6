// Purpose: Verifies that the package identity constants match the package manifest.
using System.Text.RegularExpressions;

using NUnit.Framework;

namespace {{ASSEMBLY_ROOT}}.Tests.Editor
{
    internal sealed class PackageMetadataTests
    {
        private const string SEMANTIC_VERSION_PATTERN = @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$";

        [Test]
        public void PackageName_MatchesManifest()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo = FindPackageInfo();

            Assert.That(packageInfo, Is.Not.Null, "The runtime assembly is not part of a package.");
            Assert.That(packageInfo.name, Is.EqualTo({{SYSTEM_NAME}}PackageInfo.PACKAGE_NAME));
        }

        [Test]
        public void Version_FollowsSemanticVersioning()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo = FindPackageInfo();

            Assert.That(packageInfo, Is.Not.Null, "The runtime assembly is not part of a package.");
            Assert.That(Regex.IsMatch(packageInfo.version, SEMANTIC_VERSION_PATTERN), Is.True, packageInfo.version);
        }

        private static UnityEditor.PackageManager.PackageInfo FindPackageInfo()
        {
            return UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof({{SYSTEM_NAME}}PackageInfo).Assembly);
        }
    }
}
