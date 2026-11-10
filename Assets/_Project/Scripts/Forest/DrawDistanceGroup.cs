// Assets/_Project/Scripts/Forest/DrawDistanceGroup.cs (НЕ в Editor)
// Дальность прорисовки мелкого декора вокруг Лео (настройка «Дальность прорисовки»: Ближняя / Средняя / Дальняя).
//
// Как работает:
//   • Дистанция считается по горизонтали (XZ) от PlayerMovement.Instance.transform до центра каждого рендерера.
//     Камера в расчёте не участвует: поворот и зум зоны сами по себе не меняют.
//   • Рендереры раскладываются по ярусам (Tier). У каждого яруса свой порог скрытия для трёх пресетов (метры, правятся в Inspector).
//     Вблизи видно всё; мелочь (трава, цветы, галька) пропадает первой, средний декор (камни, кусты) позже.
//     Деревья, рельеф, здания и ориентиры в ярусы не кладём: их ведут свои LODGroup / WingForest.
//   • Прячем через Renderer.forceRenderingOff: коллайдеры, GameObject, enabled, LODGroup и логика остаются как есть.
//   • Проверка раз в checkInterval секунд (по умолчанию 0.25), а не каждый кадр. Гистерезис: скрываем за порогом + hysteresis,
//     показываем обратно внутри порога - hysteresis, поэтому на границе нет мерцания.
//   • Во время облёта стройки (CameraVibe.RevealActive) декор у здания остаётся видимым: дистанция считается ещё и от точки показа.
//   • Миникарта снимается отдельной камерой: на время снимка HudMinimap зовёт BeginCapture / EndCapture и всё показывается.
//   • Туман и дымку компонент НЕ трогает: пороги разнесены так, чтобы скрытие приходилось на уже приглушённую туманом дальность.
//
// Где повесить: на корень поляны (объект с GroveArtState, TW_GROVE_SOUTH). Группы рендереров заполняются в Inspector
// (или кнопкой в меню компонента «Заполнить ярусы из artRoot»; она подбирает рендереры по ключевым словам в именах, результат проверь).
using System;
using System.Collections.Generic;
using UnityEngine;

public enum DrawDistanceLevel { Near = 0, Medium = 1, Far = 2 }

[Serializable]
public struct DrawDistanceThresholds
{
    [Tooltip("Ближняя, м")] public float near;
    [Tooltip("Средняя, м")] public float medium;
    [Tooltip("Дальняя, м")] public float far;

    public DrawDistanceThresholds(float near, float medium, float far) { this.near = near; this.medium = medium; this.far = far; }

    public float Get(DrawDistanceLevel level)
    {
        return level == DrawDistanceLevel.Near ? near : level == DrawDistanceLevel.Far ? far : medium;
    }
}

[DisallowMultipleComponent]
public sealed class DrawDistanceGroup : MonoBehaviour
{
    [Serializable]
    public class Tier
    {
        public string name = "Ярус";
        [Tooltip("Дальше этого расстояния от Лео рендереры яруса не рисуются, м")]
        public DrawDistanceThresholds hideBeyond = new DrawDistanceThresholds(16f, 28f, 45f);
        [Tooltip("Рендереры яруса. Назначать явно: деревья, здания и ориентиры сюда не кладём")]
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("Готовые LODGroup мелкого декора: берутся все их рендереры (сам LODGroup не отключается)")]
        public LODGroup[] lodGroups = new LODGroup[0];
        [Tooltip("Только для кнопки автозаполнения в редакторе: ключевые слова в именах объектов через запятую")]
        public string autoFillKeywords = "";
    }

    [Tooltip("Ярусы от самых мелких к крупным")]
    public Tier[] tiers =
    {
        new Tier { name = "Мелкий декор (трава, цветы, галька)", hideBeyond = new DrawDistanceThresholds(16f, 28f, 45f),
                   autoFillKeywords = "grass,трав,flower,цвет,pebble,галь,mushroom,гриб,clover,fern,папорот,twig,ветк" },
        new Tier { name = "Средний декор (камни, кусты)", hideBeyond = new DrawDistanceThresholds(32f, 52f, 80f),
                   autoFillKeywords = "rock,stone,камн,bush,куст,fence,забор,crate,barrel" },
    };

    [Tooltip("Запас гистерезиса, м: скрываем за порогом + запас, возвращаем внутри порога - запас")]
    [Min(0f)] public float hysteresis = 3f;
    [Tooltip("Как часто проверять дистанцию, с")]
    [Min(0.05f)] public float checkInterval = 0.25f;
    [Tooltip("Откуда брать рендереры для автозаполнения. Пусто: GroveArtState.artRoot на этом же объекте")]
    public Transform autoFillRoot;

    sealed class Runtime
    {
        public Renderer[] r;
        public Vector3[] pos;
        public bool[] hidden;
    }

    static readonly List<DrawDistanceGroup> all = new List<DrawDistanceGroup>();

    Runtime[] rt;
    float nextCheck;
    bool snap = true;
    bool captured;

    // ---------- снимок миникарты

    // Миникарту рисует отдельная камера, а forceRenderingOff действует на все камеры: на время снимка показываем всё
    public static void BeginCapture()
    {
        for (int i = 0; i < all.Count; i++) if (all[i] != null) all[i].Capture(true);
    }

    public static void EndCapture()
    {
        for (int i = 0; i < all.Count; i++) if (all[i] != null) all[i].Capture(false);
    }

    // ---------- жизнь

    void OnEnable()
    {
        Build();
        all.Add(this);
        VibeSettings.DrawDistanceChanged += OnLevelChanged;
        snap = true;
        nextCheck = 0f;
    }

    void OnDisable()
    {
        VibeSettings.DrawDistanceChanged -= OnLevelChanged;
        all.Remove(this);
        captured = false;
        ShowAllAndReset();   // компонент выключили: всё возвращаем как было
        rt = null;
    }

    void OnLevelChanged()
    {
        snap = true;
        Evaluate();   // пресет применяется сразу
    }

    void Build()
    {
        rt = new Runtime[tiers != null ? tiers.Length : 0];
        var seen = new HashSet<Renderer>();
        var list = new List<Renderer>();
        for (int t = 0; t < rt.Length; t++)
        {
            var tier = tiers[t];
            list.Clear();
            if (tier != null)
            {
                if (tier.renderers != null)
                    foreach (var r in tier.renderers) if (r != null && seen.Add(r)) list.Add(r);
                if (tier.lodGroups != null)
                    foreach (var g in tier.lodGroups)
                    {
                        if (g == null) continue;
                        foreach (var lod in g.GetLODs())
                            if (lod.renderers != null)
                                foreach (var r in lod.renderers) if (r != null && seen.Add(r)) list.Add(r);
                    }
            }
            var run = new Runtime { r = list.ToArray() };
            run.pos = new Vector3[run.r.Length];
            run.hidden = new bool[run.r.Length];
            for (int i = 0; i < run.r.Length; i++)
            {
                var b = run.r[i].bounds;   // декор статичный: центр берём один раз
                run.pos[i] = b.size.sqrMagnitude > 0f ? b.center : run.r[i].transform.position;
            }
            rt[t] = run;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;
        Evaluate();
    }

    void Evaluate()
    {
        if (rt == null || captured) return;
        var player = PlayerMovement.Instance;
        if (player == null) { ShowAllAndReset(); snap = true; return; }   // меню и пустые сцены: ничего не прячем

        Vector3 leo = player.transform.position;
        bool reveal = CameraVibe.RevealActive || CameraVibe.RevealShown > 0.01f;
        Vector3 rp = reveal ? CameraVibe.RevealPoint : Vector3.zero;
        DrawDistanceLevel level = VibeSettings.DrawDistance;

        for (int t = 0; t < rt.Length; t++)
        {
            var run = rt[t];
            var tier = tiers[t];
            if (run == null || tier == null) continue;
            float limit = Mathf.Max(0f, tier.hideBeyond.Get(level));
            float hideAt = limit + hysteresis, showAt = Mathf.Max(0f, limit - hysteresis);
            float hideSq = hideAt * hideAt, showSq = showAt * showAt, snapSq = limit * limit;

            for (int i = 0; i < run.r.Length; i++)
            {
                var r = run.r[i];
                if (r == null) continue;   // объект уничтожен (рубка, сохранение): пропускаем
                Vector3 p = run.pos[i];
                float d = Sq(p, leo);
                if (reveal) d = Mathf.Min(d, Sq(p, rp));

                bool hide;
                if (snap) hide = d > snapSq;                     // первый проход и смена пресета: без гистерезиса
                else if (run.hidden[i]) hide = d > showSq;       // скрытый возвращается только внутри порога - запас
                else hide = d > hideSq;                          // видимый скрывается только за порогом + запас

                if (hide != run.hidden[i])
                {
                    run.hidden[i] = hide;
                    r.forceRenderingOff = hide;
                }
            }
        }
        snap = false;
    }

    static float Sq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    // начало и конец снимка миникарты: пока снимок идёт, всё видно, а флаги скрытия сохраняются
    void Capture(bool on)
    {
        captured = on;
        if (rt == null) return;
        for (int t = 0; t < rt.Length; t++)
        {
            var run = rt[t];
            if (run == null) continue;
            for (int i = 0; i < run.r.Length; i++)
                if (run.r[i] != null && run.hidden[i]) run.r[i].forceRenderingOff = !on;
        }
    }

    // показать всё и забыть флаги (нет Лео, компонент выключен)
    void ShowAllAndReset()
    {
        if (rt == null) return;
        for (int t = 0; t < rt.Length; t++)
        {
            var run = rt[t];
            if (run == null) continue;
            for (int i = 0; i < run.r.Length; i++)
            {
                if (run.r[i] != null && run.hidden[i]) run.r[i].forceRenderingOff = false;
                run.hidden[i] = false;
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (tiers == null) return;
        foreach (var t in tiers)
        {
            if (t == null) continue;
            t.hideBeyond.near = Mathf.Max(0f, t.hideBeyond.near);
            t.hideBeyond.medium = Mathf.Max(0f, t.hideBeyond.medium);
            t.hideBeyond.far = Mathf.Max(0f, t.hideBeyond.far);
        }
    }

    [ContextMenu("Заполнить ярусы из artRoot")]
    void AutoFillFromArtRoot()
    {
        Transform root = autoFillRoot;
        if (root == null)
        {
            var state = GetComponent<GroveArtState>();
            if (state != null) root = state.artRoot;
        }
        if (root == null) { Debug.LogWarning("[DrawDistanceGroup] Нет корня: назначь autoFillRoot или повесь компонент на объект с GroveArtState.", this); return; }

        UnityEditor.Undo.RecordObject(this, "DrawDistanceGroup autofill");
        var used = new HashSet<Renderer>();
        var perTier = new List<Renderer>[tiers.Length];
        for (int i = 0; i < perTier.Length; i++) perTier[i] = new List<Renderer>();

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SpriteRenderer)) continue;
            if (r.GetComponentInParent<GroveChopBlock>() != null) continue;   // рубка и интерактив не трогаем
            if (r.GetComponentInParent<LandmarkTree>() != null) continue;     // ориентиры и деревья-хранители не трогаем
            string path = PathUnder(r.transform, root).ToLowerInvariant();
            for (int t = 0; t < tiers.Length; t++)
            {
                if (tiers[t] == null || !Matches(path, tiers[t].autoFillKeywords)) continue;
                if (used.Add(r)) perTier[t].Add(r);
                break;
            }
        }
        for (int t = 0; t < tiers.Length; t++)
            if (tiers[t] != null) tiers[t].renderers = perTier[t].ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[DrawDistanceGroup] Заполнено: " + string.Join(", ", Array.ConvertAll(tiers, x => x == null ? "-" : x.name + " = " + x.renderers.Length)) + ". Проверь списки в Inspector.", this);
    }

    static string PathUnder(Transform t, Transform root)
    {
        string s = t.name;
        for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static bool Matches(string path, string keywords)
    {
        if (string.IsNullOrEmpty(keywords)) return false;
        foreach (var k in keywords.Split(','))
        {
            string key = k.Trim().ToLowerInvariant();
            if (key.Length > 0 && path.Contains(key)) return true;
        }
        return false;
    }
#endif
}
