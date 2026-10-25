// WorkerCardProceduralGenerator.cs
// Place in Assets/Editor/WorkerCardProceduralGenerator.cs
// Generates 27 stylized worker-card prefabs using Unity UI + procedural portrait layers.
// Existing assets are never deleted or replaced.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class WorkerCardProceduralGenerator
{
    private const string Root = "Assets/Project/Art/Workers/ProceduralCards";
    private const int W = 512, H = 768;

    private enum Rarity { Common, Uncommon, Rare, Legendary, Mythic }
    private enum Guild { Ledger, Lumber, Sawmill }

    private class Worker
    {
        public int id; public string name; public int age; public Rarity rarity; public Guild guild;
        public string perks, quote, look; public Color coat, hair, skin, accent;
    }

    [MenuItem("Tools/BuilderSim/Рабочие/Сгенерировать 27 процедурных карточек", false, 512)]
    public static void Generate()
    {
        EnsureFolder(Root);
        EnsureFolder(Root + "/Portraits");
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Prefabs");
        var workers = MakeWorkers();
        for (int i = 0; i < workers.Count; i++)
        {
            var w = workers[i];
            var portrait = CreatePortrait(w);
            var portraitPath = Root + "/Portraits/Worker_" + w.id.ToString("00") + "_Portrait.png";
            System.IO.File.WriteAllBytes(portraitPath, portrait.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(portrait);
            AssetDatabase.ImportAsset(portraitPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(portraitPath) as TextureImporter;
            if (importer != null) { importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport(); }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
            CreateCardPrefab(w, sprite);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Процедурные карточки", "Создано 27 карточек и портретов.\n" + Root + "/Prefabs", "Отлично");
    }

    private static void CreateCardPrefab(Worker w, Sprite portrait)
    {
        var go = new GameObject("WorkerCard_" + w.id.ToString("00") + "_" + Safe(w.name), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(280, 420);
        var bg = go.GetComponent<Image>(); bg.color = new Color(.075f,.065f,.052f,1);
        var outline = go.AddComponent<Outline>(); outline.effectColor = RarityColor(w.rarity); outline.effectDistance = new Vector2(3,-3);

        AddPanel(go.transform, "OrnateFrame", new Vector2(0,0), new Vector2(1,1), RarityColor(w.rarity), new Vector4(5,5,5,5), true);
        var portraitGo = AddImage(go.transform, "Portrait", portrait, new Vector2(.055f,.31f), new Vector2(.945f,.94f), Color.white);
        var portraitOutline = portraitGo.AddComponent<Outline>(); portraitOutline.effectColor = RarityColor(w.rarity); portraitOutline.effectDistance = new Vector2(2,-2);

        AddPanel(go.transform, "NamePlate", new Vector2(.035f,.225f), new Vector2(.965f,.34f), new Color(.045f,.035f,.028f,.97f), Vector4.zero, false);
        AddText(go.transform, "Name", w.name, 21, FontStyles.Bold, new Vector2(.06f,.235f), new Vector2(.94f,.32f), new Color(1f,.89f,.67f));
        AddText(go.transform, "AgeAndGuild", w.age + " лет  •  " + GuildName(w.guild), 12, FontStyles.Normal, new Vector2(.05f,.195f), new Vector2(.95f,.235f), new Color(.77f,.71f,.61f));
        AddText(go.transform, "Perks", string.IsNullOrEmpty(w.perks) ? "Без перков" : w.perks, 12, FontStyles.Bold, new Vector2(.06f,.12f), new Vector2(.94f,.19f), RarityColor(w.rarity));
        AddText(go.transform, "Quote", "«" + w.quote + "»", 11, FontStyles.Italic, new Vector2(.06f,.035f), new Vector2(.94f,.115f), new Color(.84f,.79f,.69f));
        AddText(go.transform, "Rarity", RarityName(w.rarity).ToUpperInvariant(), 10, FontStyles.Bold, new Vector2(.08f,.935f), new Vector2(.92f,.985f), RarityColor(w.rarity));

        var prefabPath = Root + "/Prefabs/WorkerCard_" + w.id.ToString("00") + "_" + Safe(w.name) + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static Texture2D CreatePortrait(Worker w)
    {
        // A layered, hand-authored procedural bust illustration. No external assets required.
        var t = new Texture2D(W,H,TextureFormat.RGBA32,false);
        t.filterMode = FilterMode.Bilinear;
        var rng = new System.Random(w.id * 7919);
        Color bg1 = Color.Lerp(new Color(.09f,.10f,.09f), w.accent, .27f);
        for (int y=0;y<H;y++) for(int x=0;x<W;x++)
        {
            float nx=x/(float)W, ny=y/(float)H;
            float vignette = Mathf.Clamp01(1f - (Mathf.Abs(nx-.5f)*.95f + Mathf.Abs(ny-.55f)*.55f));
            float glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx,ny),new Vector2(.5f,.66f))*1.55f);
            Color c = Color.Lerp(new Color(.025f,.026f,.024f), bg1, .32f + glow*.42f);
            c *= .72f + vignette*.38f;
            t.SetPixel(x,y,c);
        }
        // Atmospheric workshop silhouettes / beams
        for(int i=0;i<18;i++)
        {
            int x=(int)(rng.NextDouble()*W), width=rng.Next(3,15);
            DrawRect(t,x,80,width,rng.Next(180,490),Color.Lerp(bg1,Color.black,.55f));
        }
        // Shoulders / coat
        Ellipse(t,256,170,195,170,w.coat);
        // Neck
        Ellipse(t,256,310,48,82,w.skin);
        // Ears
        Ellipse(t,184,432,24,43,w.skin); Ellipse(t,328,432,24,43,w.skin);
        // Hair behind head
        Ellipse(t,256,445,99,137,w.hair);
        // Face
        Ellipse(t,256,444,78,112,w.skin);
        // Hairline and hairstyle variation
        if (w.id % 3 == 0) { Ellipse(t,210,510,40,52,w.hair); Ellipse(t,276,525,65,40,w.hair); }
        else if (w.id % 3 == 1) { Ellipse(t,256,534,83,42,w.hair); DrawRect(t,185,478,27,83,w.hair); }
        else { Ellipse(t,256,538,75,36,w.hair); Ellipse(t,317,480,31,74,w.hair); }
        // Eyes, brows, nose, mouth
        DrawRect(t,218,451,22,5,Shade(w.hair,.48f)); DrawRect(t,272,451,22,5,Shade(w.hair,.48f));
        DrawRect(t,220,438,18,7,new Color(.12f,.10f,.08f)); DrawRect(t,274,438,18,7,new Color(.12f,.10f,.08f));
        DrawRect(t,252,415,8,28,Shade(w.skin,.72f));
        DrawRect(t,237,390,38,5,new Color(.39f,.16f,.12f));
        // Facial hair, glasses, freckles, etc.
        if (w.id % 4 == 1 || w.guild == Guild.Lumber && w.id % 2 == 0)
        { Ellipse(t,256,384,57,35,w.hair); DrawRect(t,238,396,36,9,w.skin); }
        if (w.id % 5 == 0 || w.id == 8 || w.id == 27)
        { Ring(t,228,438,18,13,w.accent,5); Ring(t,284,438,18,13,w.accent,5); DrawRect(t,245,437,22,4,w.accent); }
        if (w.id % 6 == 0) for(int i=0;i<16;i++) Ellipse(t,190+rng.Next(0,132),420+rng.Next(0,48),2,2,new Color(.42f,.19f,.11f));
        // Collar / lapels
        Triangle(t,215,278,256,205,250,303,Shade(w.coat,.65f));
        Triangle(t,297,278,256,205,262,303,Color.Lerp(w.coat,Color.white,.18f));
        // Guild-specific props
        if (w.guild == Guild.Ledger) { Ring(t,310,325,24,24,w.accent,5); DrawRect(t,305,300,10,28,w.accent); }
        if (w.guild == Guild.Lumber) { DrawRect(t,342,260,13,160,new Color(.30f,.17f,.08f)); Triangle(t,326,365,370,365,348,426,new Color(.66f,.67f,.63f)); }
        if (w.guild == Guild.Sawmill) { DrawRect(t,350,260,8,155,new Color(.54f,.43f,.27f)); for(int i=0;i<5;i++) DrawRect(t,345+i*4,365-i*13,25,3,w.accent); }
        // Frame glints and bottom darkening
        for(int i=0;i<W;i++){ t.SetPixel(i,0,RarityColor(w.rarity)); t.SetPixel(i,1,RarityColor(w.rarity)); t.SetPixel(i,H-1,RarityColor(w.rarity)); }
        for(int y=0;y<H;y++){ t.SetPixel(0,y,RarityColor(w.rarity)); t.SetPixel(1,y,RarityColor(w.rarity)); t.SetPixel(W-1,y,RarityColor(w.rarity)); }
        t.Apply(); return t;
    }

    private static List<Worker> MakeWorkers()
    {
        string[] names={"Нильс Кошелёк","Агата Медяк","Рюрик Грош","Сигрид Ларсен","Бьорн Тарн","Эльза Звонкая","Густав Пенс","Фрида Счётная","Брита Золотые Весы","Пекка Топорик","Мартта Щепка","Олле Пенёк","Кнут Бук","Ауне Ветка","Йоханна Корень","Эйнар Жила","Тор Глыба","Хольгер Кедровый","Тиль Опилка","Хельга Брусок","Ивар Рейка","Сольвейг Рубанок","Вильхельм Стамеска","Ингеборг Зубец","Дагни Шлиф","Ларс Пила","Ингрид Ровная"};
        int[] ages={24,31,45,38,52,29,57,48,61,20,27,41,36,33,39,50,44,70,22,35,47,30,55,42,34,52,55};
        string[] perks={"—","—","—","Выгодная сделка","Премия","Выгодная сделка • Счастливая сделка","Премия • Наставник","Выгодная сделка • Премия • Наставник","Все перки • Золотые весы","—","—","—","Тяжёлая рука","Двойной удар","Двойной удар • Вместительный","Тяжёлая рука • Золотое дерево","Двойной удар • Тяжёлая рука • Вместительный","Все перки • Поющий кедр","—","—","—","Быстрая пила","Запасливый","Двойная доска • Быстрая пила","Экономный • Запасливый","Двойная доска • Экономный • Быстрая пила","Все перки • Ровный распил"};
        string[] quotes={"Считаю даже ступеньки","Копейка рубль бережёт","Я не сплю, я прикидываю","Каждому по весу","Записано, значит, было","Слышишь? Это к удаче","В молодости я был богат. Дважды","Ошибка в счёте? Не у меня","Сначала честность, потом выгода. Выгода всё равно придёт","Он не тяжёлый, он просто длинный","Если не щепки, значит, не работа","Отдыхать тоже надо уметь","Топор сам знает, куда лететь","Два удара, два дерева. Ну почти","Что не влезло, то я поднимусь ещё раз","Золото в дереве есть, надо знать, где рубить","Не торопи дерево, оно само упадёт","Кедры поют. Ты слушай, а не руби","Это не мусор, это стружка","Ровно, или никак","Пилу не торопят","Чем быстрее, тем ровнее, если не дрожит рука","Лишних досок не бывает","Один распил, две доски","Бревно любит бережного","Пила поёт, если её слушать","Хорошая доска не нуждается в объяснениях"};
        string[] looks={"веснушки, жёлтая жилетка, счётная дощечка","круглые очки, тёмная коса, зелёный фартук","усы, фуражка, сонный взгляд","синий плащ, весы на поясе","шерстяной жилет, блокнот","рыжие кудри, красный шарф","седой, цилиндр, трость-монета","синий сюртук, золотое пенсне, счёты","седая коса с золотой нитью, янтарный плащ","мальчишка, великая шапка, большой топор","косынка в клетку, щепки в волосах","широкая борода, рубаха лесоруба","румяный, красная клетка, закатанные рукава","зелёная куртка, два ремня топора","плетёный короб, кожаные наплечники","жилистый, золотая щепка за ухом","великан, коса в бороде, огромный топор","седая борода с хвоей, кедровая шишка","очки на лбу, опилки на ресницах","синий комбинезон, карандаш за ухом","длинный, усы клином, пила на плече","жёлтая шапка, фартук с инструментами","жилет с карманами, запасные ящики","косичка короной, толстые перчатки","сиреневый платок, рулетка","седые виски, пила-смычок с гравировкой","серебряная коса, линейка-амулет"};
        var list = new List<Worker>();
        for(int i=0;i<27;i++)
        {
            int rank = i%9; Rarity r = rank==8?Rarity.Mythic:rank==7?Rarity.Legendary:rank>=5?Rarity.Rare:rank>=3?Rarity.Uncommon:Rarity.Common;
            Guild g = i<9?Guild.Ledger:i<18?Guild.Lumber:Guild.Sawmill;
            Color[] coats={new Color(.43f,.27f,.12f),new Color(.15f,.32f,.22f),new Color(.30f,.25f,.18f),new Color(.10f,.24f,.40f),new Color(.38f,.18f,.12f)};
            Color[] hairs={new Color(.22f,.12f,.06f),new Color(.10f,.08f,.06f),new Color(.40f,.25f,.12f),new Color(.66f,.48f,.28f),new Color(.65f,.65f,.59f)};
            Color skin = new Color(.76f,.54f,.39f);
            list.Add(new Worker{id=i+1,name=names[i],age=ages[i],rarity=r,guild=g,perks=perks[i],quote=quotes[i],look=looks[i],coat=coats[(i*3+i/4)%coats.Length],hair=hairs[(i*2+i/3)%hairs.Length],skin=skin,accent=RarityColor(r)});
        }
        return list;
    }

    private static Color RarityColor(Rarity r) { switch(r){case Rarity.Uncommon:return new Color(.34f,.72f,.29f);case Rarity.Rare:return new Color(.24f,.62f,.95f);case Rarity.Legendary:return new Color(.78f,.30f,.88f);case Rarity.Mythic:return new Color(1f,.67f,.18f);default:return new Color(.70f,.66f,.58f);} }
    private static string RarityName(Rarity r) { switch(r){case Rarity.Uncommon:return "Необычный";case Rarity.Rare:return "Редкий";case Rarity.Legendary:return "Легендарный";case Rarity.Mythic:return "Мифический";default:return "Обычный";} }
    private static string GuildName(Guild g) { return g==Guild.Ledger?"Казначеи":g==Guild.Lumber?"Дровосеки":"Пильщики"; }
    private static string Safe(string s) { foreach(char c in System.IO.Path.GetInvalidFileNameChars()) s=s.Replace(c,'_'); return s.Replace(' ','_'); }
    private static void EnsureFolder(string path) { string[] parts=path.Split('/'); string cur=parts[0]; for(int i=1;i<parts.Length;i++){string next=cur+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur,parts[i]); cur=next;} }

    private static GameObject AddImage(Transform parent,string name,Sprite sprite,Vector2 amin,Vector2 amax,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)); go.transform.SetParent(parent,false);
        var rt=go.GetComponent<RectTransform>(); rt.anchorMin=amin;rt.anchorMax=amax;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
        var im=go.GetComponent<Image>();im.sprite=sprite;im.color=color;im.preserveAspect=true;return go;
    }
    private static GameObject AddPanel(Transform parent,string name,Vector2 amin,Vector2 amax,Color color,Vector4 borders,bool raycast)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);
        var rt=go.GetComponent<RectTransform>();rt.anchorMin=amin;rt.anchorMax=amax;rt.offsetMin=new Vector2(borders.x,borders.y);rt.offsetMax=new Vector2(-borders.z,-borders.w);
        var im=go.GetComponent<Image>();im.color=color;im.raycastTarget=raycast;return go;
    }
    private static void AddText(Transform parent,string name,string value,int size,FontStyles style,Vector2 amin,Vector2 amax,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var rt=go.GetComponent<RectTransform>();rt.anchorMin=amin;rt.anchorMax=amax;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
        var text=go.GetComponent<TextMeshProUGUI>();text.text=value;text.fontSize=size;text.fontStyle=style;text.color=color;text.alignment=TextAlignmentOptions.Center;text.enableWordWrapping=true;text.overflowMode=TextOverflowModes.Ellipsis;text.raycastTarget=false;
    }

    private static Color Shade(Color c,float k)=>new Color(c.r*k,c.g*k,c.b*k,c.a);
    private static void DrawRect(Texture2D t,int x,int y,int w,int h,Color c){for(int yy=Mathf.Max(0,y);yy<Mathf.Min(H,y+h);yy++)for(int xx=Mathf.Max(0,x);xx<Mathf.Min(W,x+w);xx++)t.SetPixel(xx,yy,c);}
    private static void Ellipse(Texture2D t,int cx,int cy,int rx,int ry,Color c){for(int y=Mathf.Max(0,cy-ry);y<Mathf.Min(H,cy+ry);y++)for(int x=Mathf.Max(0,cx-rx);x<Mathf.Min(W,cx+rx);x++){float dx=(x-cx)/(float)rx,dy=(y-cy)/(float)ry;if(dx*dx+dy*dy<=1)t.SetPixel(x,y,c);}}
    private static void Ring(Texture2D t,int cx,int cy,int rx,int ry,Color c,int thickness){for(int y=Mathf.Max(0,cy-ry);y<Mathf.Min(H,cy+ry);y++)for(int x=Mathf.Max(0,cx-rx);x<Mathf.Min(W,cx+rx);x++){float dx=(x-cx)/(float)rx,dy=(y-cy)/(float)ry;float d=dx*dx+dy*dy;if(d<=1 && d>=Mathf.Pow(1f-(float)thickness/Mathf.Max(rx,ry),2))t.SetPixel(x,y,c);}}
    private static void Triangle(Texture2D t,int x1,int y1,int x2,int y2,int x3,int y3,Color c){int minX=Mathf.Min(x1,Mathf.Min(x2,x3)),maxX=Mathf.Max(x1,Mathf.Max(x2,x3)),minY=Mathf.Min(y1,Mathf.Min(y2,y3)),maxY=Mathf.Max(y1,Mathf.Max(y2,y3));for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){float a=((y2-y3)*(x-x3)+(x3-x2)*(y-y3))/((float)(y2-y3)*(x1-x3)+(x3-x2)*(y1-y3)+.0001f);float b=((y3-y1)*(x-x3)+(x1-x3)*(y-y3))/((float)(y2-y3)*(x1-x3)+(x3-x2)*(y1-y3)+.0001f);float cc=1-a-b;if(a>=0&&b>=0&&cc>=0)t.SetPixel(x,y,c);}}
}
