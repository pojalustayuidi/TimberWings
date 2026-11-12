using System;
using System.Collections.Generic;
using System.Linq;
using IslandV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Kind = StoryInspection.Kind;
using Object = UnityEngine.Object;

static class StoryInspectionEditor
{
    const string Menu = "Tools/TimberWings/Story Inspection/";
    const string AudioRoot = "Assets/_Project/Art/MysticCedar/NightDetail/";
    const string VoiceRoot = "Assets/_Project/Resources/Voice21/";

    static readonly string[] VoiceIds =
    {
        "leo_bdf9af3d", "leo_69cd25d4", "leo_b5024fb8", "leo_fc09f2aa",
        "leo_692e3e5e", "leo_af29fecd", "leo_2e4c2f87"
    };

    sealed class Binding
    {
        public MonoBehaviour source;
        public Kind kind;
        public Transform detail, bridge;
        public Vector3 offset;
        public string atmosphere;
        public bool authored;
        public bool Present => source.gameObject.activeInHierarchy;
        public bool Available => Present && source.enabled;
        public int VoiceMatches => source is DialogueSequence seq && seq.lines != null
            ? seq.lines.Count(line => line != null && VoiceIds.Contains(VoiceId.Make("Лео", line.text))) : 0;
    }

    [MenuItem(Menu + "Install / Update", false, 10)]
    static void Install()
    {
        if (!TryScene(out var scene)) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Story Inspection");
        var addedSources = RestoreGroveLinks(scene);
        var candidates = FindCandidates(scene);
        var createdObjects = new HashSet<GameObject>(TWGroveArt.RestoreMissingInspectionProps(
            !candidates.Any(b => b.kind == Kind.ThreeMarks && b.Available),
            !candidates.Any(b => b.kind == Kind.Duck && b.Available)));
        if (createdObjects.Count > 0) candidates = FindCandidates(scene);
        var selected = SelectBindings(candidates);
        var selectedSources = new HashSet<MonoBehaviour>(selected.Select(b => b.source));

        foreach (var binding in candidates)
        {
            bool winner = selectedSources.Contains(binding.source);
            if (!winner && binding.source.GetComponent<MysticProp>() == null) continue;
            var target = binding.source.gameObject;
            var inspection = target.GetComponent<StoryInspection>();
            bool created = inspection == null;
            if (created) inspection = Undo.AddComponent<StoryInspection>(target);
            Undo.RecordObject(inspection, "Bind story inspection");
            inspection.source = binding.source;
            inspection.kind = binding.kind;
            inspection.superseded = !winner;
            inspection.enabled = winner;
            inspection.ownsGroveSource |= addedSources.Contains(target);
            inspection.ownsGroveObject |= createdObjects.Contains(target);
            if (created) inspection.detailOffset = binding.offset;
            if (winner)
            {
                inspection.detail = SelectDetail(binding, candidates, inspection.detail, ref inspection.copiedDetail, b => b.detail);
                inspection.bridgeEngraving = SelectDetail(binding, candidates, inspection.bridgeEngraving, ref inspection.copiedBridge, b => b.bridge);
                if (inspection.atmosphere == null && binding.atmosphere != null)
                    inspection.atmosphere = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + binding.atmosphere + ".wav");
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(inspection);
        }

        Undo.CollapseUndoOperations(group);
        if (candidates.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Story Inspection: установлено {selected.Count} привязок в {scene.name}. Добавлено объектов Южной рощи: {createdObjects.Count}.");
        Audit();
    }

    static Transform SelectDetail(Binding chosen, List<Binding> all, Transform existing, ref Transform copied,
        Func<Binding, Transform> get)
    {
        var own = get(chosen);
        if (own != null) return own;
        if (existing != null && existing.IsChildOf(chosen.source.transform)) return existing;
        var template = all.Where(other => other.kind == chosen.kind && other != chosen).Select(get).FirstOrDefault(item => item != null);
        if (template == null) return null;
        var clone = Object.Instantiate(template.gameObject, chosen.source.transform, false);
        clone.name = template.name;
        clone.SetActive(true);
        Undo.RegisterCreatedObjectUndo(clone, "Restore inspection engraving");
        copied = clone.transform;
        return copied;
    }

    static HashSet<GameObject> RestoreGroveLinks(Scene scene)
    {
        var added = new HashSet<GameObject>();
        var roots = Components<GroveArtState>(scene).Select(state => state.artRoot)
            .Where(root => root != null && root.gameObject.activeInHierarchy).ToList();
        if (roots.Count == 0) return added;
        var handles = Components<GroveArtHandle>(scene).Where(h => h.gameObject.activeInHierarchy &&
            roots.Any(root => h.transform.IsChildOf(root))).ToArray();

        foreach (var key in new[] { "OldMark", "DuckStool" })
        {
            var candidates = handles.Where(h => h.key == key).ToArray();
            if (candidates.Any(h => h.GetComponent<GroveChopBlock>() != null)) continue;
            string exactName = key == "OldMark" ? "07_Камень_трёх_зарубок" : "08_Пень_который_не_рубят";
            var target = candidates.Length > 0 ? candidates[0].gameObject : roots.SelectMany(root =>
                    root.GetComponentsInChildren<Transform>()).FirstOrDefault(t => t.name == exactName)?.gameObject;
            if (target == null || target.GetComponent<GroveChopBlock>() != null) continue;
            TWGroveProps.FinishInspectionProp(target, key, addCollider: false);
            added.Add(target);
        }
        return added;
    }

    [MenuItem(Menu + "Audit", false, 11)]
    static void Audit()
    {
        if (!TryScene(out var scene)) return;
        var candidates = FindCandidates(scene);
        var bindings = SelectBindings(candidates);
        int issues = 0;
        int separateVisuals = 0;
        void Check(bool good, string message, Object context = null)
        {
            if (good) return;
            issues++;
            Debug.LogWarning("Story Inspection: " + message, context);
        }

        foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            Check(bindings.Any(b => b.kind == kind), $"не найдена цель {kind}.");

        var voice = new Dictionary<string, string>();
        foreach (var binding in bindings)
        {
            var source = binding.source;
            var inspection = source.GetComponent<StoryInspection>();
            bool installed = inspection != null && inspection.enabled && !inspection.superseded &&
                inspection.source == source && inspection.kind == binding.kind;
            Check(installed, $"не установлена привязка {binding.kind} на {source.name}.", source);
            if (binding.kind == Kind.BenchMarks || binding.kind == Kind.Witness)
                Check(installed && inspection.detail != null, $"на {source.name} отсутствует гравировка.", source);
            if (binding.kind == Kind.Knife)
                Check(installed && inspection.bridgeEngraving != null, "у ножа отсутствует привязанный знак моста.", source);
            if (installed && (binding.kind == Kind.Knife || binding.kind == Kind.CopperArrow ||
                binding.kind == Kind.Ribbon || binding.kind == Kind.Board) && !inspection.HasMovableMesh()) separateVisuals++;

            if (source is DialogueSequence seq)
            {
                Check(seq.trigger == DialogueSequence.Trigger.OnInteract && seq.lines != null && seq.lines.Length == 2,
                    $"на {source.name} ожидаются две интерактивные реплики.", source);
                if (seq.lines != null)
                    foreach (var line in seq.lines)
                        if (line != null && !string.IsNullOrWhiteSpace(line.text)) voice[VoiceId.Make("Лео", line.text)] = line.text;
            }
            if (source is GroveChopBlock grove)
                Check(grove.lines != null && grove.lines.Any(text => !string.IsNullOrWhiteSpace(text)),
                    $"на {source.name} отсутствует текст.", source);
        }
        if (bindings.Any(b => b.kind == Kind.Finale))
            voice[VoiceId.Make("Лео", StoryInspection.FinaleLine)] = StoryInspection.FinaleLine;

        int imported = VoiceIds.Count(HasVoice);
        foreach (string id in VoiceIds.Where(id => !HasVoice(id)))
            Check(false, $"отсутствует {id}.wav.");
        foreach (var line in voice.Where(pair => !VoiceIds.Contains(pair.Key) && !HasVoice(pair.Key)))
            Check(false, $"для текущей реплики нужен {line.Key}.wav — «{line.Value}».");

        string rigSource = RigSource(scene);
        Check(rigSource != null, "не найден риг Лео в сцене или префабе Resources/LeoAnimation/Leo_Model.");
        Check(Resources.Load<TextAsset>("LeoAnimation/Motion") != null, "отсутствует Resources/LeoAnimation/Motion.json.");
        Check(Components<CameraFollow>(scene).Any(), "в активной сцене отсутствует CameraFollow.");
        Check(Components<InteractionHub>(scene).Any(), "в активной сцене отсутствует InteractionHub.");

        int retired = candidates.Count - bindings.Count;
        Debug.Log($"Story Inspection Audit · {bindings.Count} целей, WAV {imported}/{VoiceIds.Length}, замечаний: {issues}. " +
            $"Пропущено копий: {retired}. Отдельных моделей: {separateVisuals}. Риг Лео: {rigSource ?? "отсутствует"}.");
    }

    static bool HasVoice(string id) => AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceRoot + id + ".wav") != null;

    static string RigSource(Scene scene)
    {
        foreach (var player in Components<PlayerMovement>(scene).Where(p => p.gameObject.activeInHierarchy))
            if (HasRig(player.gameObject)) return "сцена";

        var prefab = Resources.Load<GameObject>("LeoAnimation/Leo_Model");
        return prefab != null && HasRig(prefab) ? "Resources/LeoAnimation/Leo_Model" : null;
    }

    static bool HasRig(GameObject model)
    {
        foreach (var rig in model.GetComponentsInChildren<LeoRig>(true))
        {
            if (rig.handR == null || rig.handL == null || rig.chest == null || rig.hips == null ||
                rig.ankleR == null || rig.ankleL == null || rig.head == null) continue;
            var controller = rig.GetComponent<LeoAnimationController>();
            var presentation = rig.GetComponent<LeoModelPresentation>();
            if ((controller != null && controller.enabled) || (presentation != null && presentation.enabled)) return true;
        }
        return false;
    }

    [MenuItem(Menu + "Restore", false, 12)]
    static void Restore()
    {
        if (!TryScene(out var scene)) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore story interactions");
        int count = 0;
        foreach (var inspection in Components<StoryInspection>(scene).ToArray())
        {
            if (inspection == null) continue;
            if (inspection.ownsGroveObject) Undo.DestroyObjectImmediate(inspection.gameObject);
            else
            {
                if (inspection.copiedDetail != null) Undo.DestroyObjectImmediate(inspection.copiedDetail.gameObject);
                if (inspection.copiedBridge != null) Undo.DestroyObjectImmediate(inspection.copiedBridge.gameObject);
                if (inspection.ownsGroveSource && inspection.source != null) Undo.DestroyObjectImmediate(inspection.source);
                Undo.DestroyObjectImmediate(inspection);
            }
            count++;
        }
        Undo.CollapseUndoOperations(group);
        if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Story Inspection: снято {count} привязок. Сохрани сцену.");
    }

    static bool TryScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (!EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null &&
            scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path)) return true;
        Debug.LogWarning("Story Inspection: открой сохранённую игровую сцену в Edit Mode.");
        return false;
    }

    static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true));

    static List<Binding> SelectBindings(List<Binding> candidates) =>
        candidates.GroupBy(b => b.kind).Select(group => group.OrderByDescending(b => b.Available)
            .ThenByDescending(b => b.Present).ThenByDescending(b => b.authored)
            .ThenByDescending(b => b.detail != null || b.bridge != null)
            .ThenByDescending(b => b.VoiceMatches).First()).ToList();

    static List<Binding> FindCandidates(Scene scene)
    {
        var result = new List<Binding>();
        var mysticRoots = Components<MysticArtState>(scene).Where(state => state.art != null && state.art.activeInHierarchy)
            .Select(state => state.art.transform).ToArray();
        var groveRoots = Components<GroveArtState>(scene).Select(state => state.artRoot)
            .Where(root => root != null && root.gameObject.activeInHierarchy).ToArray();
        foreach (var prop in Components<MysticProp>(scene))
        {
            bool authored = mysticRoots.Any(root => prop.transform.IsChildOf(root));
            switch (prop.key)
            {
                case "Gunnar_Memory":
                    Add(prop.GetComponent<DialogueSequence>(), Kind.Knife, new Vector3(-.12f, .425f, .255f), authored, bridge: prop.transform.Find("Bridge_Engraving"));
                    break;
                case "Ribbon_Memory":
                    Add(prop.GetComponent<DialogueSequence>(), Kind.Ribbon, new Vector3(.035f, .402f, .075f), authored);
                    break;
                case "Bench_Memory":
                    Add(prop.GetComponent<DialogueSequence>(), Kind.Board, new Vector3(.035f, .402f, .075f), authored, atmosphere: "Forest_Breath");
                    break;
                case "Bench_4":
                    Add(prop, Kind.BenchMarks, new Vector3(0f, 1.15f, -.27f), authored, prop.transform.Find("Engraving_Bench"));
                    break;
                case "Witness_1":
                    Add(prop, Kind.Witness, new Vector3(0f, .70f, .59f), authored, prop.transform.Find("Engraving_Witness"), atmosphere: "Witness_Echo");
                    break;
            }
        }
        foreach (var egg in Components<FinalIslandEasterEgg>(scene))
            Add(egg, Kind.Finale, new Vector3(0f, .75f, .205f), false);

        foreach (var handle in Components<GroveArtHandle>(scene))
        {
            var grove = handle.GetComponent<GroveChopBlock>();
            bool authored = groveRoots.Any(root => handle.transform.IsChildOf(root));
            switch (handle.key)
            {
                case "Gate": Add(grove, Kind.Arch, new Vector3(0f, 3.46f, -.44f), authored); break;
                case "AxeStump": Add(grove, Kind.Stump, new Vector3(.10f, 1.35f, 0f), authored); break;
                case "OldMark": Add(grove, Kind.ThreeMarks, new Vector3(0f, .70f, -.405f), authored); break;
                case "LostClock": Add(grove, Kind.CopperArrow, new Vector3(.12f, .445f, .06f), authored); break;
                case "DuckStool": Add(grove, Kind.Duck, new Vector3(0f, .99f, -.22f), authored); break;
                case "Cart": Add(grove, Kind.Cart, new Vector3(-.89f, 1.03f, -.30f), authored); break;
            }
        }
        foreach (var grove in Components<GroveChopBlock>(scene))
        {
            if (grove.GetComponent<GroveArtHandle>() != null || !groveRoots.Any(root => grove.transform.IsChildOf(root))) continue;
            if (grove.name == "07_Камень_трёх_зарубок")
                Add(grove, Kind.ThreeMarks, new Vector3(0f, .70f, -.405f), true);
            else if (grove.name == "08_Пень_который_не_рубят")
                Add(grove, Kind.Duck, new Vector3(0f, .99f, -.22f), true);
        }
        return result;

        void Add(MonoBehaviour source, Kind kind, Vector3 offset, bool authored, Transform detail = null,
            Transform bridge = null, string atmosphere = null)
        {
            if (source != null) result.Add(new Binding { source = source, kind = kind, offset = offset,
                detail = detail, bridge = bridge, atmosphere = atmosphere, authored = authored });
        }
    }
}
