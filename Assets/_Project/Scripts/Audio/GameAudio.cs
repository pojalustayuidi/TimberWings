// Assets/_Project/Scripts/Audio/GameAudio.cs (НЕ в Editor)
// Единое место всех настроек звука. Окно настроек пишет сюда, а музыка, эффекты, голоса и природа читают отсюда
// каждый кадр, поэтому ползунок слышно сразу, без перезапуска.
//
//   Muted    - «Звук в игре: ВЫКЛ», глушит вообще всё
//   Master   - общая громкость
//   Music / Sfx / Voice / Ambient - громкость по группам
//   Babble   - бормотание реплик, когда у реплики нет озвучки
//
// *Level - итоговая громкость группы с учётом Muted и Master. Её и нужно умножать в своих AudioSource.
// Сохраняется в PlayerPrefs (ключи tw_vol_music / tw_vol_sfx / tw_vol_amb остались прежними, старые настройки не потеряются).
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameAudio
{
    const string KMuted = "tw_audio_muted";
    const string KMaster = "tw_vol_master";
    const string KMusic = "tw_vol_music";
    const string KSfx = "tw_vol_sfx";
    const string KVoice = "tw_vol_voice";
    const string KAmb = "tw_vol_amb";
    const string KBabble = "tw_babble";

    static bool loaded;
    static bool muted, babble = true;
    static float master = 1f, music = 1f, sfx = 0.8f, voice = 1f, amb = 0.5f;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { loaded = false; Changed = null; }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        muted = PlayerPrefs.GetInt(KMuted, 0) == 1;
        babble = PlayerPrefs.GetInt(KBabble, 1) == 1;
        master = Mathf.Clamp01(PlayerPrefs.GetFloat(KMaster, 1f));
        music = Mathf.Clamp01(PlayerPrefs.GetFloat(KMusic, 1f));
        sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KSfx, 0.8f));
        voice = Mathf.Clamp01(PlayerPrefs.GetFloat(KVoice, 1f));
        amb = Mathf.Clamp01(PlayerPrefs.GetFloat(KAmb, 0.5f));
    }

    static void Raise() { var h = Changed; if (h != null) h(); }

    // ------------------------------------------------------------ настройки (то, что видит игрок)
    public static bool Muted
    {
        get { Load(); return muted; }
        set { Load(); if (muted == value) return; muted = value; PlayerPrefs.SetInt(KMuted, value ? 1 : 0); Raise(); }
    }

    public static bool Babble
    {
        get { Load(); return babble; }
        set { Load(); if (babble == value) return; babble = value; PlayerPrefs.SetInt(KBabble, value ? 1 : 0); Raise(); }
    }

    public static float Master { get { Load(); return master; } set { Set(ref master, KMaster, value); } }
    public static float Music { get { Load(); return music; } set { Set(ref music, KMusic, value); } }
    public static float Sfx { get { Load(); return sfx; } set { Set(ref sfx, KSfx, value); } }
    public static float Voice { get { Load(); return voice; } set { Set(ref voice, KVoice, value); } }
    public static float Ambient { get { Load(); return amb; } set { Set(ref amb, KAmb, value); } }

    static void Set(ref float field, string key, float v)
    {
        Load();
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(field, v)) return;
        field = v;
        PlayerPrefs.SetFloat(key, v);
        Raise();
    }

    // ------------------------------------------------------------ итоговая громкость групп
    public static float MusicLevel { get { Load(); return muted ? 0f : master * music; } }
    public static float SfxLevel { get { Load(); return muted ? 0f : master * sfx; } }
    public static float VoiceLevel { get { Load(); return muted ? 0f : master * voice; } }
    public static float AmbientLevel { get { Load(); return muted ? 0f : master * amb; } }

    public static void Save() { PlayerPrefs.Save(); }

    // ------------------------------------------------------------ страховка: AudioListener и сохранение
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Load();
        SceneManager.sceneLoaded -= OnSceneLoaded;      // -= перед += : без дублей, если домен не перезагружается
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (UnityEngine.Object.FindFirstObjectByType<GameAudioHost>() == null)
        {
            var go = new GameObject("_GameAudioHost");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GameAudioHost>();
        }
        EnsureListener();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { EnsureListener(); }

    // нет ни одного включённого AudioListener = в игре полная тишина без единой ошибки
    static void EnsureListener()
    {
        var all = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].enabled && all[i].gameObject.activeInHierarchy) return;

        var cam = Camera.main;
        if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cam == null) return;

        var l = cam.GetComponent<AudioListener>();
        if (l == null) l = cam.gameObject.AddComponent<AudioListener>();
        l.enabled = true;
        Debug.LogWarning("[GameAudio] В сцене не было включённого AudioListener, добавлен на камеру '" + cam.name + "'.");
    }
}

// Сохраняет настройки при сворачивании и выходе (в браузере и на телефоне Close() окна может не успеть)
public class GameAudioHost : MonoBehaviour
{
    void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    void OnApplicationQuit() { PlayerPrefs.Save(); }
}
