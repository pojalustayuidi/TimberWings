// Assets/_Project/Scripts/Editor/Buildings/ShopBuilderV2.Mascot.cs   (ТОЛЬКО в папке Editor!)
// ShopBuilderV2: облачко-талисман «Пух» на правом краю прилавка (за колокольчиком).
// Крошечное пушистое облачко в голубом шарфике с золотой монеткой на груди. Живёт в Stage3 (виден на готовой лавке).
// Ссылки отдаёт в LifeRefs; оживляет ShopLife: качается на месте, моргает, поворачивается к игроку, машет рукой при
// приветствии и подпрыгивает с «пружинкой» при каждой продаже.
// Точка опоры (pivot) у основания: так «пружинка» и прыжок считаются от прилавка.
#if UNITY_EDITOR
using UnityEngine;

public partial class ShopBuilderV2
{
    void BuildMascot(Transform p)
    {
        float topY = PlatH + 0.95f;
        float zc = counterZ + 0.06f;
        var root = Obj(p, "Mascot", new Vector3(counterR - 0.1f, topY, zc - 0.1f)).transform;
        var body = Obj(root, "MascotBody", Vector3.zero).transform;

        // ножки-подушечки и пышное тельце из трёх облачных «шариков»
        Blob(body, "PuffFootL", new Vector3(-0.07f, 0.03f, 0.03f), new Vector3(0.1f, 0.07f, 0.13f), "Cream", 2);
        Blob(body, "PuffFootR", new Vector3(0.07f, 0.03f, 0.03f), new Vector3(0.1f, 0.07f, 0.13f), "Cream", 3);
        Blob(body, "PuffBody", new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.26f, 0.26f), "Cloud", 1);
        Blob(body, "PuffTopA", new Vector3(-0.055f, 0.285f, 0f), new Vector3(0.2f, 0.16f, 0.18f), "Cloud", 2);
        Blob(body, "PuffTopB", new Vector3(0.07f, 0.265f, 0.01f), new Vector3(0.16f, 0.14f, 0.16f), "Cloud", 3);

        // голубой шарфик и золотая монетка: сразу видно, что это помощник торговца
        Blob(body, "Scarf", new Vector3(0f, 0.095f, 0.02f), new Vector3(0.29f, 0.06f, 0.25f), "Sky", 2);
        Blob(body, "ScarfTail", new Vector3(-0.09f, 0.045f, 0.12f), new Vector3(0.05f, 0.11f, 0.03f), "Sky", 1);
        Cyl(body, "Badge", new Vector3(0.06f, 0.045f, 0.125f), 0.034f, 0.015f, "Gold", 10, new Vector3(90f, 0f, 0f));

        // щёчки
        Blob(body, "CheekL", new Vector3(-0.1f, 0.15f, 0.115f), new Vector3(0.05f, 0.032f, 0.02f), "Pink", 1);
        Blob(body, "CheekR", new Vector3(0.1f, 0.15f, 0.115f), new Vector3(0.05f, 0.032f, 0.02f), "Pink", 2);

        // глазки: общий пивот, чтобы ShopLife мог моргать одним масштабом по Y
        var eyes = Obj(body, "MascotEyes", new Vector3(0f, 0.2f, 0.122f));
        Blob(eyes.transform, "EyeL", new Vector3(-0.055f, 0f, 0f), new Vector3(0.04f, 0.056f, 0.03f), "Chalk", 1);
        Blob(eyes.transform, "EyeR", new Vector3(0.055f, 0f, 0f), new Vector3(0.04f, 0.056f, 0.03f), "Chalk", 2);
        Blob(eyes.transform, "EyeGlintL", new Vector3(-0.047f, 0.014f, 0.012f), new Vector3(0.012f, 0.014f, 0.01f), "Cloud", 1);
        Blob(eyes.transform, "EyeGlintR", new Vector3(0.063f, 0.014f, 0.012f), new Vector3(0.012f, 0.014f, 0.01f), "Cloud", 2);

        // рука-помахалка на шарнире (плечо), машет при приветствии
        var arm = Obj(body, "MascotArm", new Vector3(0.14f, 0.17f, 0f));
        Blob(arm.transform, "ArmMesh", new Vector3(0.055f, 0.0f, 0f), new Vector3(0.13f, 0.06f, 0.06f), "Cloud", 2);

        L.mascot = root;
        L.mascotBody = body;
        L.mascotEyes = eyes.transform;
        L.mascotArm = arm.transform;
    }
}
#endif
