// Assets/_Project/Scripts/Pets/PetPortrait.cs (НЕ в Editor)
// Снимки питомцев для карточек: каждый вид один раз «фотографируется» в мини-студии (PetStage) и кэшируется как Sprite.
// Ставить на сцену не нужно: объект создаётся сам и после старта игры по одному в кадр снимает всех питомцев из базы.
// PetCardView берёт снимок через PetPortrait.Get(PetData). Пока снимка нет, карточка показывает старую иконку.
// Выключить 3D-снимки совсем: PetPortrait.Enabled = false (вернутся иконки PetData.icon).
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetPortrait : MonoBehaviour
{
    public static bool Enabled = true;

    const int Size = 256;

    static PetPortrait inst;
    static bool preloaded;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly HashSet<string> failed = new HashSet<string>();
    static readonly HashSet<string> queued = new HashSet<string>();
    static readonly Queue<PetData> queue = new Queue<PetData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        inst = null;
        preloaded = false;
        cache.Clear();
        failed.Clear();
        queued.Clear();
        queue.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Ensure(); }

    static void Ensure()
    {
        if (inst != null) return;
        var go = new GameObject("PetPortrait");
        inst = go.AddComponent<PetPortrait>();
    }

    static string Key(PetData d) { return string.IsNullOrEmpty(d.id) ? d.name : d.id; }

    // Готовый снимок или null (тогда он ставится в очередь, при следующем Set карточки он уже будет)
    public static Sprite Get(PetData d)
    {
        if (!Enabled || d == null) return null;
        string k = Key(d);

        Sprite s;
        if (cache.TryGetValue(k, out s) && s != null) return s;
        if (failed.Contains(k)) return null;

        Enqueue(d);
        return null;
    }

    static void Enqueue(PetData d)
    {
        if (queued.Add(Key(d))) queue.Enqueue(d);
        Ensure();
    }

    IEnumerator Start()
    {
        while (true)
        {
            // как только появился менеджер с базой, ставим в очередь всех питомцев
            PetManager m = PetManager.Instance;
            if (!preloaded && m != null && m.database != null)
            {
                preloaded = true;
                for (int i = 0; i < m.database.pets.Count; i++)
                    if (m.database.pets[i] != null) Enqueue(m.database.pets[i]);
            }

            if (queue.Count > 0) yield return StartCoroutine(Render(queue.Dequeue()));
            else yield return null;
        }
    }

    IEnumerator Render(PetData d)
    {
        string k = Key(d);
        RenderTexture rt = PetStage.MakeRT(Size);
        PetStageRig rig = PetStage.Build(d, rt);
        if (rig == null)
        {
            failed.Add(k);     // у питомца нет префаба: повторять незачем
            rt.Release();
            Destroy(rt);
            yield break;
        }

        rig.cam.enabled = true;
        yield return null;                           // кадр, чтобы Animator поставил позу
        yield return new WaitForEndOfFrame();        // камера уже нарисовала в rt

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        cache[k] = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);

        rig.cam.enabled = false;
        rig.Destroy();
        rt.Release();
        Destroy(rt);
    }
}
