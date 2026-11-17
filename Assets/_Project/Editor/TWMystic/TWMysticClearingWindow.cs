using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    internal sealed class TWMysticClearingWindow : EditorWindow
    {
        TWMysticClearingPolish.Targets targets;
        Vector2 scroll;
        string error, applyError;
        bool refresh = true;
        bool applyQueued;

        [MenuItem("Tools/BuilderSim/Лес/MYSTIC FOREST/10. Круг молчания — полировка", false, 359)]
        static void Open()
        {
            var window = GetWindow<TWMysticClearingWindow>();
            window.titleContent = new GUIContent("Круг молчания");
            window.minSize = new Vector2(350f, 400f);
            window.Show();
        }

        void OnEnable()
        {
            refresh = true;
            SceneView.duringSceneGui += DrawInkFrame;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawInkFrame;
            EditorApplication.delayCall -= ApplyToScene;
            applyQueued = false;
        }

        void OnHierarchyChange()
        {
            refresh = true;
            Repaint();
        }

        void Refresh()
        {
            refresh = false;
            error = null;
            targets = null;
            try { targets = TWMysticClearingPolish.FindTargets(); }
            catch (InvalidOperationException exception) { error = exception.Message; }
        }

        void ApplyToScene()
        {
            applyQueued = false;
            applyError = null;
            try
            {
                TWMysticClearingPolish.Apply();
            }
            catch (Exception exception)
            {
                applyError = exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                refresh = true;
                Repaint();
            }
        }

        void OnGUI()
        {
            if (refresh && Event.current.type == EventType.Layout) Refresh();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || applyQueued))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Применить к открытой сцене", GUILayout.Height(30f)))
                    {
                        applyQueued = true;
                        EditorApplication.delayCall += ApplyToScene;
                    }
                    if (GUILayout.Button("Обновить", GUILayout.Width(82f), GUILayout.Height(30f)))
                    {
                        refresh = true;
                        Repaint();
                    }
                }
                if (applyError != null)
                    EditorGUILayout.HelpBox(applyError, MessageType.Error);
                if (targets == null)
                {
                    EditorGUILayout.HelpBox(error ?? "Открой игровой уровень.", MessageType.Info);
                    return;
                }
                DrawObjects();
            }
        }

        void DrawObjects()
        {
            using var list = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = list.scrollPosition;
            for (int i = 0; i < targets.Benches.Length; i++)
                Row(i == 6 ? "Тайная скамья" : "Скамья " + (i + 1), targets.Benches[i], null);
            EditorGUILayout.Space(6f);

            var board = targets.Board.Find(TWMysticClearingPolish.BoardVisual);
            Row("Старая дощечка", targets.Board, view => Position(view, board != null ? board : targets.Board,
                targets.Board.forward + Vector3.up * 1.36f, .65f));
            if (board != null)
                Row("    Модель дощечки", board, null);
            var sign = targets.Signs[0];
            Row("Знак Гуннара", sign, view => Position(view, sign,
                Vector3.ProjectOnPlane(sign.forward, Vector3.up).normalized + Vector3.up * .56f, .78f));
            Row("Камень с надписью", targets.Witness, view => Position(view, targets.Slab,
                Vector3.ProjectOnPlane(-targets.Slab.forward, Vector3.up).normalized + Vector3.up * 1.08f, 1.06f));
            Row("Большой кедр", targets.Cedar, null);
            var ground = targets.Cedar.Find(TWMysticClearingPolish.CedarGround);
            if (ground != null)
            {
                Row("    Подстилка у корней", ground, view => Position(view, ground, new Vector3(1f, 1.3f, 2f), 4.1f));
                for (int i = 0; i < ground.childCount; i++)
                {
                    var piece = ground.GetChild(i);
                    Row("    " + piece.name, piece, null);
                }
            }

            var ribbon = targets.Ribbon;
            var focus = targets.RibbonInspection != null && targets.RibbonInspection.detail != null
                ? targets.RibbonInspection.detail : ribbon;
            Row("Оборванная лента", ribbon, view => Position(view, focus,
                focus.forward + focus.right * .86f + Vector3.up * .23f, .50f));
            var frame = ribbon.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == TWMysticRibbonInk.FrameName);
            if (frame != null)
            {
                Row("    Рамка зарубок", frame, view => Position(view, frame, frame.forward, .45f));
                if (GUILayout.Button("Обновить рисунок после движения рамки") && !TWMysticRibbonInk.UpdatePlacement(frame))
                    Debug.LogWarning("Круг молчания: сначала примени полировку к сцене.", frame);
            }
        }

        static void Row(string title, Transform item, Action<SceneView> shot)
        {
            if (item == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title);
                if (GUILayout.Button("Выбрать", GUILayout.Width(74f)))
                {
                    Selection.activeTransform = item;
                    EditorGUIUtility.PingObject(item.gameObject);
                }
                if (shot != null && GUILayout.Button("Кадр", GUILayout.Width(54f)))
                {
                    var view = SceneView.lastActiveSceneView;
                    if (view == null) view = GetWindow<SceneView>();
                    shot(view);
                    view.Focus();
                    view.Repaint();
                }
            }
        }

        static void Position(SceneView view, Transform item, Vector3 direction, float size)
        {
            Vector3 focus = item.TryGetComponent<MeshRenderer>(out var renderer) ? renderer.bounds.center : item.position;
            view.LookAt(focus, Quaternion.LookRotation(-direction.normalized), size, false, false);
        }

        static void DrawInkFrame(SceneView view)
        {
            var frame = Selection.activeTransform;
            if (frame == null || frame.name != TWMysticRibbonInk.FrameName) return;
            using (new Handles.DrawingScope(new Color(.91f, .70f, .43f, .96f), frame.localToWorldMatrix))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one * 2f);
                Handles.DrawLine(Vector3.zero, Vector3.forward * 1.6f);
            }
        }
    }
}
