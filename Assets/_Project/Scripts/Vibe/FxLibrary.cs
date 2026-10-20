// Assets/_Project/Scripts/Vibe/FxLibrary.cs (НЕ в Editor)
// Список готовых префабов эффектов (например, из Cartoon FX Remaster Free) для ключевых моментов игры.
// Ассет лежит в Assets/_Project/Resources/FxLibrary.asset, его создаёт меню Tools > BuilderSim > Link Cartoon FX.
// Пустой слот = ImpactFx рисует свой эффект кодом. Заполненный слот = играет префаб.
// Слоты можно менять руками в Инспекторе: просто перетащи любой префаб эффекта.
using UnityEngine;

[CreateAssetMenu(fileName = "FxLibrary", menuName = "BuilderSim/Fx Library")]
public class FxLibrary : ScriptableObject
{
    [Header("Префабы эффектов (пусто = эффект из кода)")]
    public GameObject chopHit;     // удар топора по дереву
    public GameObject treeFall;    // дерево упало
    public GameObject levelUp;     // новый уровень
    public GameObject questDone;   // выполнено задание
    public GameObject campfire;    // огонь костра (живёт, пока горит стадия)
    public GameObject stageBuilt;  // стадия постройки готова
    public GameObject buildDone;   // постройка полностью завершена (пусто = берётся stageBuilt)

    [Header("Общее")]
    [Range(0.2f, 3f)] public float scale = 1f;

    static FxLibrary inst;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; loaded = false; }

    public static FxLibrary Get()
    {
        if (!loaded) { loaded = true; inst = Resources.Load<FxLibrary>("FxLibrary"); }
        return inst;
    }

    // разовый эффект в точке; сам удаляется через life секунд
    public static GameObject Spawn(GameObject prefab, Vector3 pos, float scaleMul = 1f, float life = 4f)
    {
        if (prefab == null) return null;
        var lib = Get();
        float s = (lib != null ? lib.scale : 1f) * scaleMul;
        var go = Object.Instantiate(prefab, pos, prefab.transform.rotation);
        go.transform.localScale = prefab.transform.localScale * s;
        Object.Destroy(go, life);
        return go;
    }
}
