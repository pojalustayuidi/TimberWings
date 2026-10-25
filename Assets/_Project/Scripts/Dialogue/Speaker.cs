// Assets/_Project/Scripts/Dialogue/Speaker.cs (НЕ в Editor)
// «Тот, кто говорит»: имя, цвет имени, точка над головой, куда ставится пузырь, голос.
// Вешается на Лео (игрока) и на каждого NPC. NPC при желании поворачивается лицом к игроку.
using System.Collections.Generic;
using UnityEngine;

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

    static readonly List<Speaker> all = new List<Speaker>();

    AudioSource voice;
    Transform player;
    float findTimer;

    public Vector3 BubblePoint { get { return transform.position + bubbleOffset; } }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() { all.Remove(this); }

    public static Speaker Find(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var s in all)
            if (s != null && string.Equals(s.displayName, name, System.StringComparison.OrdinalIgnoreCase)) return s;
        return null;
    }

    // токен затухания: каждый новый PlayVoice / StopVoice / FadeOutVoice делает старое затухание недействительным
    int fadeToken;
    float fadeGain = 1f;     // 1 = голос на полную, 0 = затух; итоговая громкость = fadeGain * GameAudio.VoiceLevel

    public void PlayVoice(AudioClip clip)
    {
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
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (s != null && s != except) s.StopVoice();
        }
    }

    void Update()
    {
        // ползунок «Голоса» и «Звук ВЫКЛ» действуют на реплику, которая звучит прямо сейчас
        if (voice != null && voice.isPlaying) voice.volume = fadeGain * GameAudio.VoiceLevel;

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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = nameColor;
        Gizmos.DrawWireSphere(BubblePoint, 0.15f);
    }
}