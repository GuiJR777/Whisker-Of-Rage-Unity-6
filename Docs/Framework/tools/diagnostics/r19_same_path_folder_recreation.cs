// Purpose: Diagnostic for risk R19: deletes and immediately recreates the SAME folder path in the AssetDatabase,
// with and without a missing [SerializeReference] type and an Undo record, and reports whether the folder really
// exists on disk afterwards. The behaviour is not deterministic, so this is a probe, not a test.
//
// Run with the host Editor open (Core listed in manifest "testables", for the PickerTestAsset fixture):
//   unity command eval_file --project-path . --file Docs/Framework/tools/diagnostics/r19_same_path_folder_recreation.cs
// Output (also written to Temp/r19_probe.txt, because the probe can outlast the CLI reply timeout): one line per
// case, "recreated=False" when CreateFolder returned a GUID without creating the folder.
// The tests avoid the pattern by using a new folder per test (TemporaryAssetFolder in each package's test assembly).
const int CYCLES = 3;
var report = new System.Text.StringBuilder();

bool Recreate(string name, bool missingType, bool registerUndo)
{
    string folder = "Assets/" + name;
    string path = folder + "/Probe.asset";
    UnityEditor.AssetDatabase.CreateFolder("Assets", name);
    var asset = UnityEngine.ScriptableObject.CreateInstance<RamiresTechGames.Core.Tests.Runtime.Fixtures.PickerTestAsset>();
    asset.Value = new RamiresTechGames.Core.Tests.Runtime.Fixtures.PickerTestAlpha();
    UnityEditor.AssetDatabase.CreateAsset(asset, path);
    UnityEditor.AssetDatabase.SaveAssets();
    if (missingType)
    {
        System.IO.File.WriteAllText(path, System.IO.File.ReadAllText(path)
            .Replace("class: PickerTestAlpha", "class: PickerTestRemovedByR19Probe"));
        UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
    }

    if (registerUndo)
    {
        UnityEditor.Undo.RegisterCompleteObjectUndo(UnityEditor.AssetDatabase.LoadMainAssetAtPath(path), "R19 probe");
    }

    UnityEditor.AssetDatabase.DeleteAsset(folder);
    UnityEditor.AssetDatabase.CreateFolder("Assets", name);
    bool recreated = System.IO.Directory.Exists(folder);
    UnityEditor.AssetDatabase.DeleteAsset(folder);
    return recreated;
}

for (int cycle = 0; cycle < CYCLES; cycle++)
{
    foreach (bool missingType in new[] { false, true })
    {
        foreach (bool registerUndo in new[] { false, true })
        {
            string name = "_R19Probe_" + cycle + (missingType ? "M" : "-") + (registerUndo ? "U" : "-");
            report.Append(name).Append(" missingType=").Append(missingType).Append(" undo=").Append(registerUndo)
                .Append(" recreated=").Append(Recreate(name, missingType, registerUndo)).Append('\n');
        }
    }
}

UnityEditor.Undo.ClearAll();
System.IO.File.WriteAllText("Temp/r19_probe.txt", report.ToString());
return report.ToString();
