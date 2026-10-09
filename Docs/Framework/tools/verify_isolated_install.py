#!/usr/bin/env python3
"""Purpose: Quality Gate QG12 - install a package in a disposable, empty Unity project and prove it compiles and
passes its tests without any game code or assets.

The disposable project contains only:
  * the package under test and its hard dependencies (transitive, from dependency-graph.json), copied without .git;
  * com.unity.test-framework (to run the package tests);
  * an empty Assets folder.

Steps: stage packages -> run the Editor in batchmode with -runTests (EditMode, and PlayMode when the package has
Tests/Runtime) -> parse the Editor log for compiler errors/warnings in framework packages -> parse NUnit results.

Usage (from the host root):
    python Docs/Framework/tools/verify_isolated_install.py core [--keep] [--editor-path <Unity executable>]
Exit code 0 when the package compiles without errors/warnings and every test passes, 1 otherwise.
"""
import argparse
import json
import platform
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ElementTree
from pathlib import Path

FRAMEWORK_ROOT = Path(__file__).resolve().parent.parent
HOST_ROOT = FRAMEWORK_ROOT.parent.parent
GRAPH_PATH = FRAMEWORK_ROOT / "dependency-graph.json"
PACKAGES_ROOT = HOST_ROOT / "Packages"
HOST_MANIFEST_PATH = PACKAGES_ROOT / "manifest.json"
HOST_VERSION_PATH = HOST_ROOT / "ProjectSettings" / "ProjectVersion.txt"
PACKAGE_PREFIX = "com.ramirestechgames."
TEST_FRAMEWORK_PACKAGE = "com.unity.test-framework"
DEFAULT_TEST_FRAMEWORK_VERSION = "1.8.0"
STAGED_FOLDER = "Staged"
PROJECT_FOLDER = "Project"
IGNORED_COPY_NAMES = (".git", ".vs", ".idea", ".vscode")
TEST_MODES = ("EditMode", "PlayMode")
EDITOR_TIMEOUT_SECONDS = 1800
COMPILER_MESSAGE_PATTERN = re.compile(r"(?P<file>[^\s:(][^(]*\.cs)\(\d+,\d+\): (?P<kind>error|warning) (?P<code>CS\d+)")
EDITOR_VERSION_PATTERN = re.compile(r"m_EditorVersion:\s*(\S+)")


def load_json(path):
    with path.open(encoding="utf-8-sig") as json_file:
        return json.load(json_file)


def resolve_closure(graph, package_id):
    """Package plus its transitive hard dependencies, dependencies first."""
    ordered = []

    def visit(current):
        if current in ordered:
            return
        for dependency in graph["packages"][current]["hard"]:
            visit(dependency)
        ordered.append(current)

    visit(package_id)
    return ordered


def read_host_editor_version():
    match = EDITOR_VERSION_PATTERN.search(HOST_VERSION_PATH.read_text(encoding="utf-8"))
    if match is None:
        raise RuntimeError(f"Editor version not found in {HOST_VERSION_PATH}")
    return match.group(1)


def read_test_framework_version():
    if not HOST_MANIFEST_PATH.exists():
        return DEFAULT_TEST_FRAMEWORK_VERSION
    return load_json(HOST_MANIFEST_PATH).get("dependencies", {}).get(
        TEST_FRAMEWORK_PACKAGE, DEFAULT_TEST_FRAMEWORK_VERSION)


def find_editor_executable(editor_version):
    result = subprocess.run(["unity", "editors", "path", editor_version, "--no-banner"],
                            capture_output=True, text=True, check=True)
    install_dir = Path(result.stdout.strip().splitlines()[-1].strip())
    system = platform.system()
    if system == "Windows":
        return install_dir / "Editor" / "Unity.exe"
    if system == "Darwin":
        return install_dir / "Unity.app" / "Contents" / "MacOS" / "Unity"
    return install_dir / "Editor" / "Unity"


def stage_project(root, closure, package_id, editor_version):
    staged_root = root / STAGED_FOLDER
    project_root = root / PROJECT_FOLDER
    dependencies = {}
    for current in closure:
        source = PACKAGES_ROOT / current
        if not (source / "package.json").exists():
            raise RuntimeError(f"{current} not found in {PACKAGES_ROOT} (required by {package_id})")
        shutil.copytree(source, staged_root / current, ignore=shutil.ignore_patterns(*IGNORED_COPY_NAMES))
        dependencies[current] = (staged_root / current).as_uri().replace("file:///", "file:")
    dependencies[TEST_FRAMEWORK_PACKAGE] = read_test_framework_version()

    (project_root / "Assets").mkdir(parents=True)
    (project_root / "ProjectSettings").mkdir()
    (project_root / "Packages").mkdir()
    manifest = {"dependencies": dependencies, "testables": [package_id]}
    (project_root / "Packages" / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    (project_root / "ProjectSettings" / "ProjectVersion.txt").write_text(
        f"m_EditorVersion: {editor_version}\n", encoding="utf-8")
    return project_root


def run_tests(editor, project_root, mode, output_dir):
    results_path = output_dir / f"results-{mode}.xml"
    log_path = output_dir / f"editor-{mode}.log"
    # No -nographics: editor UI tests need a graphics device (headless CI may add it; such tests self-skip).
    command = [str(editor), "-batchmode", "-projectPath", str(project_root),
               "-runTests", "-testPlatform", mode, "-testResults", str(results_path), "-logFile", str(log_path)]
    print(f"  running {mode} tests (this can take a few minutes)...")
    completed = subprocess.run(command, timeout=EDITOR_TIMEOUT_SECONDS, check=False)
    return completed.returncode, results_path, log_path


def collect_compiler_messages(log_path):
    """Compiler errors/warnings reported for framework packages (staged copies)."""
    messages = set()
    if not log_path.exists():
        return messages
    for line in log_path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = COMPILER_MESSAGE_PATTERN.search(line)
        if match and (PACKAGE_PREFIX in match.group("file") or STAGED_FOLDER in match.group("file")):
            messages.add(line.strip())
    return messages


def read_results(results_path):
    if not results_path.exists():
        return None
    run = ElementTree.parse(results_path).getroot()
    return {key: int(run.get(key, "0")) for key in ("total", "passed", "failed", "skipped")}


def parse_arguments():
    parser = argparse.ArgumentParser(description="QG12: verify a package in a disposable empty Unity project.")
    parser.add_argument("package", help="Package short or full id, e.g. 'core'.")
    parser.add_argument("--editor-path", type=Path, help="Unity executable. Default: resolved via the Unity CLI.")
    parser.add_argument("--keep", action="store_true", help="Keep the disposable project for inspection.")
    return parser.parse_args()


def main():
    args = parse_arguments()
    package_id = args.package if args.package.startswith(PACKAGE_PREFIX) else PACKAGE_PREFIX + args.package
    graph = load_json(GRAPH_PATH)
    if package_id not in graph["packages"]:
        print(f"error: {package_id} is not declared in the dependency graph")
        return 1

    closure = resolve_closure(graph, package_id)
    editor_version = read_host_editor_version()
    editor = args.editor_path or find_editor_executable(editor_version)
    has_runtime_tests = (PACKAGES_ROOT / package_id / "Tests" / "Runtime").is_dir()
    modes = TEST_MODES if has_runtime_tests else TEST_MODES[:1]

    root = Path(tempfile.mkdtemp(prefix="rtg-isolated-"))
    print(f"QG12 isolated install: {package_id}")
    print(f"  packages: {', '.join(closure)} + {TEST_FRAMEWORK_PACKAGE}")
    print(f"  editor: {editor} ({editor_version})")
    print(f"  project: {root / PROJECT_FOLDER}")

    failures = []
    try:
        project_root = stage_project(root, closure, package_id, editor_version)
        for mode in modes:
            exit_code, results_path, log_path = run_tests(editor, project_root, mode, root)
            compiler_messages = collect_compiler_messages(log_path)
            results = read_results(results_path)
            print(f"  {mode}: editor exit code {exit_code}, results {results}, "
                  f"compiler messages {len(compiler_messages)}")
            for message in sorted(compiler_messages):
                failures.append(f"{mode}: {message}")
            if results is None:
                failures.append(f"{mode}: no test results were produced (see {log_path})")
            elif results["failed"] > 0:
                failures.append(f"{mode}: {results['failed']} test(s) failed (see {results_path})")
            elif mode == TEST_MODES[0] and results["total"] == 0:
                failures.append(f"{mode}: no tests were found for {package_id}")
    finally:
        if args.keep or failures:
            print(f"  kept disposable project for inspection: {root}")
        else:
            shutil.rmtree(root, ignore_errors=True)

    if failures:
        print(f"FAILED ({len(failures)}):")
        for failure in failures:
            print("  - " + failure)
        return 1
    print(f"OK: {package_id} installs, compiles without errors/warnings and passes its tests in an empty project.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
