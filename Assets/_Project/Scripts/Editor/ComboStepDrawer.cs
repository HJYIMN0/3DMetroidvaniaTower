#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Combat;

namespace Game.EditorTools
{
    [CustomPropertyDrawer(typeof(ComboStep))]
    public class ComboStepDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 8f + 6f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            Rect r = new Rect(position.x, position.y, position.width, line);

            EditorGUI.PropertyField(r, property.FindPropertyRelative("label")); r.y += line;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("requiredInput")); r.y += line;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("animatorStateName")); r.y += line;

            var s = property.FindPropertyRelative("inputWindowStart");
            var e = property.FindPropertyRelative("inputWindowEnd");
            EditorGUI.PropertyField(r, s); r.y += line;
            EditorGUI.PropertyField(r, e); r.y += line;

            EditorGUI.PropertyField(r, property.FindPropertyRelative("damageMultiplier")); r.y += line;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("allowCancelIntoDodge")); r.y += line;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("allowCancelIntoParry")); r.y += line;

            EditorGUI.EndProperty();
        }
    }
}
#endif