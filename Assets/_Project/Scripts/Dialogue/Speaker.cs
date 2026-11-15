// Assets/_Project/Scripts/Dialogue/Speaker.cs (НЕ в Editor)
// «Тот, кто говорит»: имя, цвет имени, точка над головой, куда ставится пузырь, голос.
// Вешается на Лео (игрока) и на каждого NPC. NPC при желании поворачивается лицом к игроку.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(450)]
public class Speaker : MonoBehaviour
{
    [Header("Кто это")]
    public string displayName = "Персонаж";
    public Color nameColor = new Color32(0xF5, 0xC5, 0x42, 255);

    [Header("Пузырь")]
    [Tooltip("Куда ставится кончик пузыря относительно объекта (над макушкой)")]
    public Vector3 bubbleOffset = new Vector3(0f, 2.6f, 0f);

    [Header("Поворот к игроку (для NPC)")]
    public bool faceNearbyPlayer = false;
    public Transform turnRoot;            // что поворачивать (модель). Пусто = этот объект
    public float faceRadius = 5f;
    public float turnSpeed = 6f;
    [System.NonSerialized] public bool externalFacing;
    [Tooltip("Скорость доворота к собеседнику во время диалога, градусов/с")]
    public float dialogueTurnDegrees = 320f;
    [Tooltip("Ограничение реальной скорости разговора: старые NPC с 720 градусов/с не щёлкают головой и корпусом")]
    [Range(160f, 540f)] public float maxConversationTurn = 340f;
    [Header("Stage 4.1 — плавный разворот в диалоге")]
    [Tooltip("Плавность старта и торможения поворота к собеседнику")]
    [Range(1f, 12f)] public float conversationTurnEase = 4.5f;

    static readonly List<Speaker> all = new List<Speaker>();

    AudioSource voice;
    Transform player;
    float findTimer;
    Quaternion dialogueRotation;
    Quaternion beforeConversationRotation;
    bool facingConversation,returningFromConversation;
    Speaker lastFacingPartner;
    MiraRig miraFaceRig;

    // Mira has a procedural visual root driven by MiraMotion. Turning the NPC
    // scene root instead of that visual rotates prompts and other components,
    // while MiraMotion can restore a different local orientation each frame.
    // Use the real visible model for dialogues, without changing authored pivots.
    Transform DialogueVisual(PlayerMovement movement)
    {
        if (movement == null)
        {
            if (miraFaceRig == null) miraFaceRig = GetComponentInChildren<MiraRig>(true);
            if (miraFaceRig != null && miraFaceRig.gameObject.activeInHierarchy)
                return miraFaceRig.transform;
        }
        return turnRoot != null ? turnRoot : movement != null ? movement.model : transform;
    }

    // A prefab's transform.forward does not always coincide with the face.
    // Mira's eyes are generated in front of her head (+Z in the model data);
    // measuring them also works if her visible mesh was authored facing 180°
    // away from the NPC's root. No prefab or inspector changes required.
    Quaternion FaceTargetRotation(Transform visual, Vector3 towards)
    {
        Quaternion target = Quaternion.LookRotation(towards);
        if (miraFaceRig == null || !miraFaceRig.gameObject.activeInHierarchy ||
            miraFaceRig.head == null || miraFaceRig.eyeL == null || miraFaceRig.eyeR == null)
            return target;

        Vector3 eyeMidpoint = (miraFaceRig.eyeL.position + miraFaceRig.eyeR.position) * 0.5f;
        Vector3 face = visual.InverseTransformDirection(eyeMidpoint - miraFaceRig.head.position);
        face.y = 0f;
        if (face.sqrMagnitude < 0.0001f) return target;
        float faceYaw = Vector3.SignedAngle(Vector3.forward, face.normalized, Vector3.up);
        return target * Quaternion.Euler(0f, -faceYaw, 0f);
    }

    public Vector3 BubblePoint { get { return transform.position + bubbleOffset; } }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable()
    {
        all.Remove(this);
        StopVoice(); // disabling only this component must not leave an orphan recorded line playing
        facingConversation = returningFromConversation = false;
        lastFacingPartner = null;
    }

    public static Speaker Find(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var s in all)
            if (s != null && s.isActiveAndEnabled && string.Equals(s.displayName, name, System.StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    public static bool TryGetConversationPartner(Speaker actor,out Speaker partner)
    {
        if(DialogueSequence.TryGetFacingTarget(actor,out partner))return true;
        partner=null;
        if(actor==null||DialogueSequence.AnyPlaying||DialogueManager.CurrentSpeaker!=actor||actor.GetComponent<PlayerMovement>()!=null)return false;
        var movement=PlayerMovement.Instance;
        if(movement==null)return false;
        partner=movement.GetComponent<Speaker>();
        return partner!=null&&partner.isActiveAndEnabled;
    }

    // токен затухания: каждый новый PlayVoice / StopVoice / FadeOutVoice делает старое затухание недействительным
    int fadeToken;
    float fadeGain = 1f;     // 1 = голос на полную, 0 = затух; итоговая громкость = fadeGain * GameAudio.VoiceLevel

    public bool IsSpeaking { get { return voice != null && voice.isPlaying && GameAudio.UseRecordedVoices && GameAudio.VoiceLevel > .001f; } }

    public void PlayVoice(AudioClip clip)
    {
        if (!GameAudio.UseRecordedVoices) { StopVoice(); return; }
        if (clip == null) return;
        if (voice == null)
        {
            voice = gameObject.GetComponent<AudioSource>();
            if (voice == null) voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;   // реплики слышны одинаково, где бы ни стоял игрок
        }
        fadeToken++;
        fadeGain = 1f;
        voice.volume = GameAudio.VoiceLevel;
        voice.Stop();
        voice.clip = clip;
        voice.Play();
    }

    // плавное затухание вместо резкого обрыва: так пропуск реплики звучит аккуратно
    public void FadeOutVoice(float time)
    {
        if (voice == null || !voice.isPlaying) return;
        if (!isActiveAndEnabled || time <= 0.01f) { StopVoice(); return; }
        StartCoroutine(FadeRoutine(time, ++fadeToken));
    }

    System.Collections.IEnumerator FadeRoutine(float time, int token)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            if (fadeToken != token || voice == null) yield break;     // началась новая реплика или голос остановили
            fadeGain = 1f - t / time;
            voice.volume = fadeGain * GameAudio.VoiceLevel;
            yield return null;
        }
        if (voice != null && fadeToken == token) { voice.Stop(); fadeGain = 1f; voice.volume = GameAudio.VoiceLevel; }
    }

    public void StopVoice()
    {
        fadeToken++;
        fadeGain = 1f;
        if (voice == null) return;
        voice.Stop();
        voice.volume = GameAudio.VoiceLevel;
    }

    // перед новой репликой глушим голоса всех остальных: две озвучки одновременно быть не может
    public static void StopAllVoices(Speaker except)
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            var s = all[i];
            if (s == null) { all.RemoveAt(i); continue; }
            if (s != except) s.StopVoice();
        }
    }

    void Update()
    {
        // ползунок «Голоса» и «Звук ВЫКЛ» действуют на реплику, которая звучит прямо сейчас
        if (voice != null && voice.isPlaying)
        { if (!GameAudio.UseRecordedVoices || GameAudio.VoiceLevel <= .001f) StopVoice(); else voice.volume = fadeGain * GameAudio.VoiceLevel; }

        // The dialogue override is applied after NPC animation scripts. Do not
        // also steer this model toward the player from Update.
        if (externalFacing) return;
        Speaker addressee;
        if (TryGetConversationPartner(this,out addressee) || returningFromConversation) return;
        if (!faceNearbyPlayer) return;

        if (player == null)
        {
            findTimer -= Time.deltaTime;
            if (findTimer > 0f) return;
            findTimer = 0.5f;
            var pm = PlayerMovement.Instance;
            if (pm != null) player = pm.transform;
            return;
        }

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        float sq = d.sqrMagnitude;
        if (sq < 0.01f || sq > faceRadius * faceRadius) return;

        var t = turnRoot != null ? turnRoot : transform;
        t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }

    void LateUpdate()
    {
        if (externalFacing) return;
        // Mira/Oskar pose their heads and torsos in LateUpdate. Override the
        // visual root *after* those poses, so a head-yaw limit cannot leave a
        // speaker looking 60-120 degrees away from their partner.
        var movement=GetComponent<PlayerMovement>();
        Transform visual=DialogueVisual(movement);
        // Never rotate the player's Rigidbody root merely for eye contact.
        if(visual==null||movement!=null&&visual==transform)return;
        Speaker partner = null;
        bool faceLocalPlayer = miraFaceRig != null && DialogueSequence.Focus == transform &&
            PlayerMovement.Instance != null;
        if (faceLocalPlayer || TryGetConversationPartner(this, out partner))
        {
            // In Mira's player-initiated conversation, face the person standing
            // in front of her, even if a dialogue line refers to another NPC.
            Vector3 direction = faceLocalPlayer
                ? PlayerMovement.Instance.transform.position - transform.position
                : partner.transform.position - transform.position;
            direction.y=0;
            if(direction.sqrMagnitude>.0001f)
            {
                if (!facingConversation)
                {
                    beforeConversationRotation = visual.rotation;
                    facingConversation = true;
                }
                if (lastFacingPartner != partner)
                {
                    // A new speaker can take over within the same conversation.
                    // Rebase interpolation to the current orientation, not the
                    // target of the previous NPC.
                    dialogueRotation = visual.rotation;
                    lastFacingPartner = partner;
                }
                returningFromConversation=false;
                float turnDt = Mathf.Min(Time.deltaTime, .1f);
                float maxStep = Mathf.Min(Mathf.Max(120f, dialogueTurnDegrees),
                    Mathf.Clamp(maxConversationTurn, 160f, 540f)) * turnDt;
                // Match the visible face, not an arbitrary root +Z axis.
                // Otherwise Mira can complete a perfectly smooth 180-degree
                // turn and end up with her back to Leo.
                Quaternion targetRotation = FaceTargetRotation(visual, direction);
                Quaternion easedTarget = Quaternion.Slerp(dialogueRotation, targetRotation,
                    1f - Mathf.Exp(-Mathf.Max(1f, conversationTurnEase) * turnDt));
                dialogueRotation=Quaternion.RotateTowards(dialogueRotation, easedTarget, maxStep);
                visual.rotation=dialogueRotation;
            }
        }
        else if(facingConversation||returningFromConversation)
        {
            // NPC procedural animators may have restored their authored root
            // this frame. Blend the final orientation back, without snapping.
            facingConversation=false;returningFromConversation=true;
            lastFacingPartner = null;
            // If this NPC is configured to watch the player, don't spin toward
            // its pre-dialogue heading and immediately spin back toward Leo.
            Quaternion normal = beforeConversationRotation;
            if (faceNearbyPlayer && player != null)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > .01f && toPlayer.sqrMagnitude <= faceRadius * faceRadius)
                    normal = FaceTargetRotation(visual, toPlayer);
            }
            else if (miraFaceRig == null && Quaternion.Angle(visual.rotation, dialogueRotation) >= .35f)
                normal = visual.rotation; // a non-Mira animator owns this orientation
            float turnDt = Mathf.Min(Time.deltaTime, .1f);
            float maxStep = Mathf.Min(Mathf.Max(120f, dialogueTurnDegrees),
                Mathf.Clamp(maxConversationTurn, 160f, 540f)) * turnDt;
            Quaternion easedTarget = Quaternion.Slerp(dialogueRotation, normal,
                1f - Mathf.Exp(-Mathf.Max(1f, conversationTurnEase) * turnDt));
            dialogueRotation = Quaternion.RotateTowards(dialogueRotation, easedTarget, maxStep);
            visual.rotation=dialogueRotation;
            if(Quaternion.Angle(dialogueRotation,normal)<.35f)returningFromConversation=false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = nameColor;
        Gizmos.DrawWireSphere(BubblePoint, 0.15f);
    }
}
