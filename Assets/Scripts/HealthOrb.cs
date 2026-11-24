using UnityEngine;

public class HealthOrb : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<HealthOrb> All = new System.Collections.Generic.List<HealthOrb>();
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    float t;
    Vector3 basePos;

    public static void Spawn(Vector3 pos)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = "HealthOrb";
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * 0.45f;
        g.GetComponent<Renderer>().sharedMaterial = GameAssets.I.healOrb;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.3f, 1f, 0.4f); l.range = 3f; l.intensity = 2f;
        g.AddComponent<HealthOrb>().basePos = pos;
    }

    void Update()
    {
        t += Time.deltaTime;
        var p = PlayerController.I;
        if (p != null && !p.health.IsDead)
        {
            var d = Vector3.Distance(p.transform.position + Vector3.up, transform.position);
            if (d < 4f) basePos = Vector3.MoveTowards(basePos, p.transform.position + Vector3.up, Time.deltaTime * 10f);
            if (d < 1.1f)
            {
                p.health.Heal(35f);
                SFX.Play(SFX.S.Pickup, 0.6f);
                FX.Shards(transform.position, GameAssets.I.healOrb, 8, 3f, 0.07f);
                Destroy(gameObject);
                return;
            }
        }
        transform.position = basePos + Vector3.up * Mathf.Sin(t * 3f) * 0.2f;
        if (t > 25f) Destroy(gameObject);
    }
}
