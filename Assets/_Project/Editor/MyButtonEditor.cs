using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(MyButton))]
public sealed class MyButtonEditor : ButtonEditor
{
    private SerializedProperty hoverSeClip;
    private SerializedProperty selectSeClip;
    private SerializedProperty clickSeClip;

    protected override void OnEnable()
    {
        base.OnEnable();

        hoverSeClip = serializedObject.FindProperty("hoverSeClip");
        selectSeClip = serializedObject.FindProperty("selectSeClip");
        clickSeClip = serializedObject.FindProperty("clickSeClip");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SE", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hoverSeClip);
        EditorGUILayout.PropertyField(selectSeClip);
        EditorGUILayout.PropertyField(clickSeClip);
        serializedObject.ApplyModifiedProperties();
    }
}