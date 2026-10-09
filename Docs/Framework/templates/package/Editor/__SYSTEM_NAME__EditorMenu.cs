// Purpose: Adds editor menu entries that give quick access to the package documentation.
using UnityEditor;
using UnityEngine;

namespace {{ASSEMBLY_ROOT}}.Editor
{
    internal static class {{SYSTEM_NAME}}EditorMenu
    {
        #region Constants
        private const string README_PATH = "Packages/" + {{SYSTEM_NAME}}PackageInfo.PACKAGE_NAME + "/README.md";
        private const string DOCUMENTATION_MENU_PATH = {{SYSTEM_NAME}}PackageInfo.TOOLS_MENU_ROOT + "Documentation";
        private const string LOG_PREFIX = "[" + {{SYSTEM_NAME}}PackageInfo.DISPLAY_NAME + "] ";
        #endregion

        #region Private/Protected Methods
        [MenuItem(DOCUMENTATION_MENU_PATH)]
        private static void OpenDocumentation()
        {
            TextAsset readme = AssetDatabase.LoadAssetAtPath<TextAsset>(README_PATH);
            if (readme == null)
            {
                Debug.LogError(LOG_PREFIX + "README not found at " + README_PATH);
                return;
            }

            AssetDatabase.OpenAsset(readme);
        }
        #endregion
    }
}
