// Purpose: Smoke tests of the Equipment samples: Package Manager import, asset references, saved scenes and Play Mode.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using NUnit.Framework;

using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

using RamiresTechGames.Core.Validation;
using RamiresTechGames.Equipment.Components;
using RamiresTechGames.Equipment.Definitions;
using RamiresTechGames.Equipment.Items;
using RamiresTechGames.Equipment.Storage;
using RamiresTechGames.Equipment.Transactions;
using RamiresTechGames.Stats.Components;
using RamiresTechGames.Stats.Definitions;
#if RAMIRESTECHGAMES_ABILITIES
using RamiresTechGames.Abilities.Abilities;
using RamiresTechGames.Abilities.Components;
#endif

using Object = UnityEngine.Object;

namespace RamiresTechGames.Sandbox.Tests.Samples.Equipment
{
    /// <summary>
    /// Imports each Equipment sample with the Package Manager (when it is not imported yet), checks every asset and the
    /// saved scene (references, managed reference types, missing scripts, validation errors), forces a domain reload
    /// with the scene open, then enters Play Mode and checks the equipped hero: items, stash, stats, transactions,
    /// violations and, with Abilities, the granted abilities and a refused swap that keeps the strike. Only what the
    /// test imported is deleted. Gameplay itself is not automated (the owner's manual validation stays).
    /// </summary>
    internal sealed class EquipmentSampleSmokeTests
    {
        private const string PACKAGE = "com.ramirestechgames.equipment";
        private const string STATE_KEY = "RamiresTechGames.Sandbox.EquipmentSampleSmoke.";
        private const int FRAMES = 60;
        private const double PLAY_TIMEOUT_SECONDS = 120.0;
        private const int TIMEOUT_MILLISECONDS = 900000;

        private static Sample Find(string displayName)
        {
            foreach (Sample sample in Sample.FindByPackage(PACKAGE, null))
            {
                if (sample.displayName == displayName)
                {
                    return sample;
                }
            }

            Assert.Fail("sample " + displayName + " not found in " + PACKAGE);
            return default;
        }

        private static string ToAssetPath(string path)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/') + "/";
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase) ? full.Substring(project.Length) : full;
        }

        private static void AddProblem(string displayName, string problem)
        {
            string key = STATE_KEY + "Problems." + displayName;
            SessionState.SetString(key, SessionState.GetString(key, string.Empty) + problem + "\n");
        }

        private static string Problems(string displayName)
        {
            return SessionState.GetString(STATE_KEY + "Problems." + displayName, string.Empty);
        }

        /// <summary>Broken object references and managed references with missing types.</summary>
        internal static void CheckObject(string displayName, Object target, string label)
        {
            bool holdsManagedReferences = target is MonoBehaviour || target is ScriptableObject;
            if (holdsManagedReferences && SerializationUtility.HasManagedReferencesWithMissingTypes(target))
            {
                AddProblem(displayName, label + ": managed reference with a missing type");
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null &&
                    !property.objectReferenceEntityIdValue.Equals(default(EntityId)))
                {
                    AddProblem(displayName, label + ": broken reference " + property.propertyPath);
                }
            }
        }

        /// <summary>Missing scripts, broken references and validation issues of a hierarchy.</summary>
        internal static void CheckGameObject(string displayName, GameObject gameObject, ValidationReport report)
        {
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                {
                    AddProblem(displayName, child.name + ": missing script");
                }

                foreach (Component component in child.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    CheckObject(displayName, component, child.name + "/" + component.GetType().Name);
                    if (component is IValidatable validatable)
                    {
                        validatable.Validate(report);
                    }
                }
            }
        }

        internal static void ReportErrors(string displayName, ValidationReport report)
        {
            foreach (ValidationIssue issue in report.Issues)
            {
                if (issue.Severity == ValidationSeverity.Error)
                {
                    AddProblem(displayName, "validation error: " + issue.Message);
                }
            }
        }

        private static int CheckAssets(string displayName, string root)
        {
            ValidationReport report = new ValidationReport();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || path.EndsWith(".unity", StringComparison.Ordinal))
                {
                    continue;
                }

                count++;
                Object main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main == null)
                {
                    AddProblem(displayName, path + ": does not load");
                    continue;
                }

                if (main is GameObject prefab)
                {
                    CheckGameObject(displayName, prefab, report);
                    continue;
                }

                CheckObject(displayName, main, path);
                if (main is IValidatable validatable)
                {
                    validatable.Validate(report);
                }
            }

            ReportErrors(displayName, report);
            return count;
        }

        private static void CheckScene(string displayName)
        {
            ValidationReport report = new ValidationReport();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                CheckGameObject(displayName, root, report);
            }

            ReportErrors(displayName, report);
            int components = Object.FindObjectsByType<EquipmentComponent>(FindObjectsInactive.Include).Length;
            if (components != 1)
            {
                AddProblem(displayName, "scene has " + components + " equipment components");
            }
        }

        private static float Value(StatsComponent stats, string statName)
        {
            foreach (StatEntry entry in stats.StatSet.Stats)
            {
                if (entry.Stat.name == statName)
                {
                    return stats.Stats.GetValue(entry.Stat);
                }
            }

            return float.NaN;
        }

        private static EquipmentItem InStash(EquipmentStash stash, string definitionName)
        {
            foreach (EquipmentItem item in stash.Items)
            {
                if (item.Definition.name == definitionName)
                {
                    return item;
                }
            }

            return null;
        }

        private static void Expect(string displayName, string what, object actual, object expected)
        {
            if (!Equals(actual, expected))
            {
                AddProblem(displayName, what + ": " + actual + ", expected " + expected);
            }
        }

        private static void CheckBasics(string displayName, EquipmentComponent equipment)
        {
            EquipmentController controller = equipment.Controller;
            List<EquipmentItem> equipped = new List<EquipmentItem>();
            controller.GetEquipped(equipped);
            StatsComponent stats = equipment.GetComponent<StatsComponent>();
            Expect(displayName, "equipped", equipped.Count, 4);
            Expect(displayName, "stash", equipment.GetComponent<EquipmentStash>().Count, 4);
            Expect(displayName, "Strength (loadout A)", Value(stats, "Strength"), 17f);
            Expect(displayName, "Defense (loadout A)", Value(stats, "Defense"), 7f);
            Expect(displayName, "first transaction", controller.GetTransactionRecord(
                controller.TransactionLogCount - 1).Kind, EquipmentTransactionKind.ApplyLoadoutState);
        }

#if RAMIRESTECHGAMES_ABILITIES
        private static void CheckAbilities(string displayName, EquipmentComponent equipment)
        {
            EquipmentController controller = equipment.Controller;
            AbilitySystemComponent system = equipment.GetComponent<AbilitySystemComponent>();
            StatsComponent stats = equipment.GetComponent<StatsComponent>();
            List<AbilityGrantInfo> grants = new List<AbilityGrantInfo>();
            system.GetAbilities(grants);
            StringBuilder names = new StringBuilder();
            foreach (AbilityGrantInfo grant in grants)
            {
                names.Append(grant.Definition.name).Append(' ');
            }

            Expect(displayName, "grants", names.ToString(), "FlameSlash VigorAura ");
            Expect(displayName, "MaxHealth (amulet passive)", Value(stats, "MaxHealth"), 125f);
            Expect(displayName, "Strength (sword)", Value(stats, "Strength"), 12f);
            EquipmentStash stash = equipment.GetComponent<EquipmentStash>();
            EquipmentItem heavy = InStash(stash, "HeavyBlade");
            AbilityHandle strike = grants.Count > 0 ? grants[0].Handle : AbilityHandle.Invalid;
            EquipResult swap = controller.TrySwap(controller.GetHandle(0), heavy,
                new EquipOptions { Source = stash, Destination = stash });
            Expect(displayName, "swap without the warrior tag", swap.Reason, EquipFailureReason.RequirementNotMet);
            Expect(displayName, "strike kept", system.TryGetAbility(strike, out _), true);
        }
#endif

        private static void CheckPlayMode(string displayName)
        {
            EquipmentComponent equipment = Object.FindAnyObjectByType<EquipmentComponent>();
            if (equipment == null)
            {
                AddProblem(displayName, "no equipment component in Play Mode");
                return;
            }

            EquipmentController controller = equipment.Controller;
            if (displayName == "Equipment Basics")
            {
                CheckBasics(displayName, equipment);
            }
#if RAMIRESTECHGAMES_ABILITIES
            else
            {
                CheckAbilities(displayName, equipment);
            }
#endif

            Expect(displayName, "violations", controller.ViolationCount, 0L);
            Expect(displayName, "revert targets missing", controller.RevertTargetMissingCount, 0L);
            Debug.Log("[EquipmentSampleSmoke] " + displayName + ": " + controller.LastTransactionId +
                " transactions, last " + (controller.TransactionLogCount > 0
                    ? controller.GetTransactionRecord(0).Kind + " " + controller.GetTransactionRecord(0).Reason
                    : "none"));
        }

        private static void DeleteImport(string root)
        {
            AssetDatabase.DeleteAsset(root);
            string parent = Path.GetDirectoryName(root).Replace('\\', '/');
            while (parent.StartsWith("Assets/Samples", StringComparison.Ordinal) &&
                AssetDatabase.IsValidFolder(parent) &&
                AssetDatabase.FindAssets(string.Empty, new[] { parent }).Length == 0)
            {
                AssetDatabase.DeleteAsset(parent);
                parent = Path.GetDirectoryName(parent).Replace('\\', '/');
            }
        }

        // Each step after a domain reload recomputes its state: the enumerator resumes, its locals do not survive.
        private static IEnumerator Run(string displayName, string scene)
        {
            SessionState.EraseString(STATE_KEY + "Problems." + displayName);
            if (!Find(displayName).isImported)
            {
                SessionState.SetBool(STATE_KEY + "Imported." + displayName, true);
                Assert.That(Find(displayName).Import(Sample.ImportOptions.HideImportWindow), Is.True, "import");
                yield return new WaitForDomainReload();
            }

            string root = ToAssetPath(Find(displayName).importPath);
            int assets = CheckAssets(displayName, root);
            Assert.That(assets, Is.GreaterThan(10), "imported assets under " + root);
            EditorSceneManager.OpenScene(root + "/" + scene, OpenSceneMode.Single);
            CheckScene(displayName);
            EditorUtility.RequestScriptReload();
            yield return new WaitForDomainReload();

            CheckScene(displayName);
            SessionState.SetBool(STATE_KEY + "RunInBackground", Application.runInBackground);
            yield return new EnterPlayMode();

            Application.runInBackground = true;
            int start = Time.frameCount;
            double deadline = EditorApplication.timeSinceStartup + PLAY_TIMEOUT_SECONDS;
            while (Time.frameCount - start < FRAMES && EditorApplication.timeSinceStartup < deadline)
            {
                // An unfocused Editor does not run the player loop on its own (CLI runs): ask for each update.
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }

            CheckPlayMode(displayName);
            Application.runInBackground = SessionState.GetBool(STATE_KEY + "RunInBackground", false);
            yield return new ExitPlayMode();

            root = ToAssetPath(Find(displayName).importPath);
            if (SessionState.GetBool(STATE_KEY + "Imported." + displayName, false))
            {
                SessionState.EraseBool(STATE_KEY + "Imported." + displayName);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                DeleteImport(root);
                yield return new WaitForDomainReload();
            }

            string problems = Problems(displayName);
            SessionState.EraseString(STATE_KEY + "Problems." + displayName);
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void Checker_DetectsBrokenReferencesMissingLayoutsAndValidationErrors()
        {
            const string NAME = "Detection";
            SessionState.EraseString(STATE_KEY + "Problems." + NAME);
            EquipmentSetDefinition set = ScriptableObject.CreateInstance<EquipmentSetDefinition>();
            EquipmentDefinition definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            definition.name = "Broken";
            definition.SetSet(set);
            Object.DestroyImmediate(set);
            GameObject actor = new GameObject("Actor without layout");
            try
            {
                actor.AddComponent<EquipmentComponent>();
                CheckObject(NAME, definition, "Broken.asset");
                ValidationReport report = new ValidationReport();
                CheckGameObject(NAME, actor, report);
                ReportErrors(NAME, report);
                string problems = Problems(NAME);
                Assert.That(problems, Does.Contain("broken reference _set"));
                Assert.That(problems,
                    Does.Contain("validation error: Actor without layout: equipment has no slot layout"));
            }
            finally
            {
                SessionState.EraseString(STATE_KEY + "Problems." + NAME);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(definition);
            }
        }

        [UnityTest]
        [Timeout(TIMEOUT_MILLISECONDS)]
        public IEnumerator EquipmentBasics_ImportsLoadsAndRuns()
        {
            return Run("Equipment Basics", "Scenes/EquipmentBasics.unity");
        }

#if RAMIRESTECHGAMES_ABILITIES
        [UnityTest]
        [Timeout(TIMEOUT_MILLISECONDS)]
        public IEnumerator EquipmentAbilities_ImportsLoadsAndRuns()
        {
            return Run("Equipment Abilities", "Scenes/EquipmentAbilities.unity");
        }
#endif
    }
}
