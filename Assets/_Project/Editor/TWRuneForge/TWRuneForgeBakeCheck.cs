using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static IslandV2.TWRuneForgeModel;

namespace IslandV2
{
    internal static class TWRuneForgeBakeCheck
    {
        internal static void CheckRepeat()
        {
            var first = TWRuneForgeBaker.Bake();
            int rootId = first.gameObject.GetInstanceID();
            int buildId = first.build.GetInstanceID();
            int mapId = first.mapPoint.GetInstanceID();
            int objectCount = first.GetComponentsInChildren<Transform>(true).Length;
            int colliderCount = first.GetComponentsInChildren<Collider>(true).Length;
            Vector3 position = first.transform.position;
            Quaternion rotation = first.transform.rotation;
            string[] assets = AssetDatabase.FindAssets("", new[] { TWRuneForgeBaker.Folder }).OrderBy(guid => guid).ToArray();

            var second = TWRuneForgeBaker.Bake();
            var sceneForges = TWRuneForgeBaker.SceneObjects<RuneForge>(second.gameObject.scene);
            var newAssets = AssetDatabase.FindAssets("", new[] { TWRuneForgeBaker.Folder }).OrderBy(guid => guid);
            Require(sceneForges.Length == 1 && second.gameObject.GetInstanceID() == rootId
                && second.build.GetInstanceID() == buildId && second.mapPoint.GetInstanceID() == mapId,
                "Повторная сборка заменила сцену кузницы или игровую точку.");
            Require(assets.SequenceEqual(newAssets), "Повторная сборка создала новые ассеты вместо обновления существующих.");
            Require(position == second.transform.position && rotation == second.transform.rotation
                && objectCount == second.GetComponentsInChildren<Transform>(true).Length
                && colliderCount == second.GetComponentsInChildren<Collider>(true).Length,
                "Повторное запекание поменяло размещение или количество объектов.");
            Selection.activeGameObject = second.gameObject;
            Debug.Log($"TW06 QA: одна кузница, {objectCount} объектов, {colliderCount} коллайдеров. GUID ассетов, точка карты и размещение совпали после двух запеканий.", second);
        }

        internal static void Validate(RuneForge forge)
        {
            Require(forge != null && forge.isActiveAndEnabled && forge.hut != null && forge.workyard != null,
                "Кузница выключена или отсутствуют ссылки на хижину и рабочий двор.");
            ValidateBuildZone(forge);
            Require(forge.entrance != null && forge.interior != null && forge.oskarPoint != null && forge.mapPoint != null
                && forge.GetComponentsInChildren<RuneForgeInspection>(true).Length == 2, "Не хватает точек взаимодействия или маркера карты.");
            Require(forge.ruined != null && forge.repairFrame != null && forge.restored != null
                && forge.build.finishedBuilding == forge.restored, "Не хватает запечённых стадий.");
            foreach (var filter in forge.GetComponentsInChildren<MeshFilter>(true))
                Require(filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0 && AssetDatabase.Contains(filter.sharedMesh),
                    "Не сохранён меш " + filter.name);
            foreach (var renderer in forge.GetComponentsInChildren<Renderer>(true))
                Require(renderer.sharedMaterial != null && AssetDatabase.Contains(renderer.sharedMaterial),
                    "Не сохранён материал " + renderer.name);

            bool ruinedWasOn = forge.ruined.activeSelf;
            bool frameWasOn = forge.repairFrame.activeSelf;
            bool restoredWasOn = forge.restored.activeSelf;
            try
            {
                SetStage(forge, Phase.Ruined);
                Require(HasFloor(forge) && Blocked(forge, 0f, Front), "В руинах должны быть постоянный пол и перегороженный вход.");

                SetStage(forge, Phase.Frame);
                Require(HasFloor(forge) && Blocked(forge, 0f, Front), "Во время ремонта должен оставаться безопасный пол и закрытый вход.");

                SetStage(forge, Phase.Restored);
                Require(HasFloor(forge), "В готовой кузнице нет коллайдера пола.");
                for (float z = DeckFront + .18f; z <= -.34f; z += .28f)
                    Require(!Blocked(forge, 0f, z), "Проход между входом и мастерской перекрыт.");
                Require(Blocked(forge, Wall, 0f) && Blocked(forge, Workbench.x, Workbench.z)
                    && Blocked(forge, Anvil.x, Anvil.z) && Blocked(forge, Fire.x, 1.72f), "Не найдены коллайдеры стены или оборудования.");
                Require(forge.restored.GetComponentsInChildren<ParticleSystem>().Length == 2
                    && forge.restored.GetComponentsInChildren<Light>().Length == 2, "Не найдены угольки, дым или освещение.");
            }
            finally
            {
                forge.ruined.SetActive(ruinedWasOn);
                forge.repairFrame.SetActive(frameWasOn);
                forge.restored.SetActive(restoredWasOn);
                Physics.SyncTransforms();
            }
        }

        static void ValidateBuildZone(RuneForge forge)
        {
            var build = forge.build;
            Require(build != null && build.transform == forge.transform.Find(TWRuneForgeBaker.BuildZoneName),
                "Ссылка RuneForge.build должна вести в дочернюю зону BuildZone.");
            Require(build.SiteKind == BuildSiteKind.RuneForge, $"Тип BuildZone — {build.SiteKind}. Ожидался RuneForge.");
            Require(build.SaveId == RuneForge.SaveId, $"ID сохранения BuildZone — «{build.SaveId}». Ожидался «{RuneForge.SaveId}».");

            var cost = build.data;
            Require(cost != null && cost.StepCount == 1 && cost.steps[0] != null,
                "В BuildZone нужна смета RuneForge_Restoration с одним этапом ремонта.");
            var step = cost.steps[0];
            Require(step.woodCost == 0 && step.plankCost == TWRuneForgeBaker.Planks && step.coinCost == TWRuneForgeBaker.Coins,
                $"Стоимость ремонта — {step.woodCost} брёвен, {step.plankCost} досок, {step.coinCost} монет. Ожидались 0, {TWRuneForgeBaker.Planks}, {TWRuneForgeBaker.Coins}.");

            Require(build.isActiveAndEnabled && build.UseWindow, "В BuildZone выключен BuildingController или режим окна стройки.");
            var trigger = build.GetComponent<BoxCollider>();
            Require(trigger != null && trigger.enabled && trigger.isTrigger, "В BuildZone нужен активный BoxCollider с флагом Is Trigger.");
            var prompt = build.GetComponent<BuildSiteInteractable>();
            Require(prompt != null && prompt.enabled, "В BuildZone отсутствует активный BuildSiteInteractable для открытия окна.");
        }

        static void SetStage(RuneForge forge, Phase phase)
        {
            forge.ruined.SetActive(phase == Phase.Ruined);
            forge.repairFrame.SetActive(phase == Phase.Frame);
            forge.restored.SetActive(phase == Phase.Restored);
            Physics.SyncTransforms();
        }

        static bool HasFloor(RuneForge forge)
        {
            Vector3 point = forge.transform.TransformPoint(new Vector3(0f, Floor + .4f, -.9f));
            return Physics.RaycastAll(point, Vector3.down, .65f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(hit => hit.collider.transform.IsChildOf(forge.transform) && hit.normal.y > .8f);
        }

        static bool Blocked(RuneForge forge, float x, float z)
        {
            Vector3 lower = forge.transform.TransformPoint(new Vector3(x, Floor + .41f, z));
            Vector3 upper = forge.transform.TransformPoint(new Vector3(x, Floor + 1.49f, z));
            return Physics.OverlapCapsule(lower, upper, .31f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                .Any(collider => collider.transform.IsChildOf(forge.transform));
        }

        static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException("TW06 QA: " + error);
        }
    }
}
