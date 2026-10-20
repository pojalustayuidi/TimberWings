#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Forest/ForestShots.cs (Editor)
// Скриншоты леса одной кнопкой (Tools → Forest → Лес → «Результат»): сверху и под углом игры.
// Сохраняет PNG в <проект>/Screenshots/Forest/. Ничего в сцене не оставляет: камера временная.
//   Сверху: ортографическая камера строго вниз, кадр = прямоугольник зоны леса.
//   Игровая камера: pitch / yaw / FOV / расстояние из ForestSettings (shotPitch 55, shotYaw 45, shotFov 35, shotDistance 26),
//   точка взгляда из shotTarget (центр леса, арка, лесопилка, первая чаща, центр Scene View).
//   Остальные параметры (слои, очистка фона) берутся с Camera.main, если она есть.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ForestShots
{
    const int W = 1920, H = 1080;

    public static string Top(ForestSettings s)
    {
        var master = ForestZones.Find(ForestBiome.Forest);
        if (master == null) return "Скриншот сверху: нет зоны леса (кнопка «1. Создать зону леса»).";
        Rect r = ZoneRect(master.Map);
        Vector3 center = new Vector3(r.center.x, master.transform.position.y, r.center.y);

        var cam = NewCamera(true);
        try
        {
            cam.orthographic = true;
            float aspect = (float)W / H;
            cam.orthographicSize = Mathf.Max(r.height * 0.5f, r.width * 0.5f / aspect) * 1.04f;
            cam.transform.position = center + Vector3.up * 300f;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f; cam.farClipPlane = 700f;
            return Save(cam, "top");
        }
        finally { Object.DestroyImmediate(cam.gameObject); }
    }

    public static string Game(ForestSettings s)
    {
        Vector3 target;
        string what;
        if (!Target(s, out target, out what)) return "Скриншот: нет точки взгляда (" + what + ").";

        var cam = NewCamera(false);
        try
        {
            cam.orthographic = false;
            cam.fieldOfView = s.shotFov;
            Quaternion rot = Quaternion.Euler(s.shotPitch, s.shotYaw, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = target - rot * Vector3.forward * s.shotDistance;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 400f;
            string res = Save(cam, "game");
            return res + "\nТочка взгляда: " + what + ", pitch " + s.shotPitch + "°, yaw " + s.shotYaw + "°, FOV " + s.shotFov + ".";
        }
        finally { Object.DestroyImmediate(cam.gameObject); }
    }

    // ------------------------------------------------------------ внутреннее
    static bool Target(ForestSettings s, out Vector3 t, out string what)
    {
        t = Vector3.zero; what = "";
        var master = ForestZones.Find(ForestBiome.Forest);
        float y = master != null ? master.transform.position.y : 0f;
        switch (s.shotTarget)
        {
            case ForestShotTarget.Gate:
            {
                var g = GameObject.Find("ForestGate");
                if (g == null) { what = "маркера ForestGate нет"; return false; }
                t = g.transform.position; what = "арка"; return true;
            }
            case ForestShotTarget.Sawmill:
            {
                var w = GameObject.Find("ForestSawmill");
                if (w != null) { t = w.transform.position; what = "лесопилка (объект)"; return true; }
                t = new Vector3(s.sawmillCenter.x, y, s.sawmillCenter.y); what = "лесопилка (sawmillCenter)"; return true;
            }
            case ForestShotTarget.Thicket:
            {
                var list = s.GatherMasks(y);
                for (int i = 0; i < list.Count; i++)
                    if (list[i].kind == ForestMaskKind.Thicket) { t = list[i].c; what = "первая чаща"; return true; }
                what = "масок «чаща» нет"; return false;
            }
            case ForestShotTarget.SceneView:
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv == null) { what = "нет окна Scene"; return false; }
                t = sv.pivot; what = "центр Scene View"; return true;
            }
            default:
                if (master == null) { what = "нет зоны леса"; return false; }
                t = master.transform.position; what = "центр леса"; return true;
        }
    }

    static Camera NewCamera(bool top)
    {
        var go = new GameObject("ForestShotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        var main = Camera.main;
        if (main != null && !top) cam.CopyFrom(main);
        else
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.12f);
        }
        cam.targetTexture = null;
        return cam;
    }

    static string Save(Camera cam, string tag)
    {
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var prev = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots/Forest");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "forest_" + tag + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[ForestShots] " + file);
            return "Скриншот сохранён: " + file;
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = prev;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    static Rect ZoneRect(MapZone m)
    {
        Vector3 c = m.transform.position;
        if (m.shape == MapZone.Shape.Circle) return new Rect(c.x - m.radius, c.z - m.radius, m.radius * 2f, m.radius * 2f);
        Quaternion q = Quaternion.Euler(0f, m.transform.eulerAngles.y, 0f);
        float hx = m.size.x * 0.5f, hz = m.size.y * 0.5f;
        Vector3 a = q * new Vector3(-hx, 0f, -hz), b = q * new Vector3(hx, 0f, -hz), d = q * new Vector3(hx, 0f, hz), e = q * new Vector3(-hx, 0f, hz);
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(d.x, e.x)), maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(d.x, e.x));
        float minZ = Mathf.Min(Mathf.Min(a.z, b.z), Mathf.Min(d.z, e.z)), maxZ = Mathf.Max(Mathf.Max(a.z, b.z), Mathf.Max(d.z, e.z));
        return new Rect(c.x + minX, c.z + minZ, maxX - minX, maxZ - minZ);
    }
}
#endif
