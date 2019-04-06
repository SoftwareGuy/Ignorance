// ----------------------------------------
// Ignorance Transport by Matt Coburn, 2018 - 2019
// This Transport uses other dependencies that you can
// find references to in the README.md of this package.
// ----------------------------------------
// Ignorance Transport is MIT Licensed. It would be however
// nice to get some acknowledgement in your program/game's credits
// that Ignorance was used to build your network code. It would be 
// greatly appreciated if you reported bugs and donated coffee
// at https://github.com/SoftwareGuy/Ignorance. Remember, OSS is the
// way of the future!
// ----------------------------------------
// This file is part of the Ignorance Transport.
// ----------------------------------------
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(IgnoranceTransport))]
    public class IgnoranceTransportInspector : Editor
    {
        bool showGeneralSettings = true;
        bool showServerSettings = true;
        // bool showExperimentalSettings = false;
        bool showTimeoutSettings = false;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox($"Thank you for using Ignorance Transport for Mirror!\nPlease report any bugs you encounter on the GitHub issues tracker.", MessageType.Info);

            // Channels
            ShowMeThatList(serializedObject.FindProperty("m_ChannelDefinitions"));

            // General Settings Foldout
            showGeneralSettings = EditorGUILayout.Foldout(showGeneralSettings, "General Settings");
            if (showGeneralSettings)
            {
                EditorGUI.indentLevel += 1;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TransportVerbosity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_UseLZ4Compression"));

                EditorGUI.indentLevel -= 1;
            }

            // Server Settings Foldout
            showServerSettings = EditorGUILayout.Foldout(showServerSettings, "Server Settings");
            if (showServerSettings)
            {
                EditorGUI.indentLevel += 1;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BindToAllInterfaces"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BindToAddress"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Port"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MaximumTotalConnections"));

                EditorGUI.indentLevel -= 1;
            }

            // Timeout settings
            showTimeoutSettings = EditorGUILayout.Foldout(showTimeoutSettings, "Timeout Settings");
            if (showTimeoutSettings)
            {
                EditorGUI.indentLevel += 1;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_UseCustomTimeout"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BasePeerTimeout"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BasePeerMultiplier"));

                EditorGUI.indentLevel -= 1;
            }

            // Experimental Settings
            /*
            showExperimentalSettings = EditorGUILayout.Foldout(showExperimentalSettings, "Experimental Settings");
            if (showExperimentalSettings)
            {
                EditorGUI.indentLevel += 1;

                EditorGUI.indentLevel -= 1;
            }
            */

            // Apply.
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Makes a EditorGUI List for a SerializedProperty.
        /// </summary>
        /// <param name="victim">The serializedproperty to use as the source.</param>
        public void ShowMeThatList(SerializedProperty victim)
        {
            EditorGUILayout.PropertyField(victim);
            EditorGUI.indentLevel += 1;
            if (victim.isExpanded)
            {
                if (victim.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Ignorance cannot operate without any channels! You MUST add at least one.", MessageType.Error);
                    if (GUILayout.Button("New Channel...")) victim.InsertArrayElementAtIndex(0);
                }
                else
                {
                    EditorGUILayout.HelpBox("It is recommended to leave the first two channels as Reliable and Unreliable, as this matches what Unity LLAPI was originally. " +
                        "You can add up to 255 channels in total.", MessageType.Info);
                    for (int i = 0; i < victim.arraySize; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"Channel {i}", GUILayout.Width(100f));
                        EditorGUILayout.PropertyField(victim.GetArrayElementAtIndex(i), GUIContent.none);
                        if (GUILayout.Button("+", GUILayout.Width(22f)))
                        {
                            // Cannot have more than 255 channels.
                            if (victim.arraySize < 256) victim.InsertArrayElementAtIndex(i);
                        }
                        if (GUILayout.Button("-", GUILayout.Width(22f))) victim.DeleteArrayElementAtIndex(i);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            EditorGUI.indentLevel -= 1;
        }

    }
}
#endif