// Assets/_Project/Scripts/Pets/PetWorld.cs (НЕ в Editor)
// Связывает данные и мир: следит за PetManager и держит рядом с игроком моделей по экипированным питомцам.
// Ставить на сцену не нужно: объект PetWorld создаётся сам после загрузки сцены (если на сцене есть PetManager).
// Стадии роста: если у PetData нет growthPrefabs, берётся prefab, а размер меняется множителем StageScale.
using UnityEngine;

public class PetWorld : MonoBehaviour
{
    // малыш, подросток, взрослый (применяется, только если нет отдельных префабов стадий)
    static readonly float[] StageScale = { 0.6f, 0.8f, 1f };

    class Slot
    {
        public int uid = -1;
        public int stage = -1;
        public PetFollower follower;
    }

    readonly Slot[] slots = new Slot[PetManager.MaxSlots];
    Transform player;
    bool dirty = true;
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<PetWorld>() != null) return;
        new GameObject("PetWorld").AddComponent<PetWorld>();
    }

    void Awake()
    {
        for (int i = 0; i < slots.Length; i++) slots[i] = new Slot();
    }

    void OnEnable()
    {
        PetManager.Changed += MarkDirty;
        PetManager.GrowthStageChanged += OnStage;
    }

    void OnDisable()
    {
        PetManager.Changed -= MarkDirty;
        PetManager.GrowthStageChanged -= OnStage;
    }

    void MarkDirty() { dirty = true; }
    void OnStage(int stage) { dirty = true; }

    void Update()
    {
        if (PetManager.Instance == null) return;

        // игрок мог появиться позже или пересоздаться: ищем раз в полсекунды
        if (player == null)
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            PlayerMovement pm = PlayerMovement.Instance;
            if (pm == null) return;
            player = pm.transform;
            dirty = true;
        }

        // подстраховка: раз в секунду сверяемся, даже если событие пропущено
        if (!dirty && Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 1f;
            dirty = true;
        }

        if (dirty)
        {
            dirty = false;
            Sync();
        }
    }

    void Sync()
    {
        PetManager m = PetManager.Instance;
        if (m == null || player == null) return;

        int stage = m.GrowthStage;

        for (int i = 0; i < slots.Length; i++)
        {
            PetInstance inst = m.IsSlotOpen(i) ? m.GetEquipped(i) : null;
            int uid = inst != null ? inst.uid : -1;
            Slot s = slots[i];

            bool same = uid == s.uid && stage == s.stage && (uid < 0 || s.follower != null);
            if (same) continue;

            Clear(s);
            s.uid = uid;
            s.stage = stage;
            if (inst == null || inst.data == null) continue;

            GameObject prefab = inst.data.PrefabForStage(stage);
            if (prefab == null) continue;

            bool ownStagePrefabs = inst.data.growthPrefabs != null && inst.data.growthPrefabs.Length > 0;
            float mul = ownStagePrefabs ? 1f : StageScale[Mathf.Clamp(stage, 0, StageScale.Length - 1)];

            GameObject go = Instantiate(prefab, SpawnPos(i), player.rotation);
            go.name = "Pet_" + inst.data.id + "_slot" + i;
            go.transform.localScale = prefab.transform.localScale * mul;

            PetFollower f = go.GetComponent<PetFollower>();
            if (f == null) f = go.AddComponent<PetFollower>();
            f.Init(player, i);
            s.follower = f;
        }
    }

    Vector3 SpawnPos(int slot)
    {
        Vector3 fwd = player.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float side = slot == 0 ? -1f : (slot == 1 ? 1f : (slot == 3 ? 1.8f : 0f));
        return player.position - fwd * (slot == 3 ? 2.6f : 1.5f) + right * side;
    }

    static void Clear(Slot s)
    {
        if (s.follower != null) Destroy(s.follower.gameObject);
        s.follower = null;
        s.uid = -1;
        s.stage = -1;
    }

    void OnDestroy()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i].follower != null) Destroy(slots[i].follower.gameObject);
    }
}
