#!/usr/bin/env python3
"""Purpose: Scaffold a new RamiresTech Games Unity package from templates/package.

The package must already be declared in dependency-graph.json (adding a package is an architectural
decision and requires an ADR). Dependencies, assembly names and defines are read from the graph.

Usage:
    python Docs/Framework/tools/new_package.py stats --description "Generic attributes, resources and modifiers." --milestone M1
"""
import argparse
import datetime
import json
import re
import subprocess
import sys
from pathlib import Path

FRAMEWORK_ROOT = Path(__file__).resolve().parent.parent
HOST_ROOT = FRAMEWORK_ROOT.parent.parent
PACKAGES_ROOT = HOST_ROOT / "Packages"
TEMPLATE_DIR = FRAMEWORK_ROOT / "templates" / "package"
GRAPH_PATH = FRAMEWORK_ROOT / "dependency-graph.json"
PACKAGE_PREFIX = "com.ramirestechgames."
UNITY_VERSION = "6000.6"
DEFAULT_DEPENDENCY_VERSION = "0.1.0"
CORE_PACKAGE_ID = "com.ramirestechgames.core"
CORE_EDITOR_ASSEMBLY = "RamiresTechGames.Core.Editor"
JSON_KEY_INDENT = 2
STATUS_IMPLEMENTED = "implemented"
UNRESOLVED_TOKEN_PATTERN = re.compile(r"\{\{[A-Z_]+\}\}")


def load_graph():
    with GRAPH_PATH.open(encoding="utf-8") as graph_file:
        return json.load(graph_file)


def split_pascal_case(name):
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", name)


def format_nested_json(value):
    """Formats a JSON value so it sits correctly under a key indented by JSON_KEY_INDENT spaces."""
    if not value:
        return "{}" if isinstance(value, dict) else "[]"
    lines = json.dumps(value, indent=JSON_KEY_INDENT).splitlines()
    padding = " " * JSON_KEY_INDENT
    return "\n".join([lines[0]] + [padding + line for line in lines[1:]])


def format_markdown_list(package_ids):
    if not package_ids:
        return "nenhuma"
    return ", ".join("`" + package_id + "`" for package_id in package_ids)


def read_sibling_version(output_root, package_id):
    manifest_path = output_root / package_id / "package.json"
    if not manifest_path.exists():
        return DEFAULT_DEPENDENCY_VERSION
    with manifest_path.open(encoding="utf-8-sig") as manifest_file:
        return json.load(manifest_file).get("version", DEFAULT_DEPENDENCY_VERSION)


def build_tokens(args, graph, package_id, output_root):
    entry = graph["packages"][package_id]
    assembly_root = entry["assemblyRoot"]
    system_name = assembly_root.split(".")[-1]
    hard_dependencies = entry["hard"]
    runtime_references = [graph["packages"][dependency]["assemblyRoot"] for dependency in hard_dependencies]

    editor_references = [assembly_root] + runtime_references
    if CORE_PACKAGE_ID in hard_dependencies:
        editor_references.append(CORE_EDITOR_ASSEMBLY)

    test_references = ["UnityEngine.TestRunner", "UnityEditor.TestRunner", assembly_root, assembly_root + ".Editor"]
    test_references += runtime_references

    dependency_versions = {
        dependency: read_sibling_version(output_root, dependency) for dependency in hard_dependencies
    }
    today = datetime.date.today()

    return {
        "{{PACKAGE_ID}}": package_id,
        "{{PACKAGE_SHORT_ID}}": package_id[len(PACKAGE_PREFIX):],
        "{{ASSEMBLY_ROOT}}": assembly_root,
        "{{SYSTEM_NAME}}": system_name,
        "{{DISPLAY_NAME}}": args.display_name or split_pascal_case(system_name),
        "{{DESCRIPTION}}": args.description,
        "{{DEFINE}}": entry["define"],
        "{{UNITY_VERSION}}": UNITY_VERSION,
        "{{MILESTONE}}": args.milestone,
        "{{YEAR}}": str(today.year),
        "{{DATE}}": today.isoformat(),
        "{{HARD_DEPENDENCIES_JSON}}": format_nested_json(dependency_versions),
        "{{RUNTIME_REFERENCES_JSON}}": format_nested_json(runtime_references),
        "{{EDITOR_REFERENCES_JSON}}": format_nested_json(editor_references),
        "{{TEST_REFERENCES_JSON}}": format_nested_json(test_references),
        "{{HARD_DEPS_MD}}": format_markdown_list(hard_dependencies),
        "{{OPTIONAL_DEPS_MD}}": format_markdown_list(entry["optional"]),
    }


def replace_tokens(text, tokens):
    for token, value in tokens.items():
        text = text.replace(token, value)
    return text


def render_relative_path(relative_path, tokens):
    rendered = str(relative_path)
    rendered = rendered.replace("__ASSEMBLY_ROOT__", tokens["{{ASSEMBLY_ROOT}}"])
    rendered = rendered.replace("__SYSTEM_NAME__", tokens["{{SYSTEM_NAME}}"])
    return Path(rendered)


def copy_template(target_dir, tokens):
    unresolved = []
    for source_path in sorted(TEMPLATE_DIR.rglob("*")):
        if not source_path.is_file():
            continue
        relative_path = render_relative_path(source_path.relative_to(TEMPLATE_DIR), tokens)
        destination_path = target_dir / relative_path
        destination_path.parent.mkdir(parents=True, exist_ok=True)
        content = replace_tokens(source_path.read_text(encoding="utf-8"), tokens)
        if UNRESOLVED_TOKEN_PATTERN.search(content):
            unresolved.append(str(relative_path))
        destination_path.write_text(content, encoding="utf-8", newline="\n")
    return unresolved


def initialize_git(target_dir):
    subprocess.run(["git", "init", "--initial-branch=main"], cwd=target_dir, check=True)


def mark_implemented(graph, package_id):
    """Marks the package as implemented so check_dependencies.py expects it from now on."""
    graph["packages"][package_id]["status"] = STATUS_IMPLEMENTED
    GRAPH_PATH.write_text(json.dumps(graph, indent=JSON_KEY_INDENT) + "\n", encoding="utf-8", newline="\n")


def parse_arguments():
    parser = argparse.ArgumentParser(description="Scaffold a RamiresTech Games Unity package.")
    parser.add_argument("short_id", help="Package short id, e.g. 'stats' for com.ramirestechgames.stats")
    parser.add_argument("--description", required=True, help="One-sentence package description (English).")
    parser.add_argument("--display-name", help="Menu display name. Defaults to the spaced system name.")
    parser.add_argument("--milestone", default="M?", help="Milestone that implements the package, e.g. M1.")
    parser.add_argument("--output-root", type=Path, default=PACKAGES_ROOT,
                        help="Folder that holds the package repositories (default: <host>/Packages).")
    parser.add_argument("--no-git", action="store_true", help="Do not run git init.")
    return parser.parse_args()


def main():
    args = parse_arguments()
    graph = load_graph()
    package_id = PACKAGE_PREFIX + args.short_id

    if package_id not in graph["packages"]:
        print(f"error: {package_id} is not declared in {GRAPH_PATH.name}. Add it there first (requires an ADR).")
        return 1

    output_root = args.output_root.resolve()
    target_dir = output_root / package_id
    if target_dir.exists():
        print(f"error: {target_dir} already exists.")
        return 1

    tokens = build_tokens(args, graph, package_id, output_root)
    unresolved = copy_template(target_dir, tokens)
    if unresolved:
        print("warning: unresolved template tokens in: " + ", ".join(unresolved))

    if not args.no_git:
        initialize_git(target_dir)

    if args.output_root.resolve() == PACKAGES_ROOT.resolve():
        mark_implemented(graph, package_id)
        print(f"Marked {package_id} as '{STATUS_IMPLEMENTED}' in {GRAPH_PATH.name}.")

    print(f"Created {target_dir}")
    print("Next steps:")
    print(f"  1. Let Unity import it: add \"{package_id}\" to testables, run 'unity command package_resolve'")
    print("     (generates .meta files), then commit inside the package repository.")
    print(f"  2. gh repo create GuiJR777/{package_id} --private --source Packages/{package_id} --push")
    print(f"  3. git submodule add https://github.com/GuiJR777/{package_id}.git Packages/{package_id}")
    print("  4. Fill CLAUDE.md section 3 (limits) and ROADMAP.md for the milestone.")
    print("  5. Run python Docs/Framework/tools/check_dependencies.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
