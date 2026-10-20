// Assets/_Project/Scripts/Interaction/InteractionHint.cs (НЕ в Editor)
// Вешается на игрока: когда рядом дерево, над ним появляется «Зажми E, чтобы рубить» (+ строка «и жми ПРОБЕЛ в зелёной зоне», пока игрок не освоил бонус) или «Нажми E, чтобы рубить» (ритм-режим).
// Если рюкзак полон, вместо этого пишет «Рюкзак полон». Пока игрок рубит, подсказка прячется.
// На ПК в подсказке «E / ЛКМ»: дерево можно рубить и мышью.
// Дерево берётся из PlayerInteraction, поэтому подсказка всегда над тем деревом, которое реально будет рубиться.
// Когда клавишей E владеет InteractionHub (идёт разговор или рядом NPC), подсказка про дерево молчит.
using UnityEngine;

public class InteractionHint : MonoBehaviour
{
    [Header("Подсказка")]
    public string text = "Зажми <color=#6BCB5A>{key}</color>, чтобы рубить";   // режим «зажми E»; {key} подставится сам
    public string textRhythm = "Нажми <color=#6BCB5A>{key}</color>, чтобы рубить";   // ритм-рубка (по умолчанию)
    [Tooltip("Вторая строка в режиме «зажми E», пока игрок не освоил бонус (см. GameBalance.ChopBonusTeachHits)")]
    public string textBonusLine = "<size=70%><color=#AEB9D6>и жми</color> <color=#F2A93B>ПРОБЕЛ</color> <color=#AEB9D6>в зелёной зоне</color></size>";
    public string fullText = "<color=#FF8A7A>Рюкзак полон</color>";
    public Vector3 offset = new Vector3(0f, 3.5f, 0f);

    [Header("Запасной поиск (если на игроке нет PlayerInteraction)")]
    public float radius = 2.5f;
    public string resourceTag = "Resource";
    public LayerMask mask = ~0;

    WorldTag worldTag;
    BackpackTag backpack;
    PlayerInteraction interaction;
    Transform holder;
    readonly Collider[] buffer = new Collider[48];
    bool placed;
    bool lastFull;
    bool lastRhythm;
    bool lastTeach;
    int lastFree = -1, lastWant = -1;   // сколько влезет / сколько у дерева: надпись «влезет +2 из 4» обновляется при смене

    void Start()
    {
        backpack = GetComponent<BackpackTag>();
        interaction = GetComponent<PlayerInteraction>();

        var go = new GameObject("InteractionHint_Tag");
        holder = go.transform;
        worldTag = go.AddComponent<WorldTag>();
        worldTag.offset = offset;
        lastRhythm = PlayerInteraction.RhythmChop;
        lastTeach = PlayerInteraction.BonusTeaching;
        worldTag.SetText(Fmt(MainText()));
        worldTag.visible = false;
    }

    // «Зажми E, чтобы рубить» + вторая строка про Пробел, пока игрок не освоил бонус
    string MainText()
    {
        if (PlayerInteraction.RhythmChop) return textRhythm;
        return PlayerInteraction.BonusTeaching ? text + "\n" + textBonusLine : text;
    }

    KeyCode Key { get { return interaction != null ? interaction.chopKey : KeyCode.E; } }

    string Fmt(string s)
    {
        string k = Key.ToString();
        if (k.StartsWith("Alpha")) k = k.Substring(5);
        // на ПК рубить можно и мышью (PlayerInteraction.mouseChop): в подсказке «E / ЛКМ»
        if (interaction != null && interaction.mouseChop && !Application.isMobilePlatform && Input.mousePresent) k += " / ЛКМ";
        return s.Replace("{key}", k);
    }

    Transform FindByOverlap()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, mask, QueryTriggerInteraction.Collide);
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = buffer[i];
            if (c == null || !c.CompareTag(resourceTag)) continue;
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = c.transform; }
        }
        return best;
    }

    void Update()
    {
        if (worldTag == null) return;

        // E занята хабом (разговор или NPC ближе, чем дерево): подсказку про дерево не показываем
        bool owned = InteractionHub.OwnsInput;

        Transform best = null;
        if (!owned)
        {
            if (interaction != null) best = interaction.CurrentTarget != null ? interaction.CurrentTarget.transform : null;
            else best = FindByOverlap();
        }

        bool full = backpack != null && backpack.NoRoom;   // места нет совсем. Один свободный слот: рубить можно, но предупредим «влезет +1 из 4»
        bool rhythm = PlayerInteraction.RhythmChop;
        bool teach = PlayerInteraction.BonusTeaching;
        int want = (best != null && interaction != null) ? interaction.WoodFull : 0;
        int free = backpack != null ? backpack.Free : 0;
        if (full != lastFull || rhythm != lastRhythm || teach != lastTeach || want != lastWant || free != lastFree)
        {
            bool fullChanged = full != lastFull;
            lastFull = full;
            lastRhythm = rhythm;
            lastTeach = teach;
            lastWant = want;
            lastFree = free;
            string main = Fmt(MainText());
            if (!full && want > free && free > 0)
                main += "\n<size=70%><color=#F2A93B>влезет +" + free + " из " + want + "</color></size>";
            worldTag.SetText(full ? Fmt(fullText) : main);
            if (full && fullChanged) worldTag.Punch(0.25f);
        }

        // нажал рубить с полным рюкзаком: табличка дёргается
        if (best != null && full && MobileInput.ActionDown) worldTag.Punch(0.25f);

        bool chopping = interaction != null ? interaction.IsChopping : (!full && MobileInput.ActionHeld);
        worldTag.visible = best != null && !chopping;

        if (best != null)
        {
            if (!placed) { holder.position = best.position; placed = true; }
            else holder.position = Vector3.Lerp(holder.position, best.position, 1f - Mathf.Exp(-14f * Time.deltaTime));
        }
        else
        {
            placed = false; // следующее дерево табличка займёт сразу, а не поедет со старого места
        }
    }

    void OnDestroy()
    {
        if (holder != null) Destroy(holder.gameObject);
    }
}