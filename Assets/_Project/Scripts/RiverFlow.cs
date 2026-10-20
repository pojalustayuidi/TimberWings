using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class RiverFlow : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.02f, -0.07f);

    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int MainTex = Shader.PropertyToID("_MainTex");
    static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

    Material mat;
    Vector2 offset;
    bool hasBase, hasMain, hasBump;

    void Start()
    {
        mat = GetComponent<Renderer>().material;   // свой экземпляр, ассет не меняется
        hasBase = mat.HasProperty(BaseMap);
        hasMain = mat.HasProperty(MainTex);
        hasBump = mat.HasProperty(BumpMap);
    }

    void Update()
    {
        if (mat == null) return;
        offset += speed * Time.deltaTime;
        offset.x = Mathf.Repeat(offset.x, 1f);
        offset.y = Mathf.Repeat(offset.y, 1f);

        if (hasBase) mat.SetTextureOffset(BaseMap, offset);              // URP
        if (hasMain) mat.SetTextureOffset(MainTex, offset);
        if (hasBump && !hasBase) mat.SetTextureOffset(BumpMap, offset);  // Built-in
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}