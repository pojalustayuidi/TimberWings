#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShopFrontPolish
{
    const string EditName = "Лавка: открыть обзор на Рико";
    const float CounterHeight = .72f;

    [MenuItem("Tools/BuilderSim/Здания/Лавка — открыть обзор на Рико", false, 301)]
    static void OpenSceneCounter()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Лавка: выйди из Play Mode и повтори команду.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        var shop = Selection.activeGameObject != null && Selection.activeGameObject.scene == scene
            ? Selection.activeGameObject.GetComponentInParent<ShopLife>(true) : null;
        if (shop == null)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<ShopLife>(true))
            {
                if (shop != null)
                {
                    Debug.LogWarning("Лавка: выбери нужный TradingStall в Hierarchy.");
                    return;
                }
                shop = candidate;
            }
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(EditName);
        if (!Apply(shop, false))
        {
            Debug.LogWarning("Лавка: в активной сцене нужен TradingStall с прилавком из генератора v2.", shop);
            return;
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = shop.gameObject;
        Debug.Log("Лавка: обзор на Рико открыт. Сохрани сцену (Ctrl+S).", shop);
    }

    public static bool Apply(ShopLife shop, bool freshlyBuilt)
    {
        if (shop == null) return false;
        var stages = shop.stages != null ? shop.stages : shop.GetComponent<ShopStages>();
        if (stages == null || stages.stage2 == null || stages.stage3 == null) return false;
        var frame = stages.stage2.transform;
        var finish = stages.stage3.transform;
        if (frame.parent != shop.transform || finish.parent != shop.transform) return false;
        var top = frame.Find("CounterTop");
        var colliderRoot = frame.Find("CounterCollider");
        var collider = colliderRoot != null ? colliderRoot.GetComponent<BoxCollider>() : null;
        if (top == null || collider == null || colliderRoot.localScale.y <= .001f) return false;

        float colliderScale = colliderRoot.localScale.y;
        float floor = colliderRoot.localPosition.y + (collider.center.y - collider.size.y * .5f) * colliderScale;
        float oldHeight = top.localPosition.y + top.localScale.y * .5f - floor;
        if (oldHeight <= .001f) return false;

        float height = Mathf.Min(CounterHeight, oldHeight);
        var props = finish.Find("CounterProps");
        if (oldHeight - height > .001f)
        {
            float ratio = height / oldHeight;
            foreach (Transform part in frame)
            {
                switch (part.name)
                {
                    case "CounterTop": case "CounterBody": case "CounterBoard": case "CounterSkirt":
                    case "CounterLip": case "CounterPostL": case "CounterPostR":
                        Undo.RecordObject(part, EditName);
                        var position = part.localPosition;
                        position.y = floor + (position.y - floor) * ratio;
                        part.localPosition = position;
                        var scale = part.localScale;
                        scale.y *= ratio;
                        part.localScale = scale;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                        break;
                }
            }

            Undo.RecordObject(collider, EditName);
            var size = collider.size;
            size.y = height / colliderScale;
            collider.size = size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            var colliderPosition = colliderRoot.localPosition;
            colliderPosition.y = floor + height * .5f - collider.center.y * colliderScale;
            Move(colliderRoot, frame.TransformPoint(colliderPosition));

            Vector3 drop = frame.TransformVector(Vector3.up * (height - oldHeight));
            if (props != null) Move(props, props.position + drop);
            var counterPoint = finish.Find("CounterPoint");
            if (counterPoint != null) Move(counterPoint, counterPoint.position + drop);
        }

        Undo.RecordObject(shop, EditName);
        RemoveDetails(frame, freshlyBuilt, "CounterFringe", "RackPost", "Shelf", "ShelfLip", "RackCloud", "Pallet");
        RemoveDetails(finish, freshlyBuilt, "Bunting", "Mascot", "Stock");
        RemoveDetails(finish.Find("Canopy"), freshlyBuilt, "Valance");
        RemoveDetails(props, freshlyBuilt, "Chest", "Scales", "StockCoins_0", "StockCoins_1", "StockCoins_2", "StockCoins_3", "GlintPoint");

        foreach (string name in new[] { "Stage1_Crew", "Stage2_Crew" })
        {
            var crew = shop.transform.Find(name);
            if (crew == null) continue;
            // HutStages can reactivate StageVisibility groups, even when inactive.
            var visibility = crew.GetComponent<StageVisibility>();
            if (visibility != null) Undo.DestroyObjectImmediate(visibility);
            RemoveDetail(crew, freshlyBuilt);
        }

        shop.mascot = shop.mascotBody = shop.mascotEyes = shop.mascotArm = null;
        shop.scalesBeam = shop.panL = shop.panR = null;
        shop.glintPoint = shop.goldStar = null;
        shop.logTiers = new ShopLife.Tiered();
        shop.plankTiers = new ShopLife.Tiered();
        shop.coinTiers = new ShopLife.Tiered();
        PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
        return true;
    }

    static void Move(Transform part, Vector3 worldPosition)
    {
        Undo.RecordObject(part, EditName);
        part.position = worldPosition;
        PrefabUtility.RecordPrefabInstancePropertyModifications(part);
    }

    static void RemoveDetails(Transform parent, bool freshlyBuilt, params string[] names)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (System.Array.IndexOf(names, child.name) >= 0) RemoveDetail(child, freshlyBuilt);
        }
    }

    static void RemoveDetail(Transform part, bool freshlyBuilt)
    {
        if (freshlyBuilt || !PrefabUtility.IsPartOfPrefabInstance(part.gameObject))
        {
            Undo.DestroyObjectImmediate(part.gameObject);
            return;
        }
        if (!part.gameObject.activeSelf) return;
        Undo.RecordObject(part.gameObject, EditName);
        part.gameObject.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
    }
}
#endif
