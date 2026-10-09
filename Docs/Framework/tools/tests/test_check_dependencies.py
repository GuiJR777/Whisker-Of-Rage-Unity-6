"""Purpose: Regression tests for check_dependencies.py, focused on presence rules and false positives.

Run from the host root:
    python -m unittest discover -s Docs/Framework/tools/tests -v
"""
import contextlib
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import check_dependencies  # noqa: E402  (path set above)

CORE = "com.ramirestechgames.core"
STATS = "com.ramirestechgames.stats"
CHARACTER = "com.ramirestechgames.character"


def build_graph(core_status="implemented", stats_status="planned", character_status="planned"):
    return {
        "schemaVersion": 1,
        "packages": {
            CORE: {"layer": 0, "status": core_status, "assemblyRoot": "RamiresTechGames.Core",
                   "define": "RAMIRESTECHGAMES_CORE", "hard": [], "optional": []},
            STATS: {"layer": 1, "status": stats_status, "assemblyRoot": "RamiresTechGames.Stats",
                    "define": "RAMIRESTECHGAMES_STATS", "hard": [CORE], "optional": []},
            CHARACTER: {"layer": 2, "status": character_status, "assemblyRoot": "RamiresTechGames.Character",
                        "define": "RAMIRESTECHGAMES_CHARACTER", "hard": [CORE], "optional": [STATS]},
        },
        "externalPackages": {},
    }


class CheckDependenciesTests(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory()
        self.root = Path(self._temp.name)
        self.host = self.root / "Host"
        self.workspace = self.host / "Packages"
        self.workspace.mkdir(parents=True)
        self.graph_path = self.root / "graph.json"
        self.write_graph(build_graph())

    def tearDown(self):
        self._temp.cleanup()

    # --- helpers -------------------------------------------------------------------------------------------

    def write_graph(self, graph):
        self.graph_path.write_text(json.dumps(graph), encoding="utf-8")

    def write_json(self, path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding="utf-8")

    def add_package(self, package_id, assembly_root, dependencies=None, references=None):
        package_dir = self.workspace / package_id
        self.write_json(package_dir / "package.json", {
            "name": package_id, "version": "0.1.0", "unity": "6000.6",
            "dependencies": {name: "0.1.0" for name in (dependencies or [])}})
        self.write_json(package_dir / "Runtime" / (assembly_root + ".asmdef"),
                        {"name": assembly_root, "references": references or []})
        return package_dir

    def add_core(self):
        return self.add_package(CORE, "RamiresTechGames.Core")

    def write_host_manifest(self, testables=None, dependencies=None):
        self.write_json(self.workspace / "manifest.json",
                        {"dependencies": dependencies or {}, "testables": testables or []})

    def run_checker(self, *extra_arguments, use_host=True):
        arguments = ["--graph", str(self.graph_path), "--workspace", str(self.workspace)]
        arguments += ["--host-root", str(self.host)] if use_host else ["--no-host"]
        arguments += list(extra_arguments)
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            exit_code = check_dependencies.run(arguments)
        return exit_code, output.getvalue()

    # --- presence: false positives ---------------------------------------------------------------------------

    def test_EmptyWorkspace_AllPlanned_FailsBecauseNothingWasVerified(self):
        self.write_graph(build_graph(core_status="planned"))

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn("no package was verified", output)

    def test_ImplementedPackageMissing_Fails(self):
        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn(CORE + ": expected (marked implemented", output)

    def test_RequestedPackageMissing_Fails(self):
        self.add_core()

        exit_code, output = self.run_checker("--package", "stats")

        self.assertEqual(exit_code, 1)
        self.assertIn(STATS + ": expected (requested with --package)", output)

    def test_HostTestablesDeclarePackageThatIsMissing_Fails(self):
        self.add_core()
        self.write_host_manifest(testables=[CORE, STATS])

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn(STATS + ": expected (declared by the host in Packages/manifest.json testables)", output)

    def test_GitmodulesDeclarePackageThatIsMissing_Fails(self):
        self.add_core()
        (self.host / ".gitmodules").write_text(
            '[submodule "Packages/{0}"]\n\tpath = Packages/{0}\n\turl = https://example.invalid/{0}.git\n'
            .format(STATS), encoding="utf-8")

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn(STATS + ": expected (declared by the host in .gitmodules)", output)

    def test_UninitializedSubmoduleFolder_Fails(self):
        (self.workspace / CORE).mkdir()

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn("uninitialized submodule", output)

    def test_UnknownFrameworkPackageFolder_Fails(self):
        self.add_core()
        (self.workspace / "com.ramirestechgames.unknown").mkdir()

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn("com.ramirestechgames.unknown: found in the workspace but not declared", output)

    def test_PackagePresentButMarkedPlanned_Fails(self):
        self.add_core()
        self.add_package(STATS, "RamiresTechGames.Stats", dependencies=[CORE], references=["RamiresTechGames.Core"])

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn(STATS + ": found in the workspace but marked 'planned'", output)

    def test_InvalidStatusInGraph_Fails(self):
        self.write_graph(build_graph(stats_status="done"))
        self.add_core()

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 1)
        self.assertIn("has status 'done'", output)

    # --- presence: valid scenarios ---------------------------------------------------------------------------

    def test_PlannedPackagesAbsent_ImplementedPresent_Passes(self):
        self.add_core()
        self.write_host_manifest(testables=[CORE])

        exit_code, output = self.run_checker()

        self.assertEqual(exit_code, 0, output)
        self.assertIn("Packages verified: " + CORE, output)

    def test_RequestedPackage_OnlyRequestedIsVerified(self):
        self.write_graph(build_graph(stats_status="implemented"))
        self.add_core()
        self.add_package(STATS, "RamiresTechGames.Stats", dependencies=[CORE], references=["RamiresTechGames.Core"])

        exit_code, output = self.run_checker("--package", "core")

        self.assertEqual(exit_code, 0, output)
        self.assertIn("Packages verified: " + CORE + "\n", output)

    # --- manifest and asmdef rules ---------------------------------------------------------------------------

    def test_ReferenceToGameAssembly_Fails(self):
        self.add_package(CORE, "RamiresTechGames.Core", references=["WhiskersOfRage.Gameplay"])

        exit_code, output = self.run_checker(use_host=False)

        self.assertEqual(exit_code, 1)
        self.assertIn("packages must not depend on game code", output)

    def test_ReferenceToUnityAssembly_Passes(self):
        self.add_package(CORE, "RamiresTechGames.Core", references=["Unity.Mathematics", "UnityEngine.TestRunner"])

        exit_code, output = self.run_checker(use_host=False)

        self.assertEqual(exit_code, 0, output)

    def test_MissingHardDependencyInPackageJson_Fails(self):
        self.write_graph(build_graph(stats_status="implemented"))
        self.add_core()
        self.add_package(STATS, "RamiresTechGames.Stats", references=["RamiresTechGames.Core"])

        exit_code, output = self.run_checker(use_host=False)

        self.assertEqual(exit_code, 1)
        self.assertIn("missing hard dependency " + CORE, output)

    def test_OptionalDependencyOutsideIntegrationFolder_Fails(self):
        self.write_graph(build_graph(character_status="implemented"))
        self.add_core()
        self.add_package(CHARACTER, "RamiresTechGames.Character", dependencies=[CORE],
                         references=["RamiresTechGames.Core", "RamiresTechGames.Stats"])

        exit_code, output = self.run_checker("--package", "character", use_host=False)

        self.assertEqual(exit_code, 1)
        self.assertIn("may only be referenced from Runtime/Integration", output)

    def test_GuardedIntegrationAssembly_Passes(self):
        self.write_graph(build_graph(character_status="implemented"))
        self.add_core()
        package_dir = self.add_package(CHARACTER, "RamiresTechGames.Character", dependencies=[CORE],
                                       references=["RamiresTechGames.Core"])
        self.write_json(package_dir / "Runtime" / "Integration" / "Stats" / "Integration.asmdef", {
            "name": "RamiresTechGames.Character.Integration.Stats",
            "references": ["RamiresTechGames.Character", "RamiresTechGames.Stats"],
            "defineConstraints": ["RAMIRESTECHGAMES_STATS"],
            "versionDefines": [{"name": STATS, "expression": "0.1.0", "define": "RAMIRESTECHGAMES_STATS"}]})

        exit_code, output = self.run_checker(use_host=False)

        self.assertEqual(exit_code, 0, output)

    def test_GuidReference_Fails(self):
        self.add_package(CORE, "RamiresTechGames.Core", references=["GUID:0123456789abcdef"])

        exit_code, output = self.run_checker(use_host=False)

        self.assertEqual(exit_code, 1)
        self.assertIn("uses a GUID", output)


if __name__ == "__main__":
    unittest.main()
