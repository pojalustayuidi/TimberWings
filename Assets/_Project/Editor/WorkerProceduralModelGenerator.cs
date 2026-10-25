#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Альтернативный экспериментальный вариант 3D-фигурок рабочих.
// Не заменяет WorkerPortraitGenerator и не трогает существующие портреты/ассеты.
// Меню: Tools > BuilderSim > Рабочие > Сгенерировать 27 процедурных 3D-моделей
namespace TimberWings.EditorTools
{
    public static class WorkerProceduralModelGenerator
    {
        const string Root = "Assets/Project/Art/Workers/ProceduralModels";
        const string Prefabs = Root + "/Prefabs";
        const string Materials = Root + "/Materials";

        sealed class Spec
        {
            public string id, name, role, rarity, quote, outfit, accent, hair;
            public bool female, beard, hat, glasses, tool;
            public Spec(string id, string name, string role, string rarity, string quote,
                string outfit, string accent, string hair, bool female = false,
                bool beard = false, bool hat = false, bool glasses = false, bool tool = false)
            { this.id=id; this.name=name; this.role=role; this.rarity=rarity; this.quote=quote;
              this.outfit=outfit; this.accent=accent; this.hair=hair; this.female=female;
              this.beard=beard; this.hat=hat; this.glasses=glasses; this.tool=tool; }
        }

        static readonly List<Spec> Specs = new List<Spec>
        {
            new Spec("clerk_nils","Нильс Кошелёк","Приказчик","Обычный","Считаю даже ступеньки","#D9B84F","#80502D","#80502D",false,false,false,false,true),
            new Spec("clerk_agata","Агата Медяк","Приказчик","Обычный","Копейка рубль бережёт","#527A4B","#D8B66A","#35251F",true,false,false,true,true),
            new Spec("clerk_rurik","Рюрик Грош","Приказчик","Обычный","Я не сплю, я прикидываю","#68718A","#D4A64A","#77736A",false,true,true,false,false),
            new Spec("clerk_sigrid","Сигрид Ларсен","Приказчик","Необычный","Каждому по весу","#315C8A","#D8B66A","#A98748",true,false,false,false,true),
            new Spec("clerk_bjorn","Бьорн Тарн","Приказчик","Необычный","Записано, значит, было","#79694E","#B7A98B","#8B8D91",false,true,false,false,true),
            new Spec("clerk_elsa","Эльза Звонкая","Приказчик","Редкий","Слышишь? Это к удаче","#B94835","#E4BD54","#C9512D",true,false,false,false,true),
            new Spec("clerk_gustav","Густав Пенс","Приказчик","Редкий","В молодости я был богат. Дважды","#47353A","#D7B66A","#C9C8C3",false,true,true,false,true),
            new Spec("clerk_frida","Фрида Счётная","Приказчик","Легендарный","Ошибка в счёте? Не у меня","#202E49","#E3B94D","#62616A",true,false,false,true,true),
            new Spec("clerk_brita","Брита Золотые Весы","Приказчик","Мифический","Сначала честность, потом выгода. Выгода всё равно придёт","#C49A3A","#F5D76B","#D9D5C9",true,false,false,false,true),
            new Spec("lumber_pekka","Пекка Топорик","Дровосек","Обычный","Он не тяжёлый, он просто длинный","#557A4B","#B34D35","#A9753E",false,false,true,false,true),
            new Spec("lumber_martta","Мартта Щепка","Дровосек","Обычный","Если не щепки, значит, не работа","#4F794C","#C14B3C","#4B3025",true,false,true,false,true),
            new Spec("lumber_olle","Олле Пенёк","Дровосек","Обычный","Отдыхать тоже надо уметь","#65734A","#8B6345","#493329",false,true,false,false,true),
            new Spec("lumber_knut","Кнут Бук","Дровосек","Необычный","Топор сам знает, куда лететь","#B94D3D","#4B3228","#87452B",false,true,false,false,true),
            new Spec("lumber_aune","Ауне Ветка","Дровосек","Необычный","Два удара, два дерева. Ну почти","#43814B","#79523A","#2D2930",true,false,false,false,true),
            new Spec("lumber_johanna","Йоханна Корень","Дровосек","Редкий","Что не влезло, то я поднимусь ещё раз","#65734A","#B58A5D","#80512F",true,false,false,false,true),
            new Spec("lumber_einar","Эйнар Жила","Дровосек","Редкий","Золото в дереве есть, надо знать, где рубить","#565A4D","#D9AD39","#9A9A91",false,true,false,false,true),
            new Spec("lumber_tor","Тор Глыба","Дровосек","Легендарный","Не торопи дерево, оно само упадёт","#3D6845","#8B5D3C","#C99A43",false,true,false,false,true),
            new Spec("lumber_holger","Хольгер Кедровый","Дровосек","Мифический","Кедры поют. Ты слушай, а не руби","#62482F","#4A8C50","#D7D7D3",false,true,false,false,true),
            new Spec("saw_til","Тиль Опилка","Пильщик","Обычный","Это не мусор, это стружка","#496DA0","#D7D0BE","#8B5B2F",false,false,true,true,true),
            new Spec("saw_helga","Хельга Брусок","Пильщик","Обычный","Ровно, или никак","#456A9B","#D9D0BA","#3C2924",true,false,false,false,true),
            new Spec("saw_ivar","Ивар Рейка","Пильщик","Обычный","Пилу не торопят","#526F9B","#3B4D6B","#5B5C61",false,true,false,false,true),
            new Spec("saw_solveig","Сольвейг Рубанок","Пильщик","Необычный","Чем быстрее, тем ровнее, если не дрожит рука","#456DA0","#E0C44F","#B88C49",true,false,true,false,true),
            new Spec("saw_vilhelm","Вильхельм Стамеска","Пильщик","Необычный","Лишних досок не бывает","#76694F","#A18A62","#929293",false,true,false,false,true),
            new Spec("saw_ingeborg","Ингеборг Зубец","Пильщик","Редкий","Один распил, две доски","#38517C","#9A9DA4","#332A28",true,false,false,false,true),
            new Spec("saw_dagni","Дагни Шлиф","Пильщик","Редкий","Бревно любит бережного","#496CA0","#A78BC6","#302D35",true,false,true,false,true),
            new Spec("saw_lars","Ларс Пила","Пильщик","Легендарный","Пила поёт, если её слушать","#2E4A7D","#BFC5D0","#B7B8BE",false,true,false,false,true),
            new Spec("saw_ingrid","Ингрид Ровная","Пильщик","Мифический","Хорошая доска не нуждается в объяснениях","#7594BD","#E4E8F0","#C8CCD8",true,false,false,false,true)
        };

        static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        static Color Hex(string value)
        {
            Color c; ColorUtility.TryParseHtmlString(value, out c); return c;
        }

        static Material Mat(string hex)
        {
            if (MatCache.TryGetValue(hex, out var found) && found != null) return found;
            string path = Materials + "/M_" + hex.TrimStart('#') + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            // Prefer the shader matching the project's active Render Pipeline.
            // Existing materials are repaired too, so already-generated assets are not stuck pink.
            Shader shader = null;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            else
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            material.color = Hex(hex);
            MatCache[hex] = material;
            return material;
        }

        static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 pos,
            Vector3 scale, string color, Vector3? rotation = null)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (rotation.HasValue) go.transform.localEulerAngles = rotation.Value;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(color);
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            return go;
        }

        static string RarityAccent(Spec s)
        {
            switch (s.rarity)
            {
                case "Необычный": return "#66B96B";
                case "Редкий": return "#4C9BDE";
                case "Легендарный": return "#F2B544";
                case "Мифический": return "#B76BFF";
                default: return "#A5A9AF";
            }
        }

        static void Build(Spec s)
        {
            var root = new GameObject(s.name);
            root.transform.position = Vector3.zero;
            root.tag = "Untagged";
            string skin = "#E0AD89";
            string rarity = RarityAccent(s);
            Part(root.transform, "Shadow base", PrimitiveType.Cylinder, new Vector3(0, .06f, 0), new Vector3(.48f, .06f, .38f), "#292B32");
            Part(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0, .82f, 0), new Vector3(.58f, .76f, .38f), s.outfit);
            Part(root.transform, "Collar", PrimitiveType.Cube, new Vector3(0, 1.14f, -.015f), new Vector3(.28f, .12f, .34f), s.accent);
            Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), new Vector3(.48f, .52f, .43f), skin);
            Part(root.transform, "Nose", PrimitiveType.Sphere, new Vector3(0, 1.52f, -.215f), new Vector3(.09f, .09f, .06f), "#C78A6D");
            Part(root.transform, "Eye L", PrimitiveType.Sphere, new Vector3(-.105f, 1.61f, -.196f), new Vector3(.055f, .055f, .035f), "#29252A");
            Part(root.transform, "Eye R", PrimitiveType.Sphere, new Vector3(.105f, 1.61f, -.196f), new Vector3(.055f, .055f, .035f), "#29252A");
            Part(root.transform, "Hair", PrimitiveType.Sphere, new Vector3(0, 1.79f, .005f), new Vector3(.51f, .25f, .44f), s.hair);
            Part(root.transform, "Left arm", PrimitiveType.Capsule, new Vector3(-.39f, .84f, 0), new Vector3(.17f, .48f, .18f), s.outfit, new Vector3(0,0,-14));
            Part(root.transform, "Right arm", PrimitiveType.Capsule, new Vector3(.39f, .84f, 0), new Vector3(.17f, .48f, .18f), s.outfit, new Vector3(0,0,14));
            Part(root.transform, "Left hand", PrimitiveType.Sphere, new Vector3(-.43f, .55f, -.01f), new Vector3(.14f,.14f,.14f), skin);
            Part(root.transform, "Right hand", PrimitiveType.Sphere, new Vector3(.43f, .55f, -.01f), new Vector3(.14f,.14f,.14f), skin);
            Part(root.transform, "Belt", PrimitiveType.Cube, new Vector3(0, .57f, -.015f), new Vector3(.48f,.09f,.32f), s.accent);
            // Маленький знак редкости на груди, заметный на превью пака.
            Part(root.transform, "Rarity pin", PrimitiveType.Sphere, new Vector3(.13f, 1.04f, -.205f), new Vector3(.12f,.12f,.06f), rarity);
            if (s.hat) Part(root.transform, "Hat", PrimitiveType.Cylinder, new Vector3(0, 1.91f, 0), new Vector3(.34f,.09f,.32f), s.accent);
            if (s.glasses)
            {
                Part(root.transform, "Glasses L", PrimitiveType.Cylinder, new Vector3(-.105f,1.61f,-.232f), new Vector3(.11f,.025f,.025f), "#C9B16B", new Vector3(90,0,0));
                Part(root.transform, "Glasses R", PrimitiveType.Cylinder, new Vector3(.105f,1.61f,-.232f), new Vector3(.11f,.025f,.025f), "#C9B16B", new Vector3(90,0,0));
                Part(root.transform, "Glasses bridge", PrimitiveType.Cube, new Vector3(0,1.61f,-.24f), new Vector3(.1f,.025f,.025f), "#C9B16B");
            }
            if (s.beard) Part(root.transform, "Beard", PrimitiveType.Sphere, new Vector3(0,1.36f,-.07f), new Vector3(.34f,.24f,.34f), s.hair);
            if (s.tool)
            {
                bool lumber = s.role == "Дровосек";
                bool sawyer = s.role == "Пильщик";
                if (lumber)
                {
                    Part(root.transform, "Tool handle", PrimitiveType.Cylinder, new Vector3(.57f,.84f,0), new Vector3(.045f,.48f,.045f), "#805331", new Vector3(0,0,-24));
                    Part(root.transform, "Axe head", PrimitiveType.Cube, new Vector3(.72f,1.12f,0), new Vector3(.27f,.16f,.08f), "#BFC6CE", new Vector3(0,0,-24));
                }
                else if (sawyer)
                {
                    Part(root.transform, "Saw blade", PrimitiveType.Cube, new Vector3(.61f,.91f,0), new Vector3(.08f,.54f,.035f), "#C7D0D9", new Vector3(0,0,-20));
                    Part(root.transform, "Saw handle", PrimitiveType.Cube, new Vector3(.63f,1.22f,0), new Vector3(.17f,.12f,.07f), "#805331", new Vector3(0,0,-20));
                }
                else
                {
                    Part(root.transform, "Coin purse", PrimitiveType.Sphere, new Vector3(.3f,.72f,-.2f), new Vector3(.2f,.2f,.13f), s.accent);
                    Part(root.transform, "Coin", PrimitiveType.Cylinder, new Vector3(.3f,.74f,-.28f), new Vector3(.09f,.025f,.09f), "#E9C451", new Vector3(90,0,0));
                }
            }
            // Встроенная подсказка остаётся в иерархии prefab, не требует нового runtime-класса.
            var notes = new GameObject("INFO - " + s.role + " | " + s.rarity + " | " + s.quote);
            notes.transform.SetParent(root.transform, false);
            string path = Prefabs + "/" + s.id + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        [MenuItem("Tools/BuilderSim/Рабочие/Сгенерировать 27 процедурных 3D-моделей", false, 510)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Materials);
            MatCache.Clear();
            try
            {
                for (int i = 0; i < Specs.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Процедурные модели рабочих", Specs[i].name, i / (float)Specs.Count);
                    Build(Specs[i]);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Workers] Создано " + Specs.Count + " экспериментальных 3D-моделей в " + Prefabs + ". Портретный генератор и существующие варианты не изменены.");
        }
    }
}
#endif
