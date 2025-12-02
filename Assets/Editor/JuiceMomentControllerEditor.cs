#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JuiceMomentController))]
public class JuiceMomentControllerEditor : Editor
{
    SerializedProperty defaultTimelineProp;

    private void OnEnable()
    {
        defaultTimelineProp = serializedObject.FindProperty("defaultTimeline");
    }

    public override void OnInspectorGUI()
    {
        // Draw all the normal fields first
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug / Testing", EditorStyles.boldLabel);

        // Info about play mode requirement
        EditorGUILayout.HelpBox(
            "You can trigger a test juice moment while the game is playing.\n" +
            "It will use the Default Timeline assigned above.",
            MessageType.Info);

        // Only allow the button while the game is running
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("▶ Test Juice Moment (Default Timeline)"))
            {
                var controller = (JuiceMomentController)target;

                if (controller == null)
                {
                    Debug.LogWarning("[JuiceMomentControllerEditor] No controller target.");
                    return;
                }

                // Use the serialized defaultTimeline property if assigned
                var timelineObj = defaultTimelineProp != null
                    ? defaultTimelineProp.objectReferenceValue
                    : null;

                var timeline = timelineObj as JuiceTimelineAsset;

                if (timeline == null)
                {
                    Debug.LogWarning("[JuiceMomentControllerEditor] No Default Timeline assigned. " +
                                     "Assign one in the inspector to test.");
                }

                controller.TriggerJuiceMoment(timeline);
            }
        }
    }
}
#endif