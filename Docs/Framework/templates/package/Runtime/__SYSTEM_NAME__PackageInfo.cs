// Purpose: Exposes the package identity and the menu roots shared by every tool of this package.
namespace {{ASSEMBLY_ROOT}}
{
    /// <summary>
    /// Package identity and menu roots. Use these constants in <c>CreateAssetMenu</c>,
    /// <c>AddComponentMenu</c> and <c>MenuItem</c> paths so every tool of the package is grouped consistently.
    /// </summary>
    public static class {{SYSTEM_NAME}}PackageInfo
    {
        /// <summary>Unity Package Manager name of this package.</summary>
        public const string PACKAGE_NAME = "{{PACKAGE_ID}}";

        /// <summary>Human-readable system name used in menus.</summary>
        public const string DISPLAY_NAME = "{{DISPLAY_NAME}}";

        /// <summary>Root for <c>Assets/Create</c> menu entries.</summary>
        public const string CREATE_ASSET_MENU_ROOT = "RamiresTech Games/" + DISPLAY_NAME + "/";

        /// <summary>Root for <c>Add Component</c> menu entries.</summary>
        public const string COMPONENT_MENU_ROOT = "RamiresTech Games/" + DISPLAY_NAME + "/";

        /// <summary>Root for editor windows and tools.</summary>
        public const string TOOLS_MENU_ROOT = "Tools/RamiresTech Games/" + DISPLAY_NAME + "/";
    }
}
