using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;
using static UiLayout;

public partial class EndingSequence
{
    const string PortalFilm = "Finale/TimberWings_Chapter2_Finale_Unity";

    IEnumerator PlayPortalFilm()
    {
        var clip = Resources.Load<VideoClip>(PortalFilm);
        if (clip == null)
        {
            Debug.LogWarning("EndingSequence: не найден ролик главы II. Включена текстовая заставка.", this);
            yield break;
        }

        var layer = NewRT("ChapterTwoFilm", root);
        Stretch(layer);
        layer.SetSiblingIndex(hint.transform.GetSiblingIndex());
        var screen = NewRT("VideoFrame", layer);
        Stretch(screen);
        var image = screen.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
        var aspect = screen.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = clip.height > 0 ? clip.width / (float)clip.height : 16f / 9f;

        var player = layer.gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.skipOnDrop = true;
        player.renderMode = VideoRenderMode.APIOnly;
        player.source = VideoSource.VideoClip;
        player.clip = clip;

        bool english = GameLanguage.English;
        AudioSource audio = null, narrator = null;
        TMP_Text captions = null;
        ChapterOneText.Cue[] lines = null;
        float[] starts = { 1.5f, 11.5f, 15.5f };
        int nextLine = 0;
        float subtitleUntil = -1f;
        if (english)
        {
            // Use the game's music bed with the separately recorded English stems.
            // This also works with a localized video supplied with audio already embedded.
            narrator = layer.gameObject.AddComponent<AudioSource>();
            narrator.playOnAwake = false;
            narrator.spatialBlend = 0f;
            narrator.ignoreListenerVolume = true;
            lines = new[]
            {
                ChapterOneText.Find("twv3_c01_01"),
                ChapterOneText.Find("twv3_c02_01"),
                ChapterOneText.Find("twv3_c03_01")
            };
            captions = UiKit.MakeLabel(layer, "", 32f, Color.white, TextAlignmentOptions.Center, false);
            captions.outlineWidth = .20f;
            captions.outlineColor = new Color32(22, 27, 39, 255);
            var rect = captions.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = new Vector2(60f, 58f);
            rect.offsetMax = new Vector2(-60f, 150f);
            captions.gameObject.SetActive(false);
        }

        if (!english && clip.audioTrackCount > 0 && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            audio = layer.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.ignoreListenerVolume = true;
            audio.volume = GameAudio.MusicLevel;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audio);
        }
        else
        {
            player.audioOutputMode = VideoAudioOutputMode.None;
        }

        bool failed = false, finished = false;
        player.errorReceived += (_, message) =>
        {
            failed = true;
            Debug.LogWarning("EndingSequence: ролик главы II не воспроизводится. " + message, this);
        };
        player.loopPointReached += _ => finished = true;

        string normalHint = hint.text;
        hint.text = Fix("Пробел или клик — пропустить ролик  ·  удерживайте Esc — к финалу");
        advance = false;
        player.Prepare();
        float start = Time.unscaledTime;
        while (!player.isPrepared && !failed && !skip && Time.unscaledTime - start < 8f)
            yield return null;

        if (player.isPrepared && !failed && !skip)
        {
            player.Play();
            if (english) StartPad();
            start = Time.unscaledTime;
            float deadline = (float)clip.length + 6f;
            while (!failed && !finished && !skip && Time.unscaledTime - start < deadline)
            {
                if (!image.enabled && player.texture != null && player.frame >= 0)
                {
                    image.texture = player.texture;
                    image.enabled = true;
                    advance = false;
                }

                if (image.enabled && english)
                {
                    if (captions.gameObject.activeSelf && player.time >= subtitleUntil)
                        captions.gameObject.SetActive(false);
                    while (nextLine < starts.Length && player.time >= starts[nextLine])
                    {
                        var cue = lines[nextLine];
                        float startAt = starts[nextLine++];
                        if (cue == null || player.time > startAt + 2f) continue;
                        var take = ChapterOneText.Audio(cue);
                        captions.text = cue.en;
                        captions.gameObject.SetActive(DialogueManager.SubtitlesEnabled);
                        subtitleUntil = startAt + (take != null ? take.length : 2.8f);
                        if (take != null && GameAudio.UseRecordedVoices && GameAudio.VoiceLevel > .001f)
                        {
                            narrator.Stop();
                            narrator.volume = GameAudio.VoiceLevel;
                            narrator.clip = take;
                            narrator.Play();
                        }
                    }
                }

                if (image.enabled && advance)
                {
                    filmPlayed = true;
                    break;
                }
                if (!image.enabled && Time.unscaledTime - start > 5f) break;
                yield return null;
            }
            filmPlayed |= image.enabled && finished && !failed;
            if (narrator != null) narrator.Stop();
            if (captions != null) captions.gameObject.SetActive(false);

            if (image.enabled)
            {
                for (float t = 0f; t < .38f && !skip; t += Dt)
                {
                    float gain = 1f - Mathf.Clamp01(t / .38f);
                    image.color = new Color(gain, gain, gain, 1f);
                    if (audio != null) audio.volume = GameAudio.MusicLevel * gain;
                    yield return null;
                }
            }
        }

        player.Stop();
        layer.gameObject.SetActive(false);
        Destroy(layer.gameObject);
        hint.text = normalHint;
        advance = false;
    }
}
