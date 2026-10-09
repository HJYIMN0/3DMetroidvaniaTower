#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Animation;

namespace Game.EditorTools
{
    public class AnimationTunerWindow : EditorWindow
    {
        private AnimationTuner tuner;
        private float speed = 1f;
        private bool realTime;
        private float prepEnd = 0.25f;
        private float activeEnd = 0.55f;
        private string stateName = "Attack";

        [MenuItem("Game/Animation Tuner")]
        public static void Open() => GetWindow<AnimationTunerWindow>("Anim Tuner");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            tuner = (AnimationTuner)EditorGUILayout.ObjectField("Tuner", tuner, typeof(AnimationTuner), true);
            if (tuner == null) { EditorGUILayout.HelpBox("Assign an AnimationTuner from the scene.", MessageType.Info); return; }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Speed", EditorStyles.boldLabel);
            speed = EditorGUILayout.Slider("Animator.speed", speed, 0.05f, 4f);
            if (GUILayout.Button("Apply Speed")) tuner.SetGlobalSpeed(speed);

            realTime = EditorGUILayout.Toggle("Is Real Time", realTime);
            tuner.SetRealTime(realTime);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Phases", EditorStyles.boldLabel);
            stateName = EditorGUILayout.TextField("State Name", stateName);
            prepEnd = EditorGUILayout.Slider("Prep End (norm)", prepEnd, 0f, 1f);
            activeEnd = EditorGUILayout.Slider("Active End (norm)", activeEnd, 0f, 1f);
            if (GUILayout.Button("Apply Phases")) tuner.SetPhase(stateName, prepEnd, activeEnd);

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset Speed to 1")) { speed = 1f; tuner.SetGlobalSpeed(1f); }
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
#endif