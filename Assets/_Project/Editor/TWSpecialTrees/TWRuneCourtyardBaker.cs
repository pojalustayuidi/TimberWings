using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static IslandV2.TWRuneSoilArt;

namespace IslandV2
{
    internal static partial class TWSpecialTreeBaker
    {
        const string FloorName = "Ground_RuneCourtyard";
        const string RampName = "Ground_RuneApproach";

        static void BakeCourtyard(Transform courtyard, Transform runeGroup, Transform oldParent, RuneForge forge,
            TWSpecialTreePlacement.GroveSite site, TWSpecialTreePlacement placement, string folder,
            Material surface, Material glow, Material motes)
        {
            Quaternion inverse = Quaternion.Inverse(site.Pose.rotation);
            Vector3[] trace = TWSpecialTreePlacement.PathTo(forge, site)
                .Select(point => inverse * (point - site.Pose.position)).ToArray();
            Vector3 Snap(Vector3 local, float offset) => placement.Snap(site.Pose, local, offset);

            MoveGeneratedChild(oldParent, courtyard, "Podzol");
            MoveGeneratedChild(oldParent, courtyard, "PathAndStones");
            MoveGeneratedChild(runeGroup, courtyard, "SoilVeins");
            Mesh floor = SaveMesh(Foundation(), folder, "RuneSoil");
            Mesh ramp = SaveMesh(Ramp(site.Toe, site.Side), folder, "RuneGroveRamp");
            SceneMesh(courtyard, "Podzol", floor, surface);
            SceneMesh(courtyard, RampName, ramp, surface);
            SceneMesh(courtyard, "StoneAndBeds", SaveMesh(Courtyard(site.Side), folder, "RuneGroveStonework"), surface);
            SceneMesh(courtyard, "PathAndStones", SaveMesh(Trail(Snap, trace), folder, "RuneGrovePath"), surface);
            SceneMesh(courtyard, "SoilVeins", SaveMesh(Veins(site.Side), folder, "RuneSoilVeins"), glow);
            GroundCollider(SceneChild(courtyard, FloorName), floor);
            GroundCollider(SceneChild(courtyard, RampName), ramp);

            Vector3[] gate = GatePosts(site.Side);
            for (int i = 0; i < gate.Length; i++)
                PlinthCollider(courtyard, "GatePedestal_" + (i + 1), gate[i], .52f, .56f);
            Vector3 beacon = Beacon(site.Side);
            PlinthCollider(courtyard, "BeaconPedestal", beacon, .70f, .88f);

            Transform lights = SceneChild(courtyard, "RuneLight");
            Color jade = new Color(.52f, .92f, .76f);
            Color lilac = new Color(.63f, .58f, .90f);
            SoftLight(lights, "EntranceLight", EntryLip(site.Side) + new Vector3(site.Side * .46f, .95f, 0f),
                jade, .72f, 3.9f);
            SoftLight(lights, "BeaconLight", beacon + Vector3.up * 1.21f, lilac, .61f, 4.2f);
            LowMotes(lights, "BedMotes_01", TreeRoot(site.Side, 0) + Vector3.up * .67f, motes, jade);
            LowMotes(lights, "BedMotes_02", TreeRoot(site.Side, 1) + Vector3.up * .67f, motes, lilac);
        }

        static Transform MoveGeneratedChild(Transform original, Transform parent, string name)
        {
            Transform legacy = original.Find(name);
            Transform current = parent.Find(name);
            Require(legacy == null || current == null, "В роще две запечённые детали " + name + ".");
            if (legacy == null) return current != null ? current : SceneChild(parent, name);

            Undo.SetTransformParent(legacy, parent, EditName);
            Undo.RecordObject(legacy, EditName);
            legacy.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            legacy.localScale = Vector3.one;
            return legacy;
        }

        static void GroundCollider(Transform host, Mesh mesh)
        {
            var collider = SceneComponent<MeshCollider>(host);
            // Re-cook an existing ramp when the ground sampler changes its pitch.
            collider.sharedMesh = null;
            collider.convex = false;
            collider.isTrigger = false;
            collider.sharedMesh = mesh;
        }

        static void PlinthCollider(Transform parent, string name, Vector3 basePoint, float width, float height)
        {
            var collider = SceneComponent<BoxCollider>(SceneChild(parent, name));
            collider.center = basePoint + Vector3.up * (.5f * height);
            collider.size = new Vector3(width, height, width);
            collider.isTrigger = false;
        }

        static void SoftLight(Transform root, string name, Vector3 at, Color tint, float intensity, float range)
        {
            var child = SceneChild(root, name);
            Undo.RecordObject(child, EditName);
            child.localPosition = at;
            var light = SceneComponent<Light>(child);
            light.type = LightType.Point;
            light.color = tint;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static void LowMotes(Transform root, string name, Vector3 at, Material material, Color tint)
        {
            var child = SceneChild(root, name);
            Undo.RecordObject(child, EditName);
            child.localPosition = at;
            tint.a = .62f;
            var system = SceneComponent<ParticleSystem>(child);
            ConfigureMotes(system, material, .62f, tint);
            var main = system.main;
            main.maxParticles = 8;
            var emission = system.emission;
            emission.rateOverTime = 2.6f;
        }

        static void ValidateCourtyard(SpecialTreeGroves binding, TWSpecialTreePlacement.GroveSite site)
        {
            Transform court = binding.courtyard.transform;
            Require(!binding.courtyard.activeSelf && binding.runeTrees.transform.parent == court
                && binding.gameObject.activeInHierarchy && binding.transform.Find("Podzol") == null
                && binding.transform.Find("PathAndStones") == null,
                "Площадка должна быть скрыта до готовности кузницы. Проверь иерархию Courtyard.");

            var floor = court.Find(FloorName)?.GetComponent<MeshCollider>();
            var ramp = court.Find(RampName)?.GetComponent<MeshCollider>();
            Require(floor != null && ramp != null && AssetDatabase.Contains(floor.sharedMesh)
                && AssetDatabase.Contains(ramp.sharedMesh) && !floor.gameObject.activeInHierarchy,
                "Не запечены коллизия террасы и пологий вход.");

            binding.courtyard.SetActive(true);
            try
            {
                Physics.SyncTransforms();
                foreach (float x in new[] { -2.6f, 0f, 2.6f })
                foreach (float z in new[] { -3f, 0f, 3f })
                    Require(WalkableAt(floor, court.TransformPoint(new Vector3(x, 0f, z)), .99f),
                        "Верх рунной террасы должен быть горизонтальным и сплошным.");
                Vector3 lip = EntryLip(site.Side);
                foreach (float t in new[] { .04f, .5f, .96f })
                foreach (float z in new[] { -.69f, 0f, .69f })
                {
                    Vector3 sample = Vector3.Lerp(site.Toe, lip, t) + Vector3.forward * z;
                    Require(WalkableAt(ramp, court.TransformPoint(sample), .9f), "Неровный проход на площадку.");
                }
                foreach (var tree in binding.runeTrees.GetComponentsInChildren<SpecialHarvestTree>())
                    Require(WalkableAt(floor, tree.transform.position, .99f), "У рунного дерева нет опоры на террасе.");
                Require(court.GetComponentsInChildren<Light>().Length == 2 &&
                    court.GetComponentsInChildren<ParticleSystem>().Length == 4,
                    "Проверь два источника света и эффекты у грядок.");
            }
            finally
            {
                binding.courtyard.SetActive(false);
                Physics.SyncTransforms();
            }
        }

        static bool WalkableAt(Collider collider, Vector3 position, float minNormal)
        {
            var ray = new Ray(position + Vector3.up * .45f, Vector3.down);
            return collider.Raycast(ray, out var hit, .56f) && hit.normal.y >= minNormal
                && Mathf.Abs(position.y - hit.point.y) < .025f;
        }

        static void ToggleCourtyardPreview()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling
                && PrefabStageUtility.GetCurrentPrefabStage() == null, "Предпросмотр доступен в Edit Mode.");
            var grove = TWRuneForgeBaker.SceneObjects<SpecialTreeGroves>(SceneManager.GetActiveScene()).SingleOrDefault();
            Require(grove != null && grove.courtyard != null && grove.forge != null, "Сначала запеки обновлённую площадку.");
            SetCourtyardPreview(grove, !grove.courtyard.activeSelf);
            EditorSceneManager.MarkSceneDirty(grove.gameObject.scene);
            Selection.activeGameObject = grove.gameObject;
            SceneView.RepaintAll();
        }

        static void SetCourtyardPreview(SpecialTreeGroves grove, bool show)
        {
            Set(grove.forge.ruined, !show);
            Set(grove.forge.repairFrame, false);
            Set(grove.forge.restored, show);
            Set(grove.courtyard, show);

            void Set(GameObject part, bool active)
            {
                if (part.activeSelf == active) return;
                Undo.RecordObject(part, EditName);
                part.SetActive(active);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            }
        }
    }
}
