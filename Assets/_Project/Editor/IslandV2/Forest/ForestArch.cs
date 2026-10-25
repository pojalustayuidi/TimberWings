#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestArch.cs (Editor)
// A5: арка в главный лес. Tools/BuilderSim/Лес/Красота леса/6. Арка в лес + зона «Старый лес» (приоритет 235). Безопасно жать повторно.
//
// Что ставит:
//   1. Арку Landmark_ForestArch у метки ForestGate (4, 34): два толстых столба на каменных основаниях, выгнутая балка,
//      гирлянда листвы сверху, вывеска с ёлочкой-эмблемой (читается как «лес»), два тёплых фонаря. Проём 5 м, высота ~5 м,
//      смотрит от лагеря в лес. Столбы сплошные (CapsuleCollider), проход свободный.
//   2. Зону MapZone «Старый лес»: круг вокруг пути ForestGate -> PAD_Forest -> Дедушка. Плашка при входе («ВХОД В ЛЕС»),
//      зелёная область на большой карте, подпись на мини-карте.
//   3. MapPoi kind=Forest на арке: значок входа в лес на большой карте (в HudMinimap kind «Forest» только на большой карте).
// Всё лежит в IslandV2/_Manual (ForestArch, ForestZone_Old), генератор блокаута и леса их не трогает.
// Лес вокруг арки: ForestDensityGen и мои «ForestBackdrop» уже оставляют круг и коридор у ForestGate; чаща начинается за аркой.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    public static class ForestArch
    {
        const string Dir = "Assets/_Project/Prefabs/Landmarks";
        const string PrefabPath = Dir + "/Landmark_ForestArch.prefab";

        const float Half = 2.7f;     // половина проёма до оси столба, м
        const float PostH = 4.7f;

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/6. Арка в лес + зона «Старый лес»", false, 235)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Арка: останови Play Mode."); return; }
            var gate = GameObject.Find("ForestGate");
            if (gate == null) { Debug.LogError("Арка: в сцене нет метки ForestGate (Остров v2 → 2. Собрать блокаут)."); return; }
            var prefab = BuildPrefab();
            if (prefab == null) return;

            var manualGo = GameObject.Find("IslandV2/_Manual");
            Transform parent = manualGo != null ? manualGo.transform : null;
            Remove(parent, "ForestArch"); Remove(parent, "ForestZone_Old");

            // ---- арка: лицом от лагеря в лес (метка смотрит на лагерь, значит разворот на 180)
            Vector3 gp = gate.transform.position;
            Vector3 toCamp = new Vector3(-gp.x, 0f, -gp.z);
            if (toCamp.sqrMagnitude < 0.01f) toCamp = Vector3.back;
            Quaternion rot = Quaternion.LookRotation(-toCamp.normalized, Vector3.up);
            var arch = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(arch, "Арка в лес");
            arch.name = "ForestArch";
            arch.transform.position = gp; arch.transform.rotation = rot;
            var poi = arch.GetComponent<MapPoi>(); if (poi == null) poi = arch.AddComponent<MapPoi>();
            poi.kind = "Forest"; poi.label = "Старый лес";

            // ---- зона: центр между аркой и Дедушкой, захватывает PAD_Forest
            Vector3 zc = gp;
            var gr = GameObject.Find("PAD_Grandpa"); var pf = GameObject.Find("PAD_Forest");
            if (gr != null && pf != null) zc = (gr.transform.position + pf.transform.position + gp) / 3f;
            else if (pf != null) zc = (pf.transform.position + gp) * 0.5f;
            var zone = new GameObject("ForestZone_Old");
            Undo.RegisterCreatedObjectUndo(zone, "Зона Старый лес");
            if (parent != null) zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(zc.x, 0f, zc.z);
            var mz = zone.AddComponent<MapZone>();
            mz.zoneName = "Старый лес";
            mz.caption = "ВХОД В ЛЕС";
            mz.description = "Главный лес острова: рабочие деревья на полянах, огромные старые деревья в чаще, в глубине растёт Дедушка.";
            mz.shape = MapZone.Shape.Circle;
            mz.radius = Mathf.Max(22f, Vector3.Distance(zc, gp) + 9f);
            mz.color = new Color(0.30f, 0.72f, 0.38f);
            mz.showOnMap = true; mz.announce = true; mz.priority = 5;

            EditorSceneManager.MarkSceneDirty(arch.scene);
            Selection.activeGameObject = arch;
            Debug.Log("Арка в лес: поставлена в (" + gp.x.ToString("0.#") + ", " + gp.z.ToString("0.#") + "), проём 5 м, смотрит в лес. Зона «Старый лес»: центр (" +
                      zc.x.ToString("0.#") + ", " + zc.z.ToString("0.#") + "), радиус " + mz.radius.ToString("0") + " м, плашка «ВХОД В ЛЕС», значок на большой карте.\n" +
                      "Если плашки или значка нет: в HudMinimap.kinds должен быть ключ «Forest» и стоять useZones/announceZones. Двигай арку и зону руками.");
        }

        static void Remove(Transform parent, string name)
        {
            GameObject go = parent != null ? (parent.Find(name) != null ? parent.Find(name).gameObject : null) : GameObject.Find(name);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }

        // ============================================================ меш
        // локально: ось X поперёк проёма, +Z вперёд (в лес), пол y=0
        static Mesh BuildMesh()
        {
            var b = new TreeMB();
            // столбы (подпись 0 дерево)
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * Half;
                b.Tube(0, new[] { new Vector3(x, -0.2f, 0f), new Vector3(x + s * 0.03f, PostH * 0.5f, 0f), new Vector3(x - s * 0.05f, PostH, 0f) },
                       new[] { 0.55f, 0.46f, 0.44f }, 8, true);
                // каменное основание (3)
                b.Blob(3, new Vector3(x, 0.30f, 0f), new Vector3(0.95f, 0.55f, 0.95f), 300 + s + 5, 0, 0.45f, -1);
                b.Blob(3, new Vector3(x + s * 0.8f, 0.14f, 0.35f), new Vector3(0.45f, 0.30f, 0.42f), 310 + s + 5, 0, 0.5f, -1);
                // лапа-раскос от столба к балке
                b.Tube(0, new[] { new Vector3(x, PostH - 1.1f, 0f), new Vector3(x - s * 0.7f, PostH - 0.35f, 0f), new Vector3(x - s * 1.2f, PostH + 0.1f, 0f) },
                       new[] { 0.20f, 0.18f, 0.16f }, 6, true);
            }
            // выгнутая главная балка
            var beam = new Vector3[7]; var br = new float[7];
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f, x = Mathf.Lerp(-Half - 0.9f, Half + 0.9f, t);
                float u = x / (Half + 0.9f);
                beam[i] = new Vector3(x, PostH + 0.15f + 0.55f * (1f - u * u), 0f);
                br[i] = 0.34f;
            }
            b.Tube(0, beam, br, 8, true);
            // вторая, верхняя балка короче (ступенька «крыши»)
            var top = new Vector3[5]; var tr = new float[5];
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f, x = Mathf.Lerp(-Half * 0.75f, Half * 0.75f, t);
                float u = x / (Half * 0.75f);
                top[i] = new Vector3(x, PostH + 0.95f + 0.45f * (1f - u * u), 0.05f);
                tr[i] = 0.22f;
            }
            b.Tube(0, top, tr, 6, true);

            // гирлянда листвы вдоль балки (1)
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f, x = Mathf.Lerp(-Half - 0.6f, Half + 0.6f, t);
                float u = x / (Half + 0.9f);
                float y = PostH + 0.55f + 0.55f * (1f - u * u) + 0.1f * Mathf.Sin(i * 2.3f);
                b.Blob(1, new Vector3(x, y, 0.05f * Mathf.Sin(i * 1.9f)), new Vector3(0.62f, 0.40f, 0.55f), 400 + i, 1, 0.3f, 2);
            }
            // свисающие плети по краям
            for (int s = -1; s <= 1; s += 2)
                b.Blob(1, new Vector3(s * (Half + 0.15f), PostH - 0.9f, 0.18f), new Vector3(0.30f, 0.65f, 0.30f), 450 + s + 5, 1, 0.3f, 2);

            // вывеска: на двух верёвках, доска, эмблема-ёлочка
            float sy = PostH - 1.25f;
            b.Tube(0, new[] { new Vector3(-0.85f, PostH + 0.3f, 0f), new Vector3(-0.85f, sy + 0.38f, 0f) }, new[] { 0.035f, 0.035f }, 4, false);
            b.Tube(0, new[] { new Vector3(0.85f, PostH + 0.3f, 0f), new Vector3(0.85f, sy + 0.38f, 0f) }, new[] { 0.035f, 0.035f }, 4, false);
            b.Tube(0, new[] { new Vector3(-1.15f, sy, 0f), new Vector3(1.15f, sy, 0f) }, new[] { 0.40f, 0.40f }, 4, true);
            b.Tier(new Vector3(0f, sy - 0.30f, 0.38f), 0.42f, 0.34f, 5, 0f, 0f, 601, false, false, 1, 1, 1);
            b.Tier(new Vector3(0f, sy - 0.06f, 0.38f), 0.34f, 0.32f, 5, 0f, 0f, 602, false, false, 1, 1, 1);
            b.Tier(new Vector3(0f, sy + 0.16f, 0.38f), 0.24f, 0.28f, 5, 0f, 0f, 603, false, false, 1, 1, 1);

            // фонари (2, тёплые)
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (Half - 0.95f);
                b.Tube(0, new[] { new Vector3(x, PostH + 0.55f, 0f), new Vector3(x, PostH - 0.35f, 0f) }, new[] { 0.03f, 0.03f }, 4, false);
                b.Blob(2, new Vector3(x, PostH - 0.62f, 0f), new Vector3(0.20f, 0.28f, 0.20f), 500 + s + 5, 0, 0.12f, -1);
            }
            return b.Build("ForestArch");
        }

        static GameObject BuildPrefab()
        {
            EditorAssets.EnsureFolder(Dir);
            var wood = TreeToonSetup.Library("Tree_Bark_Oak");
            if (wood == null) { Debug.LogError("Арка: нет шейдера TreeToon / материала коры (Лес → Tree Toon)."); return null; }
            var leaf = ForestMats.BushLime(false);
            var glow = ForestMats.FlowerYellow(false);
            var stone = ForestMats.Rock(false);
            if (leaf == null || glow == null || stone == null) { Debug.LogError("Арка: не создались материалы."); return null; }

            var mesh = ForestMats.SaveMesh(BuildMesh(), Dir + "/ForestArch.asset");
            var root = new GameObject("Landmark_ForestArch");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = root.AddComponent<MeshRenderer>();
                var all = new[] { wood, leaf, glow, stone };
                int subs = Mathf.Max(1, mesh.subMeshCount);
                var arr = new Material[subs];
                for (int i = 0; i < subs; i++) arr[i] = all[Mathf.Min(i, all.Length - 1)];
                mr.sharedMaterials = arr;
                mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;

                for (int s = -1; s <= 1; s += 2)
                {
                    var cap = root.AddComponent<CapsuleCollider>();
                    cap.direction = 1; cap.radius = 0.55f; cap.height = PostH; cap.center = new Vector3(s * Half, PostH * 0.5f, 0f);
                }
                if (root.GetComponent<MapPoi>() == null) { var p = root.AddComponent<MapPoi>(); p.kind = "Forest"; p.label = "Старый лес"; }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
