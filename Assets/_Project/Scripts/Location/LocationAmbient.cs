// Живёт на объекте Location/Polish (его создаёт LocationPolish).
//  1. передаёт позицию игрока в шейдер травы (_LocPlayer) -> трава расступается
//  2. пыль из-под ног при беге
//  3. мерцание пламени костра: меши "Mesh_Flame*" получают единый LightFlicker (пресет Campfire).
//     Если пламя уже ведёт CampfireFlicker/LightFlicker выше по иерархии, второй раз его не трогаем (раньше мерцало дважды).
//     Свет костра («camp» в имени) больше не мерцает здесь: его ведёт FireSource/FireLightPool, формулу даёт LightFlicker.
using System.Collections.Generic;
using UnityEngine;

public class LocationAmbient : MonoBehaviour
{
    public static Transform Player;

    [Tooltip("Если пусто: ищем объект с тегом Player, затем CharacterController")]
    public Transform playerOverride;
    public string playerTag = "Player";
    public ParticleSystem footDust;
    public float bendRadius = 1.4f;
    public bool flickerFire = true;

    static readonly int PlayerId = Shader.PropertyToID("_LocPlayer");

    float findTimer, dustTimer;
    Vector3 lastPos;
    bool hasLast;

    // при выключенном Domain Reload статики иначе переживают выход из Play Mode
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Player = null; }

    void Start()
    {
        if (flickerFire) CollectFire();
        FindPlayer();
    }

    // раньше тут были свои массивы света и пламени и свой Perlin. Теперь только навешиваем единый LightFlicker на языки пламени
    void CollectFire()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Mesh_Flame")) continue;
            // пламя уже ведёт LightFlicker или CampfireFlicker (он добавит LightFlicker сам, когда Stage3 включится)
            if (t.GetComponentInParent<LightFlicker>(true) != null || t.GetComponentInParent<CampfireFlicker>(true) != null) continue;
            var lf = t.gameObject.AddComponent<LightFlicker>();
            lf.Configure(FlickerPreset.Campfire, null, new[] { t });
        }
    }

    void FindPlayer()
    {
        if (playerOverride != null) { Player = playerOverride; return; }
        if (Player != null) return;
        try
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) Player = go.transform;
        }
        catch (UnityException) { /* тега нет в проекте */ }
        if (Player == null)
        {
            var cc = FindFirstObjectByType<CharacterController>();
            if (cc != null) Player = cc.transform;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (Player == null)
        {
            Shader.SetGlobalVector(PlayerId, Vector4.zero);
            findTimer -= dt;
            if (findTimer <= 0f) { findTimer = 2f; FindPlayer(); }
            return;
        }

        Vector3 p = Player.position;
        Shader.SetGlobalVector(PlayerId, new Vector4(p.x, p.y, p.z, bendRadius));

        if (footDust != null && hasLast && dt > 0f)
        {
            Vector3 d = p - lastPos;
            d.y = 0f;
            float speed = d.magnitude / dt;
            dustTimer -= dt;
            if (speed > 0.8f && speed < 30f && dustTimer <= 0f)
            {
                var ep = new ParticleSystem.EmitParams();
                ep.position = p + Vector3.up * 0.05f;
                footDust.Emit(ep, 2);
                dustTimer = 0.16f;
            }
        }
        lastPos = p;
        hasLast = true;
    }

    void OnDisable()
    {
        Shader.SetGlobalVector(PlayerId, Vector4.zero);
    }
}