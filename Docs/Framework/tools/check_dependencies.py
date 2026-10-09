#!/usr/bin/env python3
"""Purpose: Validate the framework dependency graph and every package manifest and asmdef against it.

Checks:
  * The graph itself: known packages only, strictly lower layers, hard and optional do not overlap.
  * package.json: name, SemVer version, unity field, RamiresTech dependencies == hard dependencies.
  * asmdef: references by name (no GUID), only allowed packages, optional packages only inside
    Integration assemblies guarded by defineConstraints + versionDefines.

Usage:
    python tools/check_dependencies.py [--workspace <folder with package repositories>]
Exit code 0 when no errors are found, 1 otherwise.
"""
import argparse
import json
import re
import sys
from pathlib import Path

FRAMEWORK_ROOT = Path(__file__).resolve().parent.parent
GRAPH_PATH = FRAMEWORK_ROOT / "dependency-graph.json"
PACKAGE_PREFIX = "com.ramirestechgames."
GUID_REFERENCE_PREFIX = "GUID:"
INTEGRATION_FOLDER = "Integration"
SAMPLES_FOLDER = "Samples~"
SEMANTIC_VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")


def load_json(path):
    with path.open(encoding="utf-8-sig") as json_file:
        return json.load(json_file)


class DependencyChecker:
    def __init__(self, graph, workspace):
        self._graph = graph
        self._workspace = workspace
        self._packages = graph["packages"]
        self._externals = graph.get("externalPackages", {})
        self._errors = []
        self._assembly_owners = self._build_assembly_owners()

    @property
    def errors(self):
        return self._errors

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

    def check_workspace(self):
        found = []
        for package_id in self._packages:
            package_dir = self._workspace / package_id
            if not package_dir.is_dir():
                continue
            found.append(package_id)
            self._check_manifest(package_id, package_dir)
            for asmdef_path in sorted(package_dir.rglob("*.asmdef")):
                self._check_asmdef(package_id, package_dir, asmdef_path)
        return found

    def _check_manifest(self, package_id, package_dir):
        manifest_path = package_dir / "package.json"
        if not manifest_path.exists():
            self._error(f"{package_id}: package.json not found")
            return
        manifest = load_json(manifest_path)
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
            if owner is None or owner == package_id or owner in entry["hard"]:
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


def parse_arguments():
    parser = argparse.ArgumentParser(description="Validate RamiresTech package dependencies.")
    parser.add_argument("--workspace", type=Path, default=FRAMEWORK_ROOT.parent,
                        help="Folder that contains the package repositories.")
    return parser.parse_args()


def main():
    args = parse_arguments()
    checker = DependencyChecker(load_json(GRAPH_PATH), args.workspace.resolve())
    checker.check_graph()
    found = checker.check_workspace()

    print(f"Packages checked: {', '.join(found) if found else 'none found'}")
    if checker.errors:
        print(f"{len(checker.errors)} error(s):")
        for message in checker.errors:
            print("  - " + message)
        return 1
    print("OK: dependency graph and package manifests are consistent.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
