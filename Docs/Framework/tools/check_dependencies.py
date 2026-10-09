#!/usr/bin/env python3
"""Purpose: Validate the framework dependency graph and every package manifest and asmdef against it.

Checks:
  * The graph itself: known packages only, strictly lower layers, hard and optional do not overlap,
    valid status ("planned" or "implemented").
  * Presence: every expected package must be found and verified. A package is expected when it is
    marked "implemented" in the graph, declared by the host (Packages/manifest.json dependencies or
    testables, .gitmodules) or requested with --package. "planned" packages may be absent.
    The run fails when no package could be verified at all.
  * package.json: name, SemVer version, unity field, RamiresTech dependencies == hard dependencies.
  * asmdef: references by name (no GUID), only allowed packages, optional packages only inside
    Integration assemblies guarded by defineConstraints + versionDefines, and no reference to any
    assembly outside the framework or Unity (protects packages from depending on game code).

Usage:
    python Docs/Framework/tools/check_dependencies.py [--package core] [--workspace <dir>] [--host-root <dir>]
Exit code 0 when every expected package was verified without errors, 1 otherwise.
"""
import argparse
import configparser
import json
import re
import sys
from pathlib import Path

FRAMEWORK_ROOT = Path(__file__).resolve().parent.parent
HOST_ROOT = FRAMEWORK_ROOT.parent.parent
GRAPH_PATH = FRAMEWORK_ROOT / "dependency-graph.json"
PACKAGES_FOLDER = "Packages"
UNITY_ASSEMBLY_PREFIXES = ("Unity.", "UnityEngine", "UnityEditor")
PACKAGE_PREFIX = "com.ramirestechgames."
GUID_REFERENCE_PREFIX = "GUID:"
INTEGRATION_FOLDER = "Integration"
SAMPLES_FOLDER = "Samples~"
STATUS_IMPLEMENTED = "implemented"
STATUS_PLANNED = "planned"
VALID_STATUSES = (STATUS_IMPLEMENTED, STATUS_PLANNED)
SEMANTIC_VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")


def load_json(path):
    with path.open(encoding="utf-8-sig") as json_file:
        return json.load(json_file)


def normalize_package_id(name):
    return name if name.startswith(PACKAGE_PREFIX) else PACKAGE_PREFIX + name


def read_host_declarations(host_root):
    """Returns {package_id: reason} for framework packages the host project declares."""
    declared = {}
    if host_root is None:
        return declared

    manifest_path = host_root / PACKAGES_FOLDER / "manifest.json"
    if manifest_path.exists():
        manifest = load_json(manifest_path)
        for name in manifest.get("dependencies", {}):
            if name.startswith(PACKAGE_PREFIX):
                declared[name] = "Packages/manifest.json dependencies"
        for name in manifest.get("testables", []):
            if name.startswith(PACKAGE_PREFIX):
                declared.setdefault(name, "Packages/manifest.json testables")

    gitmodules_path = host_root / ".gitmodules"
    if gitmodules_path.exists():
        parser = configparser.ConfigParser()
        parser.read(gitmodules_path, encoding="utf-8")
        for section in parser.sections():
            submodule_path = parser[section].get("path", "")
            name = Path(submodule_path).name
            if submodule_path.startswith(PACKAGES_FOLDER + "/") and name.startswith(PACKAGE_PREFIX):
                declared.setdefault(name, ".gitmodules")
    return declared


class DependencyChecker:
    def __init__(self, graph, workspace):
        self._workspace = workspace
        self._packages = graph["packages"]
        self._externals = graph.get("externalPackages", {})
        self._errors = []
        self._verified = []
        self._assembly_owners = self._build_assembly_owners()

    @property
    def errors(self):
        return self._errors

    @property
    def verified(self):
        return self._verified

    def _error(self, message):
        self._errors.append(message)

    def _build_assembly_owners(self):
        owners = {}
        for package_id, entry in self._packages.items():
            owners[entry["assemblyRoot"]] = package_id
        for package_id, entry in self._externals.items():
            owners[entry["assemblyRoot"]] = package_id
        return sorted(owners.items(), key=lambda item: len(item[0]), reverse=True)

    def _owner_of(self, assembly_name):
        for assembly_root, package_id in self._assembly_owners:
            if assembly_name == assembly_root or assembly_name.startswith(assembly_root + "."):
                return package_id
        return None

    def _define_of(self, package_id):
        if package_id in self._packages:
            return self._packages[package_id]["define"]
        return self._externals[package_id]["define"]

    def check_graph(self):
        for package_id, entry in self._packages.items():
            if entry.get("status") not in VALID_STATUSES:
                self._error(f"graph: {package_id} has status '{entry.get('status')}'; "
                            f"expected one of {list(VALID_STATUSES)}")
            overlap = set(entry["hard"]) & set(entry["optional"])
            if overlap:
                self._error(f"graph: {package_id} lists {sorted(overlap)} as both hard and optional")
            for dependency in entry["hard"] + entry["optional"]:
                if dependency in self._externals:
                    continue
                if dependency not in self._packages:
                    self._error(f"graph: {package_id} depends on unknown package {dependency}")
                    continue
                if self._packages[dependency]["layer"] >= entry["layer"]:
                    self._error(f"graph: {package_id} (L{entry['layer']}) depends on {dependency} "
                                f"(L{self._packages[dependency]['layer']}); dependencies must be on lower layers")

    def expected_packages(self, host_declarations, requested):
        """Returns {package_id: reason} for packages that must be found and verified."""
        expected = {}
        for package_id, entry in self._packages.items():
            if entry.get("status") == STATUS_IMPLEMENTED:
                expected[package_id] = "marked implemented in dependency-graph.json"
        for package_id, reason in host_declarations.items():
            expected.setdefault(package_id, "declared by the host in " + reason)
        for package_id in requested:
            expected[package_id] = "requested with --package"
        return expected

    def check_presence(self, expected, requested):
        """Reports unknown, missing and uninitialized packages. Returns the ids that can be verified."""
        for package_id in sorted(expected):
            if package_id not in self._packages:
                self._error(f"{package_id}: expected ({expected[package_id]}) but not declared in the graph")

        for package_dir in sorted(self._workspace.glob(PACKAGE_PREFIX + "*")):
            if package_dir.is_dir() and package_dir.name not in self._packages:
                self._error(f"{package_dir.name}: found in the workspace but not declared in the graph")

        candidates = requested if requested else list(self._packages)
        to_verify = []
        for package_id in candidates:
            if package_id not in self._packages:
                continue
            package_dir = self._workspace / package_id
            is_expected = package_id in expected
            if not package_dir.is_dir():
                if is_expected:
                    self._error(f"{package_id}: expected ({expected[package_id]}) but not found in {self._workspace}")
                continue
            if not (package_dir / "package.json").exists():
                self._error(f"{package_id}: folder exists but has no package.json "
                            f"(uninitialized submodule? run 'git submodule update --init')")
                continue
            if self._packages[package_id].get("status") != STATUS_IMPLEMENTED:
                self._error(f"{package_id}: found in the workspace but marked "
                            f"'{self._packages[package_id].get('status')}' in the graph; set status to "
                            f"'{STATUS_IMPLEMENTED}'")
            to_verify.append(package_id)
        return to_verify

    def verify_package(self, package_id):
        package_dir = self._workspace / package_id
        self._check_manifest(package_id, package_dir)
        for asmdef_path in sorted(package_dir.rglob("*.asmdef")):
            self._check_asmdef(package_id, package_dir, asmdef_path)
        self._verified.append(package_id)

    def _check_manifest(self, package_id, package_dir):
        manifest = load_json(package_dir / "package.json")
        if manifest.get("name") != package_id:
            self._error(f"{package_id}: package.json name is '{manifest.get('name')}'")
        if not SEMANTIC_VERSION_PATTERN.match(manifest.get("version", "")):
            self._error(f"{package_id}: version '{manifest.get('version')}' is not SemVer")
        if "unity" not in manifest:
            self._error(f"{package_id}: package.json has no 'unity' field")

        declared = {name for name in manifest.get("dependencies", {}) if name.startswith(PACKAGE_PREFIX)}
        expected = set(self._packages[package_id]["hard"])
        for extra in sorted(declared - expected):
            self._error(f"{package_id}: package.json depends on {extra}, which is not a hard dependency in the graph")
        for missing in sorted(expected - declared):
            self._error(f"{package_id}: package.json is missing hard dependency {missing}")

    def _check_asmdef(self, package_id, package_dir, asmdef_path):
        relative_path = asmdef_path.relative_to(package_dir)
        label = f"{package_id}/{relative_path.as_posix()}"
        asmdef = load_json(asmdef_path)
        entry = self._packages[package_id]
        is_integration = INTEGRATION_FOLDER in relative_path.parts
        is_sample = SAMPLES_FOLDER in relative_path.parts

        if relative_path.parts[0] == "Runtime" and len(relative_path.parts) == 2:
            if asmdef.get("name") != entry["assemblyRoot"]:
                self._error(f"{label}: runtime assembly must be named {entry['assemblyRoot']}")

        define_constraints = set(asmdef.get("defineConstraints", []))
        version_defines = {(item.get("name"), item.get("define")) for item in asmdef.get("versionDefines", [])}

        for reference in asmdef.get("references", []):
            if reference.startswith(GUID_REFERENCE_PREFIX):
                self._error(f"{label}: reference '{reference}' uses a GUID; reference assemblies by name")
                continue
            owner = self._owner_of(reference)
            if owner is None:
                if not reference.startswith(UNITY_ASSEMBLY_PREFIXES):
                    self._error(f"{label}: references '{reference}', which is neither a framework nor a Unity "
                                f"assembly (packages must not depend on game code)")
                continue
            if owner == package_id or owner in entry["hard"]:
                continue
            if owner not in entry["optional"]:
                self._error(f"{label}: references {reference} ({owner}), which is not allowed by the graph")
                continue
            if is_sample:
                continue
            if not is_integration:
                self._error(f"{label}: optional dependency {owner} may only be referenced from "
                            f"Runtime/{INTEGRATION_FOLDER}/<Package>/ assemblies")
                continue
            define = self._define_of(owner)
            if define not in define_constraints:
                self._error(f"{label}: missing defineConstraints entry '{define}' for optional {owner}")
            if (owner, define) not in version_defines:
                self._error(f"{label}: missing versionDefines entry name='{owner}' define='{define}'")


def parse_arguments(argv):
    parser = argparse.ArgumentParser(description="Validate RamiresTech package dependencies.")
    parser.add_argument("--package", action="append", default=[],
                        help="Package to verify (short or full id). Repeatable. Must be found.")
    parser.add_argument("--graph", type=Path, default=GRAPH_PATH, help="Path to dependency-graph.json.")
    parser.add_argument("--workspace", type=Path, default=None,
                        help="Folder that contains the package repositories (default: <host-root>/Packages).")
    parser.add_argument("--host-root", type=Path, default=HOST_ROOT,
                        help="Unity project whose manifest.json/.gitmodules declare expected packages.")
    parser.add_argument("--no-host", action="store_true",
                        help="Ignore host declarations (only graph status and --package define expectations).")
    return parser.parse_args(argv)


def run(argv=None):
    args = parse_arguments(argv)
    host_root = None if args.no_host else args.host_root.resolve()
    workspace = args.workspace.resolve() if args.workspace else args.host_root.resolve() / PACKAGES_FOLDER

    checker = DependencyChecker(load_json(args.graph), workspace)
    checker.check_graph()
    requested = [normalize_package_id(name) for name in args.package]
    expected = checker.expected_packages(read_host_declarations(host_root), requested)
    for package_id in checker.check_presence(expected, requested):
        checker.verify_package(package_id)

    if not checker.verified:
        checker.errors.append(f"no package was verified in {workspace}; nothing to report as consistent")

    print(f"Packages verified: {', '.join(checker.verified) if checker.verified else 'none'}")
    if checker.errors:
        print(f"{len(checker.errors)} error(s):")
        for message in checker.errors:
            print("  - " + message)
        return 1
    print("OK: dependency graph and package manifests are consistent.")
    return 0


if __name__ == "__main__":
    sys.exit(run())
