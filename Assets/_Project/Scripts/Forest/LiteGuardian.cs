// Assets/_Project/Scripts/Forest/LiteGuardian.cs (НЕ в Editor)  НОВЫЙ ФАЙЛ
// «Лёгкие хранители»: дешёвые нерубимые деревья-кулисы вместо старых фоновых Decor_* (кедры/дубы-стена).
// Вешает ForestGuardLite.cs (Editor) на корень префаба Lite_<Вид> (крупные: кольцо с рунами + кристалл) и LiteS_<Вид> (мелкие: кристалл без кольца).
// Руками ничего настраивать не нужно.
//
// Чем дешевле настоящих хранителей (SacredVibe):
//   • никаких частиц, никаких Light и FireSource, никаких MaterialPropertyBlock;
//   • один общий драйвер на ВСЕ лёгкие деревья (один Update на сцену, а не по одному на дерево);
//   • дальше effectDistance от камеры дерево не обновляется вообще (стоит в последней позе);
//   • «Уменьшить эффекты» (VibeSettings.ReduceEffects): покачивания нет, кристалл не вращается.
// Что делает: лёгкое покачивание кроны (наклон visual), вращение и покачивание кристалла-знака.
// Срубить нельзя технически: на префабе нет тега Resource и нет ResourceItem. E-взаимодействия тоже нет (это кулисы, а не достопримечательность).
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LiteGuardian : MonoBehaviour
{
    public enum Tier { Large = 0, Small = 1 }

    [Header("Вид (заполняет ForestGuardLite.cs)")]
    public Tier tier = Tier.Large;
    public SacredKind kind = SacredKind.Cloud;

    [Header("Части")]
    [Tooltip("Дочерний объект с мешем дерева: покачивается")]
    public Transform visual;
    [Tooltip("Парящий кристалл-знак: вращается и чуть покачивается")]
    public Transform gem;
    [Tooltip("Высота кристалла над землёй в локальных координатах префаба, м")]
    public float gemHeight = 8f;

    [Header("Живость")]
    [Tooltip("Покачивание кроны, градусы (у мелких чуть сильнее: они лёгкие)")]
    public float swayDeg = 0.6f;
    [Tooltip("Вращение кристалла, градусов в секунду")]
    public float gemSpin = 28f;
    [Tooltip("Амплитуда покачивания кристалла по высоте, м")]
    public float gemBob = 0.12f;
    [Tooltip("Дальше этого расстояния от камеры дерево не обновляется, м")]
    public float effectDistance = 45f;

    [Header("Полянка")]
    [Tooltip("Радиус чистой полянки вокруг ствола, м: подлесок и рубимые деревья сюда не лезут (читает ChopZone.Avoid и ForestGroves)")]
    public float clearRadius = 4.2f;

    static readonly List<LiteGuardian> All = new List<LiteGuardian>(64);
    static Driver driver;

    float phase;
    Quaternion visualRest = Quaternion.identity;
    Vector3 gemRest;
    bool rested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); driver = null; }

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;   // в редакторе вне Play ничего не крутим
#endif
        if (!rested)
        {
            if (visual != null) visualRest = visual.localRotation;
            if (gem != null) gemRest = gem.localPosition;
            // фаза от позиции: соседние деревья качаются не в такт, и при каждом запуске одинаково
            phase = Mathf.Abs(Mathf.Sin(transform.position.x * 12.9898f + transform.position.z * 78.233f)) * 6.2832f;
            rested = true;
        }
        if (!All.Contains(this)) All.Add(this);
        if (driver == null)
        {
            var go = new GameObject("LiteGuardianDriver") { hideFlags = HideFlags.HideAndDontSave };
            driver = go.AddComponent<Driver>();
        }
    }

    void OnDisable() { All.Remove(this); }

    sealed class Driver : MonoBehaviour
    {
        void Update()
        {
            if (All.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 cp = cam.transform.position;
            float t = Time.time;
            bool calm = VibeSettings.ReduceEffects;
            for (int i = 0; i < All.Count; i++)
            {
                var g = All[i];
                if (g == null) continue;
                Vector3 d = g.transform.position - cp;
                float lim = g.effectDistance;
                if (d.sqrMagnitude > lim * lim) continue;
                g.Tick(t, calm);
            }
        }
    }

    void Tick(float t, bool calm)
    {
        if (calm) return;
        if (visual != null && swayDeg > 0f)
        {
            float a = Mathf.Sin(t * 0.55f + phase) * swayDeg;
            float b = Mathf.Sin(t * 0.43f + phase * 1.7f) * swayDeg * 0.7f;
            visual.localRotation = visualRest * Quaternion.Euler(a, 0f, b);
        }
        if (gem != null)
        {
            gem.localRotation = Quaternion.Euler(0f, t * gemSpin + phase * 20f, 0f);
            gem.localPosition = gemRest + new Vector3(0f, Mathf.Sin(t * 0.9f + phase) * gemBob, 0f);
        }
    }
}
