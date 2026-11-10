using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IslandV2
{
    internal static class TWCenterValidation
    {
        static readonly Regex InstanceRef = new Regex("\\\"instanceID\\\"\\s*:\\s*(-?\\d+)");

        static string StableKey(Object obj)
        {
            if (obj == null) return "null";
            Transform transform = obj is GameObject go ? go.transform : (obj is Component component ? component.transform : null);
            if (transform != null && transform.gameObject.scene.IsValid())
            {
                string path = "";
                for (var t = transform; t != null; t = t.parent) path = "/" + t.name + "[" + t.GetSiblingIndex() + "]" + path;
                string type = obj is Component c ? ":" + c.GetType().FullName + ":" + Array.IndexOf(c.GetComponents<Component>(), c) : "";
                return transform.gameObject.scene.path + path + type;
            }
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long local)) return guid + ":" + local;
            return obj.GetType().FullName + ":" + obj.name;
        }

        internal static bool IsHearthVisual(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name.StartsWith("TW_HEARTH_VISUAL_", StringComparison.Ordinal)) return true;
            return false;
        }

        internal static string Canonical(Object target, Dictionary<int, string> references)
        {
            return InstanceRef.Replace(EditorJsonUtility.ToJson(target), match =>
            {
                int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (!references.TryGetValue(id, out string key))
                {
                    key = StableKey(id != 0 ? EditorUtility.InstanceIDToObject(id) : null); references.Add(id, key);
                }
                return "\"stableRef\":\"" + key.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            });
        }

        public static string FireHash(Transform root)
        {
            var references = new Dictionary<int, string>();
            var text = new StringBuilder();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                text.Append(Canonical(t.gameObject, references)).Append('\n');
                foreach (var component in t.GetComponents<Component>())
                    if (component != null) text.Append(component.GetType().FullName).Append(':').Append(Canonical(component, references)).Append('\n');
            }
            // Construction costs/durations may live in a separate data asset.
            foreach (var data in root.GetComponentsInChildren<BuildingController>(true).Select(b => b.data).Where(d => d != null).Distinct())
                text.Append(Canonical(data, references)).Append('\n');
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }

        public static string LogicHash(Transform fire)
        {
            var references = new Dictionary<int, string>(); var text = new StringBuilder();
            text.Append(fire.name).Append(fire.localToWorldMatrix.ToString("R")).Append(StableKey(fire.parent));
            foreach (var behaviour in fire.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && !IsHearthVisual(behaviour.transform))
                    text.Append(StableKey(behaviour)).Append(Canonical(behaviour, references));
            var point = fire.Find("BuildPoint");
            if (point != null) text.Append(point.localToWorldMatrix.ToString("R"));
            foreach (var controller in fire.GetComponentsInChildren<BuildingController>(true))
            {
                if (controller.data != null) text.Append(Canonical(controller.data, references));
                foreach (var collider in controller.GetComponents<Collider>()) text.Append(Canonical(collider, references));
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }

        public static void Mesh(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount < 3) throw new InvalidOperationException("Пустой меш окружения центра.");
            var v = mesh.vertices; var t = mesh.triangles;
            if (t.Length == 0 || t.Length % 3 != 0) throw new InvalidOperationException("Нет треугольников: " + mesh.name);
            foreach (Vector3 p in v)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new InvalidOperationException("NaN/Infinity: " + mesh.name);
            for (int i = 0; i < t.Length; i += 3)
            {
                if (t[i] < 0 || t[i + 1] < 0 || t[i + 2] < 0 || t[i] >= v.Length || t[i + 1] >= v.Length || t[i + 2] >= v.Length)
                    throw new InvalidOperationException("Неверные индексы: " + mesh.name);
                if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).sqrMagnitude < 1e-12f)
                    throw new InvalidOperationException("Вырожденная грань: " + mesh.name);
            }
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public static void Installed(TWCenterContext x, CenterArtState state)
        {
            if (state.revision != TWCenterArt.Revision || !state.gameObject.activeInHierarchy) throw new InvalidOperationException("Неверная ревизия / комплект выключен.");
            if (state.campfire == null || FireHash(state.campfire) != state.campfireHash)
                throw new InvalidOperationException("Костёр или его данные изменились после сборки. Сравни с резервной сценой; генератор окружения их не перестраивает.");
            if (state.marker == null || Vector3.Distance(state.marker.position, state.markerPosition) > 0.0001f)
                throw new InvalidOperationException("Метка CAMPFIRE_Point смещена.");
            foreach (var item in state.hidden)
                if (item.target != null && item.target.activeSelf) throw new InvalidOperationException("Старая площадка включена: " + item.target.name);
            var ground = state.transform.Find("Organic_Earth_Clearing");
            if (ground == null) throw new InvalidOperationException("Нет земляной поляны.");
            var material = ground.GetComponent<MeshRenderer>().sharedMaterial;
            if (material == null || !material.HasProperty("_GroundTex") || material.GetTexture("_GroundTex") == null)
                throw new InvalidOperationException("Потерян исходный материал земли.");
            var groundMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            if (groundMesh.colors.Length != groundMesh.vertexCount || !groundMesh.colors.Any(c => c.a < 0.05f))
                throw new InvalidOperationException("Нет мягкого перехода грунта.");
            foreach (var filter in state.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh(filter.sharedMesh);
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount || renderer.sharedMaterials.Any(m => m == null))
                    throw new InvalidOperationException("Не все части имеют материалы: " + filter.name);
            }
            Physics.SyncTransforms();
            foreach (var collider in state.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger) throw new InvalidOperationException("Декор не должен добавлять игровые триггеры.");
                bool blocked;
                if (collider is BoxCollider box)
                {
                    Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
                    blocked = x.ProtectedPose(box.transform.TransformPoint(box.center), box.transform.rotation, new Vector2(size.x, size.z));
                }
                else blocked = x.Protected(collider.bounds);
                if (blocked) throw new InvalidOperationException("Коллайдер декора пересекает зарезервированный подход или здание: " + collider.name);
            }
            if (state.GetComponentsInChildren<BuildingController>(true).Length != 0 || state.GetComponentsInChildren<HutStages>(true).Length != 0)
                throw new InvalidOperationException("В окружении не должно быть второй постройки костра.");
            if (state.GetComponentsInChildren<Light>(true).Length != 0) throw new InvalidOperationException("Фонари должны пользоваться существующим пулом света.");
            if (state.lanterns == 0 || state.benches == 0) throw new InvalidOperationException("Не размещены основные предметы. Посмотри предупреждения сборки о свободных площадках.");
            foreach (var source in state.GetComponentsInChildren<FireSource>(true))
                if (source.emissive == null || source.emissive.Length == 0 || source.emissive.Any(r => r == null))
                    throw new InvalidOperationException("Не подключено свечение фонаря.");
        }
    }
}
