using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IslandV2
{
    internal static class TWGroveValidation
    {
        static readonly Regex InstanceReference = new Regex("\\\"instanceID\\\"\\s*:\\s*(-?\\d+)");
        public static string ProtectedHash(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true));
            var components = all.Where(c => c is ResourceItem || c is BuildingController || c is HutStages || c is MapZone || c is ForestZone)
                .OrderBy(c => GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(), StringComparer.Ordinal).ToArray();
            var text = new StringBuilder(); var references = new Dictionary<int, string>();
            string Json(Object target)
            {
                return InstanceReference.Replace(EditorJsonUtility.ToJson(target), match =>
                {
                    int id = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (!references.TryGetValue(id, out string stable))
                    {
                        Object obj = id == 0 ? null : EditorUtility.InstanceIDToObject(id);
                        stable = obj == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString(); references.Add(id, stable);
                    }
                    return "\"stableRef\":\"" + stable + "\"";
                });
            }
            foreach (var component in components)
            {
                text.Append(component.GetType().FullName).Append(Json(component)).Append(Json(component.transform));
                text.Append(component.gameObject.activeSelf).Append(component.gameObject.tag).Append(component.gameObject.layer);
                foreach (var collider in component.GetComponents<Collider>()) text.Append(Json(collider));
                if (component is BuildingController controller && controller.data != null) text.Append(Json(controller.data));
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }

        public static void Mesh(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount < 3) throw new InvalidOperationException("Пустая геометрия поляны.");
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            if (triangles.Length == 0 || triangles.Length % 3 != 0) throw new InvalidOperationException("Нет треугольников: " + mesh.name);
            foreach (var p in vertices)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new InvalidOperationException("NaN/Infinity: " + mesh.name);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (triangles[i] < 0 || triangles[i + 1] < 0 || triangles[i + 2] < 0 || triangles[i] >= vertices.Length || triangles[i + 1] >= vertices.Length || triangles[i + 2] >= vertices.Length)
                    throw new InvalidOperationException("Индексы за пределами меша: " + mesh.name);
                if (Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude < 1e-12f)
                    throw new InvalidOperationException("Вырожденная грань: " + mesh.name);
            }
        }
        static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }

        public static void Geometry(Transform root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh(filter.sharedMesh); var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount || renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader"))
                    throw new InvalidOperationException("Не назначены материалы: " + filter.name);
            }
            if (root.GetComponentsInChildren<Light>(true).Length != 0) throw new InvalidOperationException("Фонари должны использовать пул света.");
            if (root.GetComponentsInChildren<ResourceItem>(true).Length != 0 || root.GetComponentsInChildren<BuildingController>(true).Length != 0)
                throw new InvalidOperationException("В окружении появилась лишняя игровая постройка или ресурс.");
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                if (col.isTrigger) throw new InvalidOperationException("Новый декор не должен создавать игровые триггеры.");
        }

        public static void Installed(TWGroveContext x)
        {
            var state = x.previous;
            if (state == null || state.artRoot == null) throw new InvalidOperationException("Сначала собери поляну, пункт 2.");
            var root = state.artRoot;
            Geometry(root);
            var handles = root.GetComponentsInChildren<GroveArtHandle>(true);
            if (handles.Count(h => h.key == "Gate") != 1) throw new InvalidOperationException("Нужна одна отдельная арка.");
            var gate = handles.Single(h => h.key == "Gate").transform;
            if(state.revision>=2)
            {
                if(gate.Find("GroveDetails_Gate")==null) throw new InvalidOperationException("Не добавлены опознавательные знаки арки.");
                foreach(var h in handles)
                {
                    int own=0;
                    foreach(Transform child in h.transform) if(child.name=="GroveDetails_"+h.key)own++;
                    if(own>1) throw new InvalidOperationException("Дублируются детали: "+h.name);
                    if(h.key.StartsWith("DetailPatch_",StringComparison.Ordinal) && h.GetComponentsInChildren<Collider>(true).Length!=0)
                        throw new InvalidOperationException("Подлесок не должен перекрывать проход коллайдерами.");
                }
            }
            Vector3 passage = gate.TransformPoint(new Vector3(0, 1.15f, 0));
            foreach (var box in gate.GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 d = box.transform.InverseTransformPoint(passage) - box.center;
                if (Mathf.Abs(d.x) < box.size.x * 0.5f && Mathf.Abs(d.y) < box.size.y * 0.5f && Mathf.Abs(d.z) < box.size.z * 0.5f)
                    throw new InvalidOperationException("Коллайдер внутри арки перекрывает середину прохода.");
            }
            if (handles.Select(h => h.key).Distinct().Count() != handles.Length) throw new InvalidOperationException("Повторяются ключи объектов.");
            if (handles.Any(h => h.transform.parent != root)) throw new InvalidOperationException("Ключевые объекты должны быть отдельными детьми корня поляны.");
            foreach (var hidden in state.hidden)
                if (hidden.target != null && hidden.target.activeSelf) throw new InvalidOperationException("Включён старый слой декора: " + hidden.target.name);
            if (x.marker.GetComponent<ChopGroveBuilder>() != null) throw new InvalidOperationException("На метке остался старый генератор. Повтори пункт 2.");
            var paths = root.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(m => m.name == "00_Земля_тропы_и_рабочие_пятна");
            if (paths == null || paths.sharedMesh.colors.Length != paths.sharedMesh.vertexCount || !paths.sharedMesh.colors.Any(c => c.a < 0.05f))
                throw new InvalidOperationException("Нет корректного перехода троп в землю.");
            var material = paths.GetComponent<Renderer>().sharedMaterial;
            if (!material.HasProperty("_GroundTex") || material.GetTexture("_GroundTex") == null) throw new InvalidOperationException("Потеряна родная текстура земли.");
            if (ProtectedHash(x.marker.gameObject.scene) != state.protectedHash)
                Debug.LogWarning("GROVE ART: деревья, стройки или зоны изменены после установки. При сборке их исходный снимок совпадал; проверь последующие ручные изменения.");
            Physics.SyncTransforms(); int warnings = 0;
            foreach (var handle in handles)
            {
                if (!handle.blocksWalking || handle.key == "Gate") continue;
                float yaw = (Quaternion.Inverse(x.rotation) * handle.transform.rotation).eulerAngles.y;
                Vector3 scale = handle.transform.lossyScale;
                var f = new TWGroveContext.Footprint { point = x.Local(handle.transform.position), half = Vector2.Scale(handle.halfSize, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.z))), yaw = yaw };
                if (x.trees.Any(t => TWGrovePaths.PointDistance(t.point, f) < t.radius - 0.08f) || x.buildings.Any(b => Vector2.Distance(b.point, f.point) < b.radius + f.half.magnitude))
                { warnings++; Debug.LogWarning("GROVE ART: проверь подходы после перемещения «" + handle.name + "».", handle); }
            }
            Debug.Log("GROVE ART QA: геометрия, материалы, разделение объектов и отключение старого генератора проверены. Объектов: " + handles.Length +
                ", деревьев для рубки: " + x.resources.Length + ", предупреждений по размещению: " + warnings + ". Рубку/рост деревьев и ночной свет проверь в Play Mode.");
        }
    }
}
