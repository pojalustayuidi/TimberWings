// Assets/_Project/Scripts/Forest/WingForest.cs (НЕ в Editor)  НОВЫЙ ФАЙЛ
// «Кулисные деревья»: один компонент на ВЕСЬ лес кулис. Лежит на корне IslandV2/_Manual/ForestWings (его вешает ForestWings.cs, руками ничего не настраивать).
// На самих деревьях НИКАКИХ компонентов нет (только меши, LODGroup и капсульный коллайдер ствола): это и есть «лёгкий рантайм».
//
// Что делает:
//   • при включении один раз обходит детей корня и запоминает у каждого дерева дочерний объект Visual (крона и ствол без венка из камней);
//   • в одном Update слегка качает Visual каждого дерева возле камеры (наклон на доли градуса), у дальних реже и вообще не трогает дальше effectDistance;
//   • никаких частиц, Light, MaterialPropertyBlock и аллокаций в кадре. Ветер в вершинах (кроны и пряди ивы) делает сам шейдер TreeToon, он и так бесплатный.
// «Уменьшить эффекты» (VibeSettings.ReduceEffects): покачивания нет, остаётся только ветер шейдера.
// Руками можно двигать, удалять и дублировать деревья: список строится при запуске игры. Для деревьев, добавленных во время игры: ПКМ на компоненте, «Пересканировать».
// Срубить кулису нельзя технически: на префабах нет тега Resource и нет ResourceItem.
using UnityEngine;

[DisallowMultipleComponent]
public class WingForest : MonoBehaviour
{
    [Header("Живость")]
    [Tooltip("Максимальный наклон кроны, градусы (умножается на множитель вида: ива и лента качаются сильнее, гриб слабее)")]
    public float swayDeg = 1.1f;
    [Tooltip("Дальше этого расстояния от камеры деревья не обновляются, м")]
    public float effectDistance = 45f;
    [Tooltip("Ближе этого расстояния обновляем каждый кадр, дальше через кадр, м")]
    public float nearDistance = 24f;

    struct Item
    {
        public Transform visual;
        public Quaternion rest;
        public float phase, amp;
    }

    Item[] items = new Item[0];
    int count;
    Camera cam;
    bool calm;
    int calmCheckFrame;

    void OnEnable() { Scan(); }

    [ContextMenu("Пересканировать")]
    public void Scan()
    {
        int n = transform.childCount;
        if (items.Length < n) items = new Item[n];
        count = 0;
        for (int i = 0; i < n; i++)
        {
            Transform t = transform.GetChild(i);
            Transform v = t.Find("Visual");
            if (v == null) continue;
            float amp = AmpOf(t.name);
            if (amp <= 0f) continue;   // куст: качает только шейдер
            Vector3 p = t.position;
            items[count].visual = v;
            items[count].rest = v.localRotation;
            items[count].amp = amp;
            // фаза от позиции: соседние деревья качаются не в такт, и при каждом запуске одинаково
            items[count].phase = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f)) * 6.2832f;
            count++;
        }
        calm = VibeSettings.ReduceEffects;
    }

    // множитель покачивания по виду (по префиксу имени Wing_<Вид><форма>_<номер>)
    static float AmpOf(string n)
    {
        if (n.StartsWith("Wing_Bush")) return 0f;
        if (n.StartsWith("Wing_Willow")) return 1.0f;
        if (n.StartsWith("Wing_Ribbon")) return 0.9f;
        if (n.StartsWith("Wing_Cotton")) return 0.8f;
        if (n.StartsWith("Wing_Moon")) return 0.7f;
        if (n.StartsWith("Wing_Cloud")) return 0.6f;
        if (n.StartsWith("Wing_Mushroom")) return 0.35f;
        return 0.6f;
    }

    void Update()
    {
        if (count == 0) return;
        // настройку читаем раз в секунду-две: PlayerPrefs каждый кадр дорог
        if (Time.frameCount - calmCheckFrame > 90) { calmCheckFrame = Time.frameCount; calm = VibeSettings.ReduceEffects; }
        if (calm) return;
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector3 cp = cam.transform.position;
        float t = Time.time;
        float lim2 = effectDistance * effectDistance, near2 = nearDistance * nearDistance;
        int parity = Time.frameCount & 1;
        for (int i = 0; i < count; i++)
        {
            var v = items[i].visual;
            if (v == null) continue;
            Vector3 d = v.position - cp;
            float d2 = d.sqrMagnitude;
            if (d2 > lim2) continue;
            if (d2 > near2 && (i & 1) != parity) continue;   // дальние через кадр: движение медленное, не заметно
            float ph = items[i].phase, k = swayDeg * items[i].amp;
            float a = Mathf.Sin(t * 0.55f + ph) * k;
            float b = Mathf.Sin(t * 0.43f + ph * 1.7f) * k * 0.7f;
            v.localRotation = items[i].rest * Quaternion.Euler(a, 0f, b);
        }
    }
}
