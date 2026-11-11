using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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

        AudioSource audio = null;
        if (clip.audioTrackCount > 0 && Application.platform != RuntimePlatform.WebGLPlayer)
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

                if (image.enabled && advance)
                {
                    filmPlayed = true;
                    break;
                }
                if (!image.enabled && Time.unscaledTime - start > 5f) break;
                yield return null;
            }
            filmPlayed |= image.enabled && finished && !failed;

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
