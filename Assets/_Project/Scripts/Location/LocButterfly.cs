// Assets/_Project/Scripts/Location/LocButterfly.cs   (НЕ в папке Editor!)
// Бабочка летает по лиссажу вокруг цветочной поляны, держится над землёй и улетает от игрока.
using UnityEngine;

public class LocButterfly : MonoBehaviour
{
    public Vector3 homeLocal;
    public float radius = 3f;
    public float speed = 0.7f;
    public float height = 0.7f;
    public float phase;
    public float flapSpeed = 14f;
    public Transform wingL, wingR;

    Vector3 home, pos;
    Terrain terrain;
    float flee;

    void Start()
    {
        home = transform.parent != null ? transform.parent.TransformPoint(homeLocal) : homeLocal;
        terrain = Terrain.activeTerrain;
        pos = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time * speed + phase;

        Vector3 tg = home + new Vector3(
            Mathf.Sin(t * 0.7f) * radius + Mathf.Sin(t * 1.9f) * radius * 0.25f, 0f,
            Mathf.Cos(t * 0.55f) * radius + Mathf.Cos(t * 1.3f) * radius * 0.25f);

        float ground = terrain != null ? terrain.SampleHeight(tg) + terrain.transform.position.y : home.y;

        float fleeTarget = 0f;
        var pl = LocationAmbient.Player;
        if (pl != null)
        {
            Vector3 d = tg - pl.position;
            d.y = 0f;
            float m = d.magnitude;
            const float R = 2.2f;
            if (m < R)
            {
                fleeTarget = 1f - m / R;
                tg += d / Mathf.Max(m, 0.1f) * fleeTarget * 1.5f;
            }
        }
        flee = Mathf.MoveTowards(flee, fleeTarget, dt * 2f);

        tg.y = ground + height + Mathf.Sin(Time.time * 2.1f + phase) * 0.12f + flee * 0.9f;

        Vector3 np = Vector3.Lerp(pos, tg, 1f - Mathf.Exp(-dt * 3f));
        Vector3 vel = np - pos;
        pos = np;
        transform.position = pos;

        vel.y = 0f;
        if (vel.sqrMagnitude > 1e-8f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vel), 1f - Mathf.Exp(-dt * 6f));

        if (wingR != null && wingL != null)
        {
            float a = 30f + 35f * Mathf.Sin(Time.time * flapSpeed * (1f + flee * 0.5f) + phase * 3f);
            wingR.localRotation = Quaternion.Euler(0f, 0f, a);
            wingL.localRotation = Quaternion.Euler(0f, 0f, -a);
        }
    }
}
