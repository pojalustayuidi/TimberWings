// Assets/_Project/Scripts/Editor/HutV2/LumberjackHutBuilder.Sawyer.cs   (ТОЛЬКО в папке Editor!)
// Рабочий-пильщик для стадии 3: низкополигональный лесоруб с «костями» (пустые объекты-шарниры) + козлы с бревном и реквизит.
// Жизнь ему даёт Scripts/Build/Hut/HutSawyer.cs, тут только модель и привязка ссылок.
//
//   Sawyer (корень, HutSawyer)
//     ├─ LegL/LegR (hipL/hipR) → knee → голень + сапог
//     └─ Hips (hips) → торс, ремень, подтяжки
//          ├─ Head (head) → голова, борода, глаза, шапка
//          ├─ ShoulderL/R (shoulder) → ElbowL/R (elbow) → предплечье и кисть
//          │      └─ у правого локтя: HandSaw (пила в руке), у левого: Mug (кружка)
//   SawHorse (козлы): бревно, пила, воткнутая в бревно (LogSaw), точка реза (CutPoint), опилки на земле
//   TossPlank: доска, которая летит на склад
//
// Размеры: рост ~1.45 м (с шапкой ~1.7), плечи на 1.0 м, длина руки 0.2 + 0.2 м. От этого зависят позы в HutSawyer
// (рука ложится на рукоятку каретки «PushBar» и на пилу у козел), поэтому размеры лучше не менять без проверки поз.
#if UNITY_EDITOR
using UnityEngine;

public static partial class LumberjackHutBuilder
{
    // где стоит рабочий, когда заказа нет (локальные метры стадии), козлы встают прямо перед ним
    static readonly Vector3 SawyerRestPos = new Vector3(2.9f, 0.05f, 0.15f);
    const float SawyerLaneZ = SawZ + 1.3f;     // вдоль каретки он идёт по этой линии (руки как раз достают до PushBar)

    static void BuildHutSawyer(Transform sm, SawmillAnimator anim)
    {
        EnsurePolishPalette();   // «Skin», «Coal» и т.п. добавляются доводкой, а она идёт после пилорамы

        float gy = O.groundY;
        Vector3 rest = SawyerRestPos + Vector3.up * gy;

        // ---------------------------------------------------------------- козлы с бревном
        var horse = Obj(sm, "SawHorse", rest + new Vector3(0f, 0f, 0.62f));
        var ht = horse.transform;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            float x = sx * 0.5f;
            Beam(ht, "HorseLegA", new Vector3(x, 0f, -0.28f), new Vector3(x, 0.46f, 0f), 0.07f, "WoodDark");
            Beam(ht, "HorseLegB", new Vector3(x, 0f, 0.28f), new Vector3(x, 0.46f, 0f), 0.07f, "WoodDark");
            Beam(ht, "HorseTop", new Vector3(x, 0.46f, -0.3f), new Vector3(x, 0.46f, 0.3f), 0.08f, "WoodMid");
        }
        Beam(ht, "HorseBrace", new Vector3(-0.5f, 0.3f, 0f), new Vector3(0.5f, 0.3f, 0f), 0.05f, "WoodDark");
        Log(ht, "CutLog", new Vector3(-0.7f, 0.62f, 0f), new Vector3(0.7f, 0.62f, 0f), 0.12f, "WoodLight");
        NoShadow(Blob(ht, "SawdustPile", new Vector3(0.25f, 0.025f, 0f), new Vector3(0.75f, 0.05f, 0.45f), "Plank", 2));
        // чурбачки, уже отпиленные
        Log(ht, "SawnChunkA", new Vector3(-0.95f, 0.1f, 0.2f), new Vector3(-0.95f, 0.1f, 0.5f), 0.1f, "WoodLight");
        Log(ht, "SawnChunkB", new Vector3(-1.1f, 0.1f, -0.1f), new Vector3(-1.1f, 0.1f, 0.15f), 0.09f, "WoodMid");

        // пила, воткнутая в бревно (видна, пока рабочий ушёл к пилораме)
        var logSaw = Obj(ht, "LogSaw", new Vector3(0.27f, 0.74f, 0f));
        Box(logSaw.transform, "LogSawBlade", new Vector3(0f, -0.02f, 0f), new Vector3(0.015f, 0.11f, 0.5f), "Metal");
        Box(logSaw.transform, "LogSawSpine", new Vector3(0f, 0.04f, 0f), new Vector3(0.02f, 0.025f, 0.5f), "MetalDark");
        Box(logSaw.transform, "LogSawGrip", new Vector3(0f, 0.06f, 0.3f), new Vector3(0.05f, 0.12f, 0.1f), "Red");
        logSaw.SetActive(false);

        // точка реза: сюда садятся опилки, пока рабочий пилит
        var cut = Obj(ht, "CutPoint", new Vector3(0.27f, 0.74f, 0f));

        // ---------------------------------------------------------------- сам рабочий
        var root = Obj(sm, "Sawyer", rest);
        var rt = root.transform;
        var sawyer = root.AddComponent<HutSawyer>();

        // ноги: бедро (hip) -> колено (knee), оба шарнира крутятся вокруг X
        Transform hipL, kneeL, hipR, kneeR;
        MakeLeg(rt, "L", -0.1f, out hipL, out kneeL);
        MakeLeg(rt, "R", 0.1f, out hipR, out kneeR);

        // таз: шарнир на уровне пояса, от него наклоняется всё тело
        var hipsGo = Obj(rt, "Hips", new Vector3(0f, 0.55f, 0f));
        var hp = hipsGo.transform;
        Box(hp, "Torso", new Vector3(0f, 0.25f, 0f), new Vector3(0.4f, 0.5f, 0.25f), "Red");
        Box(hp, "Belt", new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.07f, 0.27f), "WoodDark");
        Box(hp, "BeltBuckle", new Vector3(0f, 0.02f, 0.14f), new Vector3(0.07f, 0.06f, 0.02f), "Metal");
        for (int sx = -1; sx <= 1; sx += 2)   // подтяжки
        {
            Box(hp, "StrapFront", new Vector3(sx * 0.11f, 0.3f, 0.13f), new Vector3(0.05f, 0.44f, 0.02f), "WoodDark");
            Box(hp, "StrapBack", new Vector3(sx * 0.11f, 0.3f, -0.13f), new Vector3(0.05f, 0.44f, 0.02f), "WoodDark");
        }
        Box(hp, "Pocket", new Vector3(-0.1f, 0.13f, 0.135f), new Vector3(0.11f, 0.1f, 0.02f), "WoodDark");
        Box(hp, "Handkerchief", new Vector3(0.0f, 0.5f, 0.1f), new Vector3(0.2f, 0.07f, 0.2f), "Cream");

        // голова
        var headGo = Obj(hp, "Head", new Vector3(0f, 0.53f, 0f));
        var hd = headGo.transform;
        Blob(hd, "HeadMesh", new Vector3(0f, 0.14f, 0f), new Vector3(0.3f, 0.3f, 0.3f), "Skin", 1);
        Blob(hd, "Beard", new Vector3(0f, 0.05f, 0.08f), new Vector3(0.25f, 0.15f, 0.15f), "PlankDark", 2);
        Blob(hd, "Nose", new Vector3(0f, 0.13f, 0.15f), new Vector3(0.06f, 0.06f, 0.06f), "Skin", 3);
        for (int sx = -1; sx <= 1; sx += 2)
            Box(hd, "Eye", new Vector3(sx * 0.06f, 0.18f, 0.135f), new Vector3(0.035f, 0.045f, 0.02f), "Coal");
        Blob(hd, "Cap", new Vector3(0f, 0.27f, -0.01f), new Vector3(0.35f, 0.22f, 0.35f), "Leaf", 1);
        Box(hd, "CapBand", new Vector3(0f, 0.21f, 0f), new Vector3(0.34f, 0.05f, 0.34f), "Cream");
        Blob(hd, "CapPompon", new Vector3(0f, 0.39f, 0f), new Vector3(0.09f, 0.09f, 0.09f), "Cream", 2);

        // руки
        Transform shR, elR, shL, elL;
        MakeArm(hp, "R", 0.27f, out shR, out elR);
        MakeArm(hp, "L", -0.27f, out shL, out elL);

        // ручная пила в правой руке: лезвие смотрит вперёд вдоль предплечья, зубья вниз (видна только у козел)
        var handSaw = Obj(elR, "HandSaw", new Vector3(0f, 0f, 0f));
        Box(handSaw.transform, "SawBlade", new Vector3(0f, -0.47f, 0f), new Vector3(0.015f, 0.5f, 0.11f), "Metal");
        Box(handSaw.transform, "SawSpine", new Vector3(0f, -0.47f, 0.06f), new Vector3(0.02f, 0.5f, 0.025f), "MetalDark");
        Box(handSaw.transform, "SawGrip", new Vector3(0f, -0.22f, 0.03f), new Vector3(0.05f, 0.11f, 0.1f), "Red");

        // кружка в левой руке: повёрнута так, чтобы стоять ровно, когда рука поднята ко рту
        var mug = Obj(elL, "Mug", new Vector3(0f, -0.25f, 0.05f));
        mug.transform.localRotation = Quaternion.Euler(161f, 0f, 0f);
        Cyl(mug.transform, "MugBody", Vector3.zero, 0.05f, 0.1f, "Cream", 8);
        Cyl(mug.transform, "MugTea", new Vector3(0f, 0.045f, 0f), 0.042f, 0.012f, "WoodMid", 8);
        Box(mug.transform, "MugHandle", new Vector3(0.065f, 0f, 0f), new Vector3(0.03f, 0.07f, 0.03f), "Cream");
        mug.SetActive(false);

        // доска, которая летит на склад (живёт под пилорамой, а не под рабочим: летит по миру)
        var plank = Box(sm, "TossPlank", Vector3.zero, new Vector3(0.8f, 0.045f, 0.2f), "Plank");
        plank.SetActive(false);

        // ---------------------------------------------------------------- привязка
        sawyer.saw = anim;
        sawyer.hips = hp;
        sawyer.head = hd;
        sawyer.shoulderR = shR; sawyer.elbowR = elR;
        sawyer.shoulderL = shL; sawyer.elbowL = elL;
        sawyer.hipL = hipL; sawyer.kneeL = kneeL;
        sawyer.hipR = hipR; sawyer.kneeR = kneeR;
        sawyer.handSaw = handSaw;
        sawyer.logSaw = logSaw;
        sawyer.mug = mug;
        sawyer.tossPlank = plank.transform;
        sawyer.cutPoint = cut.transform;
        sawyer.restPos = rest;
        sawyer.restYaw = 0f;
        sawyer.laneZ = SawyerLaneZ;
        sawyer.followX = 0f;
        sawyer.groundY = rest.y;
        sawyer.deckY = 0.13f + gy;
        sawyer.deckX0 = SawX0 - 0.1f;
    }

    // нога: шарнир бедра (X), выше колено; шарниры лежат на корне, чтобы не наклоняться вместе с корпусом
    static void MakeLeg(Transform p, string side, float x, out Transform hip, out Transform knee)
    {
        var h = Obj(p, "Hip" + side, new Vector3(x, 0.52f, 0f));
        Box(h.transform, "Thigh", new Vector3(0f, -0.13f, 0f), new Vector3(0.14f, 0.27f, 0.16f), "MetalDark");
        var k = Obj(h.transform, "Knee" + side, new Vector3(0f, -0.26f, 0f));
        Box(k.transform, "Shin", new Vector3(0f, -0.12f, 0f), new Vector3(0.13f, 0.25f, 0.15f), "MetalDark");
        Box(k.transform, "Boot", new Vector3(0f, -0.24f, 0.035f), new Vector3(0.15f, 0.11f, 0.25f), "WoodDark");
        hip = h.transform;
        knee = k.transform;
    }

    // рука: плечо (шарнир на корпусе) -> локоть -> предплечье и кисть. Висит вдоль -Y, поэтому «вперёд» = отрицательный угол по X
    static void MakeArm(Transform hips, string side, float x, out Transform shoulder, out Transform elbow)
    {
        var s = Obj(hips, "Shoulder" + side, new Vector3(x, 0.46f, 0f));
        Box(s.transform, "UpperArm", new Vector3(0f, -0.1f, 0f), new Vector3(0.1f, 0.22f, 0.1f), "Red");
        var e = Obj(s.transform, "Elbow" + side, new Vector3(0f, -0.21f, 0f));
        Box(e.transform, "Forearm", new Vector3(0f, -0.09f, 0f), new Vector3(0.09f, 0.2f, 0.09f), "Skin");
        Blob(e.transform, "Hand", new Vector3(0f, -0.21f, 0f), new Vector3(0.1f, 0.1f, 0.1f), "Skin", 2);
        shoulder = s.transform;
        elbow = e.transform;
    }
}
#endif
