#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Mirror
{
    /// <summary>
    /// Adds the given define symbols to PlayerSettings define symbols.
    /// Just add your own define symbols to the Symbols property at the below.
    /// </summary>
    [InitializeOnLoad]
    public class AddIgnoranceDefine : Editor
    {

        /// <summary>
        /// Symbols that will be added to the editor
        /// </summary>
        public static readonly string[] Symbols = new string[] {
            "IGNORANCE", // Ignorance exists
            "IGNORANCE_1", // Major version
            "IGNORANCE_1_2" // Major and minor version
        };

        /// <summary>
        /// Add define symbols as soon as Unity gets done compiling.
        /// </summary>
        static AddIgnoranceDefine()
        {
            // Get the current scripting defines
            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            // Convert the string to a list
            List<string> allDefines = definesString.Split(';').ToList();
            // Remove any old version defines from previous installs
            allDefines.RemoveAll(x => x.StartsWith("IGNORANCE"));
            // Add any symbols that weren't already in the list
            allDefines.AddRange(Symbols.Except(allDefines));
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup,
                string.Join(";", allDefines.ToArray())
            );
        }

    }
}
#endif