using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace IslandV2
{
    internal static class TWHearthVisual
    {
        static readonly Color W = Color.white;

        static GameObject Finish(TWCenterModel model, TWCenterAssets assets, Transform parent, string name)
        {
            return model.Finish(assets, parent, name, parent.position, parent.rotation);
        }

        public static void Build(TWCenterAssets assets, Transform stage1, Transform stage2, Transform stage3, Transform flames, int preview)
        {
            var hearth = new TWCenterModel();
            hearth.Part("Ash").Cyl(Vector3.up * 0.025f, Vector3.up * 0.06f, 1.48f, 1.48f, W, 24);
            for (int i = 0; i < 11; i++)
            {
                float a = i * Mathf.PI * 2f / 11f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 1.30f, 0.20f, Mathf.Sin(a) * 1.30f);
                hearth.Part("Stone").Puff(p, new Vector3(0.32f, 0.23f + (i % 3) * 0.015f, 0.25f), W, -a * Mathf.Rad2Deg, 7, 3, 0f);
            }
            var ring = Finish(hearth, assets, stage1, "Hearth_Stone_And_Ash");
            ring.AddComponent<MeshCollider>().sharedMesh = ring.GetComponent<MeshFilter>().sharedMesh;
            var obstacle = ring.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 1.55f; obstacle.height = 0.5f; obstacle.center = Vector3.up * 0.25f;
            obstacle.carving = true; obstacle.carveOnlyStationary = true;

            var cold = new GameObject("Fuel_Before_Ignition"); cold.transform.SetParent(stage1, false);
            var visible = cold.AddComponent<StageVisibility>(); visible.minStage = 1; visible.maxStage = 2;
            visible.exitStyle = StageVisibility.ExitStyle.Pop; visible.exitTime = 0.7f;
            var fuel = new TWCenterModel(); Logs(fuel, false);
            Finish(fuel, assets, cold.transform, "Ready_Split_Logs");
            cold.SetActive(preview < 3);
            Sign(assets, stage1);

            var frame = new TWCenterModel();
            // A high horizontal bar keeps the central flames unobstructed.
            foreach (int side in new[] { -1, 1 })
            {
                frame.Part("DarkWood").Cyl(new Vector3(side * 1.18f, 0f, 0.75f), new Vector3(side * 1.12f, 2.25f, 0.75f), 0.065f, 0.055f, W, 7);
                for (int band = 0; band < 3; band++)
                    frame.Part("Rope").Cyl(new Vector3(side * 1.12f, 2.09f + band * 0.043f, 0.75f), new Vector3(side * 1.12f, 2.11f + band * 0.043f, 0.75f), 0.078f, 0.078f, W, 7);
            }
            frame.Part("Wood").Cyl(new Vector3(-1.35f, 2.18f, 0.75f), new Vector3(1.35f, 2.18f, 0.75f), 0.075f, 0.07f, W, 8);
            // The kettle sits behind the fire, leaving its front silhouette clear.
            frame.Part("Metal").Cyl(new Vector3(0f, 2.15f, 0.75f), new Vector3(0f, 1.71f, 0.75f), 0.012f, 0.012f, W, 5);
            frame.Part("Metal").Puff(new Vector3(0f, 1.48f, 0.75f), new Vector3(0.27f, 0.21f, 0.27f), W, 0f, 9, 4, 0f);
            frame.Part("Metal").Cyl(new Vector3(0f, 1.67f, 0.75f), new Vector3(0f, 1.71f, 0.75f), 0.21f, 0.19f, W, 9);
            // A small hourglass on the right post signals the time/rest function.
            Hourglass(frame, new Vector3(1.31f, 1.15f, 0.68f));
            var frameGo = Finish(frame, assets, stage2, "Timber_Crossbar_And_Kettle");
            foreach (int side in new[] { -1, 1 })
            {
                var col = frameGo.AddComponent<BoxCollider>(); col.center = new Vector3(side * 1.15f, 1.12f, 0.75f); col.size = new Vector3(0.15f, 2.25f, 0.15f);
            }

            var burning = new TWCenterModel(); Logs(burning, true);
            burning.Part("Coal").Puff(new Vector3(0f, 0.12f, 0f), new Vector3(0.94f, 0.10f, 0.94f), W, 0f, 12, 3, 0f);
            Finish(burning, assets, stage3, "Burning_Logs_And_Coals");
            var fire = new TWCenterModel();
            fire.Part("FireOrange").Teardrop(new Vector3(0f, 0.02f, 0f), 1.72f, 0.48f, new Vector3(0.10f, 0f, 0f), W, W, 9, 5);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.3f;
                fire.Part("FireOrange").Teardrop(new Vector3(Mathf.Cos(a) * 0.34f, 0.01f, Mathf.Sin(a) * 0.30f), 1.05f + (i % 2) * 0.22f,
                    0.24f, new Vector3(Mathf.Cos(a) * 0.15f, 0f, Mathf.Sin(a) * 0.1f), W, W, 7, 4);
            }
            fire.Part("FireCore").Teardrop(new Vector3(0.02f, 0.025f, -0.12f), 1.08f, 0.32f, new Vector3(-0.06f, 0f, 0f), W, W, 8, 4);
            var fireGo = Finish(fire, assets, flames, "Hero_Flames");
            fireGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Logs(TWCenterModel model, bool burning)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 30f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 b = dir * 0.87f + Vector3.up * 0.19f, t = dir * 0.09f + Vector3.up * (burning ? 0.67f : 0.93f);
                model.Part(burning ? "DarkWood" : "Wood").Cyl(b, t, 0.14f, 0.11f, W, 7);
                Vector3 end = (b - t).normalized;
                model.Part("EndGrain").Cyl(b + end * 0.002f, b + end * 0.014f, 0.105f, 0.105f, W, 7);
            }
        }

        static void Hourglass(TWCenterModel model, Vector3 p)
        {
            model.Part("EndGrain").Box(p, new Vector3(0.22f, 0.04f, 0.17f), Quaternion.identity, W);
            model.Part("EndGrain").Box(p + Vector3.up * 0.32f, new Vector3(0.22f, 0.04f, 0.17f), Quaternion.identity, W);
            model.Part("Cup").Cyl(p + Vector3.up * 0.04f, p + Vector3.up * 0.16f, 0.075f, 0.012f, W, 7);
            model.Part("Cup").Cyl(p + Vector3.up * 0.16f, p + Vector3.up * 0.28f, 0.012f, 0.075f, W, 7);
            foreach (int side in new[] { -1, 1 }) model.Part("DarkWood").Cyl(p + Vector3.right * (side * 0.09f), p + Vector3.right * (side * 0.09f) + Vector3.up * 0.32f, 0.012f, 0.012f, W, 5);
        }

        static void Sign(TWCenterAssets assets, Transform root)
        {
            var sign = new TWCenterModel(); Vector3 p = new Vector3(-2.10f, 0f, 0.90f);
            sign.Part("DarkWood").Cyl(p, p + Vector3.up * 1.45f, 0.075f, 0.06f, W, 7);
            sign.Part("DarkWood").Box(p + Vector3.up * 1.20f, new Vector3(1.52f, 0.52f, 0.09f), Quaternion.identity, W);
            sign.Part("EndGrain").Box(p + new Vector3(0f, 1.20f, -0.051f), new Vector3(1.40f, 0.42f, 0.018f), Quaternion.identity, W);
            // A campfire crest above the readable title, modelled without glyphs.
            sign.Part("Metal").Cyl(p + new Vector3(-0.20f, 1.59f, 0f), p + new Vector3(0.20f, 1.75f, 0f), 0.025f, 0.025f, W, 5);
            sign.Part("Metal").Cyl(p + new Vector3(0.20f, 1.59f, 0f), p + new Vector3(-0.20f, 1.75f, 0f), 0.025f, 0.025f, W, 5);
            sign.Part("Wood").Teardrop(p + new Vector3(0f, 1.69f, 0f), 0.40f, 0.15f, Vector3.zero, W, W, 7, 4);
            var go = Finish(sign, assets, root, "Campfire_Landmark_Sign");
            Font font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { return; }
            if (font == null) return;
            font.RequestCharactersInTexture("КОСТЁР", 64, FontStyle.Bold);
            var textGo = new GameObject("Campfire_Title"); textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = p + new Vector3(0f, 1.20f, -0.065f);
            var label = textGo.AddComponent<TextMesh>(); label.font = font; label.fontSize = 64; label.characterSize = 0.24f;
            label.fontStyle = FontStyle.Bold; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = new Color(0.24f, 0.20f, 0.17f); label.text = "КОСТЁР";
            var renderer = textGo.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
