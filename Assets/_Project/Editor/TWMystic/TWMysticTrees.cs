using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using Surface=UnityEngine.PhysicsMaterial;
#else
using Surface=UnityEngine.PhysicMaterial;
#endif

namespace IslandV2
{
    internal static class TWMysticTrees
    {
        internal static MysticArtState.EconomyRecord CaptureEconomy(ResourceItem item)
        {
            if(item==null)throw new InvalidOperationException("Нет ресурсного дерева для снимка баланса.");
            return new MysticArtState.EconomyRecord{valid=true,gatherTime=item.gatherTime,respawn=item.respawn,
                useBalanceRespawn=item.useBalanceRespawn,respawnSeconds=item.respawnSeconds,growVisual=item.growVisual};
        }
        public static void Capture(TWMysticContext x)
        {
            var records=new List<MysticArtState.TreeRecord>();
            for(int i=0;i<6;i++)
            {
                var item=i<x.resources.Length?x.resources[i]:null;bool created=item==null;
                if(created)
                {
                    var go=new GameObject("MysticTree_"+(i+1));go.SetActive(false);Undo.RegisterCreatedObjectUndo(go,"Create authored resource tree");
                    item=Undo.AddComponent<ResourceItem>(go);go.tag="Resource";
                    int layer=LayerMask.NameToLayer("Resource");go.layer=x.resources.Length>0?x.resources[0].gameObject.layer:Mathf.Max(0,layer);
                }
                var info=item.GetComponent<TreeSpeciesInfo>();
                records.Add(new MysticArtState.TreeRecord{target=item,created=created,parent=item.transform.parent,position=item.transform.localPosition,
                    rotation=item.transform.localRotation,scale=item.transform.localScale,active=item.gameObject.activeSelf,json=EditorJsonUtility.ToJson(item),
                    economy=CaptureEconomy(item),
                    worldPosition=item.transform.position,worldRotation=item.transform.rotation,worldScale=item.transform.lossyScale,
                    hadSpecies=info!=null,speciesJson=info!=null?EditorJsonUtility.ToJson(info):null,
                    size=item.overrideSize?item.forcedSize:created?TreeSize.Mature:TreeSizing.Roll(item.transform.position),
                    collisions=item.GetComponentsInChildren<Collider>(true).Select(c=>new MysticArtState.ColliderRecord{target=c,json=EditorJsonUtility.ToJson(c)}).ToArray(),
                    renderers=item.GetComponentsInChildren<Renderer>(true).Select(r=>new MysticArtState.RendererRecord{target=r,enabled=r.enabled}).ToArray(),
                    lods=item.GetComponentsInChildren<LODGroup>(true).Select(l=>new MysticArtState.LodRecord{target=l,enabled=l.enabled}).ToArray()});
            }
            x.state.trees=records.ToArray();
        }
        public static void Build(TWMysticContext x,TWMysticAssets a,Surface surface)
        {
            var parent=new GameObject("10_Шесть_рубимых_деревьев").transform;parent.SetParent(x.root,false);
            for(int i=0;i<x.state.trees.Length;i++)
            {
                var record=x.state.trees[i];var item=record.target;
                if(item==null)throw new InvalidOperationException("Ресурсное дерево удалено вручную. Откати установку перед повторной сборкой.");
                if(!record.economy.valid)throw new InvalidOperationException("Нет исходного снимка баланса: "+item.name+". Повтори полную сборку пунктом 2.");
                var added=new List<UnityEngine.Object>();Undo.RecordObject(item,"Keep resource economy; bake appearance");Undo.RecordObject(item.transform,"Place six authored trees");
                Undo.SetTransformParent(item.transform,parent,"Preserve original resource object");
                Vector2 p=x.Site("Tree_"+i,TWMysticContext.Ring(36+i*57.0f,i%2==0?13.5f:14.2f));
                item.transform.SetPositionAndRotation(x.Ground(p),x.rotation*Quaternion.Euler(0,i*63,0));item.transform.localScale=Vector3.one;
                foreach(var old in record.renderers)if(old.target!=null){Undo.RecordObject(old.target,"Hide old tree model");old.target.enabled=false;}
                foreach(var old in record.collisions)if(old.target!=null){Undo.RecordObject(old.target,"Use authored tree collision");old.target.enabled=false;}
                foreach(var lod in record.lods)if(lod.target!=null){Undo.RecordObject(lod.target,"Use authored tree model");lod.target.enabled=false;}
                item.useGeneratedModel=false;item.autoSize=false;item.overrideSize=true;item.forcedSize=record.size;
                var info=item.GetComponent<TreeSpeciesInfo>();if(info==null){info=Undo.AddComponent<TreeSpeciesInfo>(item.gameObject);added.Add(info);}else Undo.RecordObject(info,"Set visual species dimensions");
                float k=GameBalance.TreeScaleMul[(int)record.size];info.species="Mystic_Memory";info.pickRadius=1.4f*k;info.pickHeight=4.5f*k;info.trunkRadius=0.19f*k;info.trunkHeight=1.5f*k;
                var pick=item.GetComponent<CapsuleCollider>();if(pick==null){pick=Undo.AddComponent<CapsuleCollider>(item.gameObject);added.Add(pick);}else Undo.RecordObject(pick,"Authored pick collider");
                pick.enabled=true;pick.isTrigger=true;pick.direction=1;pick.radius=info.pickRadius;pick.height=info.pickHeight;pick.center=Vector3.up*(info.pickHeight*0.5f);
                Transform solid=item.transform.Find(TreeCollision.TrunkName);
                if(solid==null){var go=new GameObject(TreeCollision.TrunkName);go.layer=item.gameObject.layer;go.transform.SetParent(item.transform,false);Undo.RegisterCreatedObjectUndo(go,"Bake tree trunk");solid=go.transform;added.Add(go);}
                var cap=solid.GetComponent<CapsuleCollider>();if(cap==null){cap=Undo.AddComponent<CapsuleCollider>(solid.gameObject);if(!added.Contains(solid.gameObject))added.Add(cap);}else Undo.RecordObject(cap,"Authored trunk shape");
                cap.enabled=true;cap.isTrigger=false;cap.direction=1;cap.radius=info.trunkRadius;cap.height=info.trunkHeight;cap.center=Vector3.up*(info.trunkHeight*0.5f-0.03f);cap.sharedMaterial=surface;
                var model=TWMysticModels.Tree(a,item.transform,i,record.size);added.Add(model);
                record.added=added.ToArray();Undo.RecordObject(item.gameObject,"Show authored tree");item.gameObject.SetActive(true);EditorUtility.SetDirty(item);
            }
        }
        public static void Restore(MysticArtState state,bool final)
        {
            foreach(var record in state.trees)
            {
                if(record.target==null)continue;
                if(record.created && final){Undo.DestroyObjectImmediate(record.target.gameObject);continue;}
                foreach(var obj in record.added)if(obj!=null)Undo.DestroyObjectImmediate(obj);
                record.added=Array.Empty<UnityEngine.Object>();
                var item=record.target;Undo.RecordObject(item,"Restore resource settings");EditorJsonUtility.FromJsonOverwrite(record.json,item);
                // Old installed scenes contain only the full Editor JSON. Migrate after restoring it with the same serializer.
                if(!record.economy.valid)record.economy=CaptureEconomy(item);
                else
                {
                    // Restore the exact original scalar values; JSON remains the backup for visual settings and references.
                    item.gatherTime=record.economy.gatherTime;item.respawn=record.economy.respawn;
                    item.useBalanceRespawn=record.economy.useBalanceRespawn;item.respawnSeconds=record.economy.respawnSeconds;item.growVisual=record.economy.growVisual;
                }
                Undo.RecordObject(item.transform,"Restore resource placement");Undo.SetTransformParent(item.transform,record.parent,"Restore original resource parent");
                if(record.parent!=null){item.transform.localPosition=record.position;item.transform.localRotation=record.rotation;item.transform.localScale=record.scale;}
                else{item.transform.SetPositionAndRotation(record.worldPosition,record.worldRotation);item.transform.localScale=record.worldScale;}
                Undo.RecordObject(item.gameObject,"Restore resource visibility");item.gameObject.SetActive(record.active);
                foreach(var old in record.collisions)if(old.target!=null){Undo.RecordObject(old.target,"Restore tree collision");EditorJsonUtility.FromJsonOverwrite(old.json,old.target);}
                foreach(var old in record.renderers)if(old.target!=null){Undo.RecordObject(old.target,"Restore tree renderer");old.target.enabled=old.enabled;}
                foreach(var old in record.lods)if(old.target!=null){Undo.RecordObject(old.target,"Restore tree LOD group");old.target.enabled=old.enabled;}
                var species=item.GetComponent<TreeSpeciesInfo>();if(record.hadSpecies && species!=null){Undo.RecordObject(species,"Restore species info");EditorJsonUtility.FromJsonOverwrite(record.speciesJson,species);}
            }
        }
    }
}
