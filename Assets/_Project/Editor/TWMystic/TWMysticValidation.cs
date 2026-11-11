using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWMysticValidation
    {
        internal static string PropKeyProblems(IEnumerable<string> actual,IEnumerable<string> required)
        {
            var counts=actual.Where(k=>k!=null).GroupBy(k=>k,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
            var problems=new List<string>();
            foreach(string key in required)
            {
                int count=counts.TryGetValue(key,out var n)?n:0;
                if(count!=1)problems.Add(key+": найдено "+count+", требуется 1");
            }
            return string.Join("; ",problems);
        }
        internal static void PropKeyTests()
        {
            string[] benches=Enumerable.Range(0,6).Select(i=>"Bench_"+i).ToArray();
            string[] actual=benches.Concat(new[]{"Bench_Memory","Ribbon_Memory","Gunnar_Memory","Entrance"}).ToArray();
            if(PropKeyProblems(actual,benches).Length!=0)throw new InvalidOperationException("Тест: Bench_Memory ошибочно посчитан скамьёй.");
            if(!PropKeyProblems(new[]{"Bench_Memory"},benches).Contains("Bench_0:"))throw new InvalidOperationException("Тест: сюжетный предмет заменил настоящую скамью.");
            if(!PropKeyProblems(actual.Where(k=>k!="Bench_3"),benches).Contains("Bench_3: найдено 0"))throw new InvalidOperationException("Тест: не найдена отсутствующая скамья.");
            var duplicate=new[]{"Bench_0","Bench_1","Bench_1","Bench_3","Bench_4","Bench_5","Bench_Memory"};
            string errors=PropKeyProblems(duplicate,benches);
            if(!errors.Contains("Bench_1: найдено 2") || !errors.Contains("Bench_2: найдено 0"))throw new InvalidOperationException("Тест: повтор скрывает пропуск скамьи.");
            if(PropKeyProblems(actual.Concat(new string[]{null,""}),benches).Length!=0)throw new InvalidOperationException("Тест: пустой ключ мешает проверке скамей.");
        }
        internal static string EconomyDifferences(MysticArtState.EconomyRecord before,MysticArtState.EconomyRecord after)
        {
            if(!before.valid || !after.valid)return "нет исходного снимка; повтори сборку пунктом 2";
            var result=new StringBuilder();
            string Number(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
            void Difference(string field,string left,string right,bool same)
            {
                if(same)return;if(result.Length>0)result.Append("; ");result.Append(field).Append(": ").Append(left).Append(" → ").Append(right);
            }
            Difference("gatherTime",Number(before.gatherTime),Number(after.gatherTime),before.gatherTime.Equals(after.gatherTime));
            Difference("respawn",before.respawn.ToString(),after.respawn.ToString(),before.respawn==after.respawn);
            Difference("useBalanceRespawn",before.useBalanceRespawn.ToString(),after.useBalanceRespawn.ToString(),before.useBalanceRespawn==after.useBalanceRespawn);
            Difference("respawnSeconds.x",Number(before.respawnSeconds.x),Number(after.respawnSeconds.x),before.respawnSeconds.x.Equals(after.respawnSeconds.x));
            Difference("respawnSeconds.y",Number(before.respawnSeconds.y),Number(after.respawnSeconds.y),before.respawnSeconds.y.Equals(after.respawnSeconds.y));
            Difference("growVisual",before.growVisual.ToString(),after.growVisual.ToString(),before.growVisual==after.growVisual);
            return result.ToString();
        }
        internal static void EconomyTests()
        {
            var temporary=new GameObject("Mystic_Economy_Test"){hideFlags=HideFlags.HideAndDontSave};temporary.SetActive(false);
            try
            {
                var tree=temporary.AddComponent<ResourceItem>();tree.gatherTime=4.125f;tree.respawn=false;tree.useBalanceRespawn=false;
                tree.respawnSeconds=new Vector2(17.25f,33.75f);tree.growVisual=false;
                var before=TWMysticTrees.CaptureEconomy(tree);
                if(!before.valid || before.gatherTime!=4.125f || before.respawn || before.useBalanceRespawn || before.growVisual || before.respawnSeconds!=tree.respawnSeconds)
                    throw new InvalidOperationException("Тест снимка: потеряны нестандартные значения или false.");
                if(EconomyDifferences(before,TWMysticTrees.CaptureEconomy(tree)).Length!=0)throw new InvalidOperationException("Тест снимка: ложное изменение неизменённого дерева.");
                var persisted=JsonUtility.FromJson<MysticArtState.EconomyRecord>(JsonUtility.ToJson(before));
                if(EconomyDifferences(before,persisted).Length!=0)throw new InvalidOperationException("Тест снимка: типизированные данные не пережили сериализацию.");
                string original=EditorJsonUtility.ToJson(tree);tree.gatherTime=99;tree.respawn=true;tree.useBalanceRespawn=true;tree.respawnSeconds=Vector2.zero;tree.growVisual=true;
                EditorJsonUtility.FromJsonOverwrite(original,tree);
                if(EconomyDifferences(before,TWMysticTrees.CaptureEconomy(tree)).Length!=0)throw new InvalidOperationException("Тест отката: Editor-снимок не восстановил исходные настройки.");
                void Detect(MysticArtState.EconomyRecord changed,string field)
                {if(!EconomyDifferences(before,changed).Contains(field+":"))throw new InvalidOperationException("Тест защиты баланса: пропущено поле "+field);}
                var after=before;after.gatherTime+=0.25f;Detect(after,"gatherTime");
                after=before;after.respawn=true;Detect(after,"respawn");
                after=before;after.useBalanceRespawn=true;Detect(after,"useBalanceRespawn");
                after=before;after.respawnSeconds.x+=1;Detect(after,"respawnSeconds.x");
                after=before;after.respawnSeconds.y+=1;Detect(after,"respawnSeconds.y");
                after=before;after.growVisual=true;Detect(after,"growVisual");
            }
            finally{UnityEngine.Object.DestroyImmediate(temporary);}
        }
        public static void Mesh(Mesh mesh)
        {
            if(mesh==null || mesh.vertexCount<3)throw new InvalidOperationException("Пустой меш Старого леса.");
            var v=mesh.vertices;var t=mesh.triangles;if(t.Length==0 || t.Length%3!=0)throw new InvalidOperationException("Нет треугольников: "+mesh.name);
            // Imported bevels on the small signature are valid below the procedural scenery threshold.
            float minCrossSquared=AssetDatabase.GetAssetPath(mesh).EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)?1e-20f:1e-12f;
            foreach(var p in v)if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))throw new InvalidOperationException("Неверная вершина: "+mesh.name);
            for(int i=0;i<t.Length;i+=3)
            {
                if(t[i]<0||t[i+1]<0||t[i+2]<0||t[i]>=v.Length||t[i+1]>=v.Length||t[i+2]>=v.Length)throw new InvalidOperationException("Неверные индексы: "+mesh.name);
                if(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude<minCrossSquared)throw new InvalidOperationException("Вырожденная грань: "+mesh.name);
            }
        }
        public static void Art(TWMysticContext x)
        {
            if(x.root==null || x.state==null || x.state.trees.Length!=6)throw new InvalidOperationException("Нужен один сохранённый лес с шестью деревьями.");
            foreach(var filter in x.root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null || !renderer.enabled)continue;
                Mesh(filter.sharedMesh);
                if(!AssetDatabase.Contains(filter.sharedMesh))throw new InvalidOperationException("Меш не сохранён в Assets: "+filter.name);
                if(renderer.sharedMaterials.Length!=filter.sharedMesh.subMeshCount || renderer.sharedMaterials.Any(m=>m==null || m.shader==null || m.shader.name=="Hidden/InternalErrorShader"))
                    throw new InvalidOperationException("Не назначен материал: "+filter.name);
            }
            if(x.elder.GetComponent<ResourceItem>()!=null || x.elder.CompareTag("Resource"))throw new InvalidOperationException("Дедушка не должен быть рубимым.");
            var cedar=x.root.Find("Cedar_Landmark");
            if(cedar==null)throw new InvalidOperationException("Не установлен новый кедр.");
            var cedarRenderers=cedar.GetComponentsInChildren<MeshRenderer>(true);
            if(cedarRenderers.Length==0)throw new InvalidOperationException("У кедра нет мешей.");
            var cedarBounds=cedarRenderers[0].bounds;
            foreach(var r in cedarRenderers)cedarBounds.Encapsulate(r.bounds);
            if(cedarBounds.size.y<10 || cedarBounds.size.y>22)
                throw new InvalidOperationException("Неверные оси или масштаб FBX кедра. Импортируй модели вместе с их .meta из патча.");
            foreach(var record in x.state.trees)
            {
                var tree=record.target;if(tree==null || !tree.gameObject.activeInHierarchy || !tree.CompareTag("Resource"))throw new InvalidOperationException("Неактивное или непомеченное ресурсное дерево.");
                if(!record.economy.valid)throw new InvalidOperationException("Старый снимок баланса дерева «"+tree.name+"». Повтори пункт 2 для обновления снимка.");
                string differences=EconomyDifferences(record.economy,TWMysticTrees.CaptureEconomy(tree));
                if(differences.Length!=0)throw new InvalidOperationException("Изменился баланс ресурсного дерева «"+tree.name+"»: "+differences);
                if(tree.useGeneratedModel || tree.autoSize || !tree.overrideSize || tree.forcedSize!=record.size)throw new InvalidOperationException("Дерево не подготовлено в Editor: "+tree.name);
                if(!x.Supported(x.Local(tree.transform.position),1.1f))throw new InvalidOperationException("Нет опоры вокруг ресурсного дерева: "+tree.name);
                var trunk=tree.transform.Find(TreeCollision.TrunkName);var cap=trunk!=null?trunk.GetComponent<CapsuleCollider>():null;
                if(cap==null || !cap.enabled || cap.isTrigger || cap.sharedMaterial==null || cap.sharedMaterial.dynamicFriction!=0)throw new InvalidOperationException("Нет гладкого ствола: "+tree.name);
            }
            if(x.root.GetComponentsInChildren<ResourceItem>(true).Length!=6)throw new InvalidOperationException("Внутри сохранённого корня не шесть ResourceItem.");
            var props=x.root.GetComponentsInChildren<MysticProp>(true);
            string[] memories={"Ribbon_Memory","Gunnar_Memory","Bench_Memory"};
            var required=Enumerable.Range(0,6).Select(i=>"Bench_"+i).Concat(memories).Concat(Enumerable.Range(0,3).Select(i=>"Witness_"+i)).Concat(new[]{"Entrance","SecretBench"});
            string propProblems=PropKeyProblems(props.Select(p=>p.key),required);
            if(propProblems.Length!=0)throw new InvalidOperationException("Состав объектов Круга молчания: "+propProblems);
            foreach(string key in memories)
            {
                var dialogue=props.Single(p=>p.key==key).GetComponent<DialogueSequence>();
                if(dialogue==null || dialogue.lines==null || dialogue.lines.Length==0)throw new InvalidOperationException("Нет реплик у сюжетного следа: "+key);
            }
            var vibe=x.root.GetComponentInChildren<MysticAtmosphere>(true);
            if(vibe==null || vibe.dayVolume==null || vibe.nightVolume==null || vibe.particles.Length!=3 || vibe.signature.Length==0 || vibe.signatureAnchor==null || vibe.crown.Length==0 || vibe.swing==null || vibe.signatureFireflies==null)
                throw new InvalidOperationException("Не сохранена полная атмосфера.");
            if(vibe.signatureAnchor.GetComponentsInChildren<TextMesh>(true).Length!=0 || vibe.nightWords.Length!=0)
                throw new InvalidOperationException("Ночная подпись должна быть моделью инкрустации.");
            ValidateNightDetails(vibe, props);
            if(vibe.signatureAnchor.GetComponentsInChildren<ParticleSystem>(true).Length!=1)
                throw new InvalidOperationException("У подписи должен быть один эффект светлячков.");
            foreach(var volume in new[]{vibe.dayVolume,vibe.nightVolume})
                if(volume.sharedProfile==null || !AssetDatabase.Contains(volume.sharedProfile) || volume.sharedProfile.components.Any(c=>c==null || !AssetDatabase.Contains(c)))
                    throw new InvalidOperationException("Профиль или его компоненты не сохранены в Assets.");
            var floor=x.root.GetComponentsInChildren<MeshCollider>(true).FirstOrDefault(c=>c.name=="00_Conforming_Ground");
            if(floor==null || floor.sharedMesh==null || !floor.enabled)throw new InvalidOperationException("Нет сохранённой опоры поляны.");
            var vertices=floor.sharedMesh.vertices;var triangles=floor.sharedMesh.triangles;int stride=Mathf.Max(1,triangles.Length/3/20)*3;
            for(int index=0;index<triangles.Length;index+=stride)
            {
                Vector3 point=(vertices[triangles[index]]+vertices[triangles[index+1]]+vertices[triangles[index+2]])/3;
                Vector3 world=floor.transform.TransformPoint(point);
                if(!floor.Raycast(new Ray(world+Vector3.up*2,Vector3.down),out RaycastHit hit,4))throw new InvalidOperationException("Пустой пол в точке "+world);
            }
            foreach(var c in x.root.GetComponentsInChildren<CapsuleCollider>(true))
            {
                if(!c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger)continue;
                for(int z=6;z<=Mathf.Min(24,x.zoneRadius-1);z+=2)
                {
                    if(!x.TryGround(new Vector2(0,z),out Vector3 ground))continue;
                    Vector3 local=c.transform.InverseTransformPoint(ground)-c.center;
                    float half=Mathf.Max(0,c.height/2-c.radius);
                    if(c.direction==0)local.x=Mathf.Max(0,Mathf.Abs(local.x)-half);
                    if(c.direction==2)local.z=Mathf.Max(0,Mathf.Abs(local.z)-half);
                    if(new Vector2(local.x,local.z).magnitude<c.radius+0.65f)throw new InvalidOperationException("Перекрыт центральный подход: "+c.transform.parent.name);
                }
            }
            foreach(var disabled in x.state.disabled)if(disabled.target!=null && disabled.target.enabled)throw new InvalidOperationException("Включён старый генератор или эффект: "+disabled.target.name);
            foreach(string path in new[]{"Assets/_Project/Scripts/123/MysticGladeBuilder.cs","Assets/_Project/Scripts/123/MysticGladeBuilder.Props.cs","Assets/_Project/Scripts/123/MysticGladeVibe.cs"})
            {
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if(script==null || script.text.Contains("RuntimeInitializeOnLoadMethod") || script.text.Contains("new GameObject"))throw new InvalidOperationException("Остался старый runtime-генератор: "+path);
            }
        }
        static void ValidateNightDetails(MysticAtmosphere vibe, MysticProp[] props)
        {
            var benchPlaque = props.Single(p => p.key == "Bench_4").transform.Find(TWMysticNightDetails.BenchPlaque);
            var witnessPlaque = props.Single(p => p.key == "Witness_1").transform.Find(TWMysticNightDetails.WitnessPlaque);
            if (benchPlaque == null || witnessPlaque == null || vibe.engravingSurfaces.Length != 2 ||
                !vibe.engravingSurfaces.Contains(benchPlaque.GetComponent<MeshRenderer>()) ||
                !vibe.engravingSurfaces.Contains(witnessPlaque.GetComponent<MeshRenderer>()) ||
                vibe.engravingSurfaces.Any(r => r == null || r.sharedMaterial == null ||
                    r.sharedMaterial.GetTexture("_BaseMap") == null || r.sharedMaterial.GetTexture("_EmissionMap") == null))
                throw new InvalidOperationException("Не сохранены две гравировки. Повтори сборку Круга молчания.");

            if (!witnessPlaque.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger &&
                (!(c is MeshCollider mesh) || mesh.sharedMesh != null)))
                throw new InvalidOperationException("У каменной плиты нет твёрдой коллизии.");

            if (vibe.nightAccents.Length != TWMysticNightDetails.AccentCount ||
                vibe.nightAccents.Any(a => a.light == null || a.light.shadows != LightShadows.None))
                throw new InvalidOperationException("Не настроены локальные ночные огни без теней.");
            if (vibe.fireflies.Length != TWMysticNightDetails.FireflyGroupCount ||
                vibe.fireflies.Any(ps => ps == null || ps.main.playOnAwake))
                throw new InvalidOperationException("Светлячки должны включаться только контроллером ночи.");
            if (vibe.ambience == null || vibe.ambience.clip == null || !vibe.ambience.loop ||
                vibe.nightEchoes.Length != 1 || vibe.nightEchoes.Any(a => a == null || a.clip == null || a.playOnAwake || a.loop) ||
                vibe.swingCreak == null || vibe.swingCreak.clip == null || vibe.swingCreak.playOnAwake || vibe.swingCreak.loop ||
                vibe.nightEchoes.Contains(vibe.swingCreak))
                throw new InvalidOperationException("Не назначен фон поляны или один из ночных звуков.");

            var seat = vibe.swing.Find("Swing_Seat_Collision");
            var box = seat != null ? seat.GetComponent<BoxCollider>() : null;
            if (box == null || box.isTrigger || !box.enabled ||
                box.size.y * Mathf.Abs(seat.lossyScale.y) > 0.38f)
                throw new InvalidOperationException("Коллайдер должен быть установлен только на сиденье качелей.");
        }

        [MenuItem("Tools/BuilderSim/Лес/MYSTIC FOREST/8. Проверить переходы и отключение runtime-сборки",false,357)]
        static void Tests()
        {
            try
            {
                if(MysticAtmosphere.Boundary(19,18,5)!=0 || MysticAtmosphere.Boundary(18,18,5)!=0 || MysticAtmosphere.Boundary(0,18,5)!=1)
                    throw new InvalidOperationException("Неверная граница атмосферы.");
                if(MysticAtmosphere.SecretVisibility(false,1,0,2.5f,5)!=0 ||
                    MysticAtmosphere.SecretVisibility(true,0,0,2.5f,5)!=0 ||
                    MysticAtmosphere.SecretVisibility(true,1,6,2.5f,5)!=0 ||
                    MysticAtmosphere.SecretVisibility(true,1,0,2.5f,5)!=1)
                    throw new InvalidOperationException("Дневная/ночная видимость пасхалки нарушена.");
                TWMysticContext.SurfaceTests();
                EconomyTests();
                PropKeyTests();
                float previous=1;
                for(int i=0;i<=200;i++)
                {float value=MysticAtmosphere.Boundary(i*0.1f,18,5);if(value>previous || value<0 || value>1)throw new InvalidOperationException("Разрыв плавного перехода.");previous=value;}
                var x=TWMysticArt.Inspect();if(x.state==null || x.state.art==null)throw new InvalidOperationException("Сначала пункт 2.");x.root=x.state.art.transform;Art(x);
                Debug.Log("MYSTIC FOREST TESTS: PASS. Типизированный снимок баланса, нестандартные значения, откат, обнаружение изменения каждого поля, адаптивная раскладка и отсутствие runtime-сборки проверены.");
            }
            catch(Exception e){Debug.LogError("MYSTIC FOREST TESTS: "+e);}
        }
    }
}
