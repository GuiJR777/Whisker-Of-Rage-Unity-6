// Purpose: Smoke tests of the AI samples: Package Manager import, asset references, saved scenes and Play Mode.
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

using RamiresTechGames.AI.Components;
using RamiresTechGames.Core.Validation;

using Object = UnityEngine.Object;

namespace RamiresTechGames.Sandbox.Tests.Samples.AI
{
    /// <summary>
    /// Imports each AI sample with the Package Manager (when it is not imported yet), checks every asset and the saved
    /// scene (references, managed reference types, missing scripts, validation errors), forces a domain reload with the
    /// scene open — the reload that once resurrected an empty registry in a scene-authored <see cref="AIWorld"/> —,
    /// then enters Play Mode and runs fixed steps checking registration, capacity and evaluation. Only what the test
    /// imported is deleted. Gameplay itself is not automated (the owner's manual validation stays).
    /// </summary>
    internal sealed class AISampleSmokeTests
    {
        private const string PACKAGE = "com.ramirestechgames.ai";
        private const string STATE_KEY = "RamiresTechGames.Sandbox.AISampleSmoke.";
        private const int STEPS = 150;
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

        private static void CheckObject(string displayName, Object target, string label)
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

        private static void CheckGameObject(string displayName, GameObject gameObject, ValidationReport report)
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

        private static void ReportErrors(string displayName, ValidationReport report)
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

        private static int CountPerceivables()
        {
            return Object.FindObjectsByType<AIPerceivable>(FindObjectsInactive.Exclude).Length;
        }

        private static void CheckScene(string displayName, int agents, int perceivables)
        {
            ValidationReport report = new ValidationReport();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                CheckGameObject(displayName, root, report);
            }

            ReportErrors(displayName, report);
            AIWorld[] worlds = Object.FindObjectsByType<AIWorld>(FindObjectsInactive.Include);
            if (worlds.Length != 1)
            {
                AddProblem(displayName, "scene has " + worlds.Length + " AI worlds");
            }
            else if (worlds[0].RegistryCapacity < perceivables)
            {
                AddProblem(displayName, "registry capacity " + worlds[0].RegistryCapacity + " < " + perceivables);
            }

            int sceneAgents = Object.FindObjectsByType<AIAgent>(FindObjectsInactive.Exclude).Length;
            if (sceneAgents != agents || CountPerceivables() != perceivables)
            {
                AddProblem(displayName, "scene has " + sceneAgents + " agents and " + CountPerceivables() +
                    " perceivables, expected " + agents + " and " + perceivables);
            }
        }

        private static void CheckPlayMode(string displayName, int agents, int perceivables, int controlled)
        {
            AIWorld world = Object.FindAnyObjectByType<AIWorld>();
            if (world == null)
            {
                AddProblem(displayName, "no AI world in Play Mode");
                return;
            }

            StringBuilder state = new StringBuilder();
            state.Append(displayName).Append(": steps ").Append(world.StepIndex).Append(", registered ")
                .Append(world.RegisteredCount).Append('/').Append(world.RegistryCapacity).Append(", agents ")
                .Append(world.Agents.Count);
            if (world.StepIndex < STEPS)
            {
                AddProblem(displayName, "the world ran only " + world.StepIndex + " steps");
            }

            if (world.RegisteredCount != perceivables || world.RegistrySaturationCount != 0 ||
                world.DuplicateIdCount != 0 || world.InvalidIdCount != 0)
            {
                AddProblem(displayName, "registration: " + world.RegisteredCount + " of " + perceivables +
                    ", saturation " + world.RegistrySaturationCount + ", duplicates " + world.DuplicateIdCount +
                    ", invalid " + world.InvalidIdCount);
            }

            foreach (AIPerceivable perceivable in Object.FindObjectsByType<AIPerceivable>(FindObjectsInactive.Exclude))
            {
                if (!perceivable.IsRegistered || perceivable.World != world)
                {
                    AddProblem(displayName, perceivable.name + " is not registered in the world");
                }
            }

            int evaluating = 0;
            foreach (AIAgent agent in world.Agents)
            {
                state.Append(" | ").Append(agent.name).Append(agent.HasControl ? " AI " : " source ")
                    .Append(agent.EvaluationCount);
                if (agent.HasControl)
                {
                    evaluating += agent.EvaluationCount > 0 ? 1 : 0;
                }
            }

            if (world.Agents.Count != agents || evaluating != controlled)
            {
                AddProblem(displayName, "agents " + world.Agents.Count + "/" + agents + ", evaluating with control " +
                    evaluating + "/" + controlled);
            }

            Debug.Log("[AISampleSmoke] " + state);
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
        private static IEnumerator Run(string displayName, string scene, int agents, int perceivables, int controlled)
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
            CheckScene(displayName, agents, perceivables);
            EditorUtility.RequestScriptReload();
            yield return new WaitForDomainReload();

            CheckScene(displayName, agents, perceivables);
            SessionState.SetBool(STATE_KEY + "RunInBackground", Application.runInBackground);
            yield return new EnterPlayMode();

            Application.runInBackground = true;
            AIWorld world = Object.FindAnyObjectByType<AIWorld>();
            double deadline = EditorApplication.timeSinceStartup + PLAY_TIMEOUT_SECONDS;
            while (world != null && world.StepIndex < STEPS && EditorApplication.timeSinceStartup < deadline)
            {
                // An unfocused Editor does not run the player loop on its own (CLI runs): ask for each update.
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }

            CheckPlayMode(displayName, agents, perceivables, controlled);
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

            string problems = SessionState.GetString(STATE_KEY + "Problems." + displayName, string.Empty);
            SessionState.EraseString(STATE_KEY + "Problems." + displayName);
            Assert.That(problems, Is.Empty);
        }

#if RAMIRESTECHGAMES_CHARACTER
        [UnityTest]
        [Timeout(TIMEOUT_MILLISECONDS)]
        public IEnumerator AIBasics_ImportsLoadsAndRuns()
        {
            return Run("AI Basics", "Scenes/AIBasics.unity", 2, 3, 2);
        }
#endif

#if RAMIRESTECHGAMES_CHARACTER && RAMIRESTECHGAMES_COMBAT && RAMIRESTECHGAMES_ABILITIES
#if RAMIRESTECHGAMES_HFSM && RAMIRESTECHGAMES_STATS
        [UnityTest]
        [Timeout(TIMEOUT_MILLISECONDS)]
        public IEnumerator AICombatArena_ImportsLoadsAndRuns()
        {
            return Run("AI Combat Arena", "Scenes/AICombatArena.unity", 6, 6, 5);
        }
#endif
#endif
    }
}
