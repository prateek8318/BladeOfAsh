using System.Collections.Generic;
using UnityEngine;

public enum HitResult { Ignored, Dodged, Parried, Blocked, Hit }

// Telegraphed ground attack: a glowing circle warns the player, then it erupts.
public class Hazard : MonoBehaviour
{
    public enum Kind { Fire, Ice, Lightning }

    Kind kind;
    float radius, delay, damage, t;
    Enemy owner;
    Transform disc, core;

    public static void Spawn(Vector3 pos, float radius, float delay, float damage, Kind kind, Enemy owner)
    {
        var go = new GameObject("Hazard_" + kind);
        go.transform.position = new Vector3(pos.x, 0.03f, pos.z);
        var h = go.AddComponent<Hazard>();
        h.kind = kind; h.radius = radius; h.delay = delay; h.damage = damage; h.owner = owner;
        h.disc = CharacterBuilder.Part(null, PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f), GameAssets.I.telegraph);
        h.core = CharacterBuilder.Part(null, PrimitiveType.Cylinder, go.transform, Vector3.up * 0.01f, Vector3.zero, GameAssets.I.telegraph);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / delay);
        float r = radius * 2f * k;
        core.localScale = new Vector3(r, 0.012f, r);
        disc.localScale = new Vector3(radius * 2f, 0.01f + 0.01f * Mathf.Sin(t * 30f), radius * 2f);
        if (t < delay) return;

        var pos = transform.position;
        var A = GameAssets.I;
        switch (kind)
        {
            case Kind.Lightning:
                LightningBolt.Create(pos + Vector3.up * 28f + Random.insideUnitSphere * 3f, pos, A.lightning, 0.35f, 0.3f);
                FX.Ring(pos, A.lightning, radius * 1.3f, 0.3f);
                FX.Shards(pos, A.lightning, 8, 7f, 0.08f);
                SFX.Play(SFX.S.Thunder, 0.7f, 1.1f);
                FX.Shake(0.5f);
                break;
            case Kind.Ice:
                IceSpike.Create(pos, radius);
                FX.Shards(pos, A.iceShard, 10, 6f, 0.12f);
                SFX.Play(SFX.S.Shatter, 0.6f);
                FX.Shake(0.3f);
                break;
            default:
                FX.Ring(pos, A.shockwave, radius * 1.3f, 0.35f);
                FX.Shards(pos, A.shard, 12, 8f, 0.14f);
                SFX.Play(SFX.S.Boom, 0.6f, 1.2f);
                FX.Shake(0.4f);
                break;
        }

        var p = PlayerController.I;
        if (p != null && !p.health.IsDead)
        {
            var d = p.transform.position - pos; d.y = 0;
            if (d.magnitude <= radius + 0.35f && p.transform.position.y < 1.8f)
            {
                var res = p.ReceiveAttack(pos, owner, damage, 12f, true);
                if (res == HitResult.Hit && kind == Kind.Ice) p.Chill(2f);
            }
        }
        Destroy(gameObject);
    }
}

public class IceSpike : MonoBehaviour
{
    float t;
    float height;

    public static void Create(Vector3 pos, float radius)
    {
        var go = new GameObject("IceSpike");
        go.transform.position = pos;
        var s = go.AddComponent<IceSpike>();
        s.height = 2.2f + radius;
        for (int i = 0; i < 4; i++)
        {
            var off = i == 0 ? Vector3.zero : Quaternion.Euler(0, i * 120f + Random.Range(-20, 20), 0) * Vector3.forward * radius * 0.45f;
            float h = i == 0 ? s.height : s.height * Random.Range(0.45f, 0.7f);
            var spike = CharacterBuilder.Part(null, PrimitiveType.Cube, go.transform, off + Vector3.up * h * 0.5f,
                new Vector3(0.45f, h, 0.45f) * (i == 0 ? 1.3f : 0.9f), GameAssets.I.crystal,
                new Vector3(Random.Range(-15f, 15f) + (i == 0 ? 0 : 18f), Random.Range(0, 360f), Random.Range(-15f, 15f)));
            spike.localRotation = Quaternion.LookRotation(off.sqrMagnitude > 0 ? off : Vector3.forward) * Quaternion.Euler(i == 0 ? 0 : 20f, 45f, 0);
        }
        go.transform.localScale = new Vector3(1, 0.01f, 1);
    }

    void Update()
    {
        t += Time.deltaTime;
        float y = t < 0.12f ? t / 0.12f : (t > 1.4f ? Mathf.Clamp01(1f - (t - 1.4f) / 0.4f) : 1f);
        transform.localScale = new Vector3(1, Mathf.Max(0.01f, y), 1);
        if (t > 1.8f) Destroy(gameObject);
    }
}

// Jagged lightning made of stretched glowing cubes, flickering for a moment.
public class LightningBolt : MonoBehaviour
{
    float life, maxLife;
    Light flash;
    readonly List<Renderer> segs = new List<Renderer>();

    public static void Create(Vector3 from, Vector3 to, Material mat, float width = 0.2f, float life = 0.25f)
    {
        var go = new GameObject("Lightning");
        var lb = go.AddComponent<LightningBolt>();
        lb.life = lb.maxLife = life;
        Vector3 dir = to - from;
        float len = dir.magnitude;
        int n = Mathf.Clamp(Mathf.CeilToInt(len / 1.6f), 3, 18);
        Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        Vector3 side2 = Vector3.Cross(dir.normalized, side);
        Vector3 prev = from;
        for (int i = 1; i <= n; i++)
        {
            float k = i / (float)n;
            Vector3 p = Vector3.Lerp(from, to, k);
            if (i < n) p += (side * Random.Range(-1f, 1f) + side2 * Random.Range(-1f, 1f)) * Mathf.Min(1.4f, len * 0.08f);
            var seg = p - prev;
            var t = CharacterBuilder.Part(null, PrimitiveType.Cube, go.transform, (prev + p) * 0.5f, new Vector3(width, width, seg.magnitude + width), mat);
            t.rotation = Quaternion.LookRotation(seg.normalized);
            lb.segs.Add(t.GetComponent<Renderer>());
            // small forks
            if (i < n && Random.value < 0.35f)
            {
                var fdir = (seg.normalized + Random.insideUnitSphere * 1.2f).normalized * Random.Range(0.8f, 2f);
                var f = CharacterBuilder.Part(null, PrimitiveType.Cube, go.transform, p + fdir * 0.5f, new Vector3(width * 0.6f, width * 0.6f, fdir.magnitude), mat);
                f.rotation = Quaternion.LookRotation(fdir.normalized);
                lb.segs.Add(f.GetComponent<Renderer>());
            }
            prev = p;
        }
        var lg = new GameObject("Flash");
        lg.transform.SetParent(go.transform, false);
        lg.transform.position = to + Vector3.up;
        lb.flash = lg.AddComponent<Light>();
        lb.flash.type = LightType.Point;
        lb.flash.color = new Color(0.6f, 0.8f, 1f);
        lb.flash.range = 14f;
        lb.flash.intensity = 7f;
    }

    void Update()
    {
        life -= Time.unscaledDeltaTime;
        bool on = Random.value > 0.25f;
        foreach (var r in segs) if (r != null) r.enabled = on || life > maxLife * 0.7f;
        if (flash != null) flash.intensity = 7f * Mathf.Clamp01(life / maxLife);
        if (life <= 0f) Destroy(gameObject);
    }
}

// Magic projectile fired by enemies. A perfect parry sends it back at its caster.
public class EnemyBolt : MonoBehaviour
{
    Enemy owner;
    Vector3 vel;
    float damage, life = 4f, homing;
    bool reflected, chill;
    Light glow;

    public static void Fire(Enemy owner, Vector3 from, Vector3 dir, float speed, float damage, Material mat, float homing = 0f, bool chill = false, float size = 0.45f)
    {
        var go = new GameObject("Bolt");
        go.transform.position = from;
        var b = go.AddComponent<EnemyBolt>();
        b.owner = owner; b.vel = dir.normalized * speed; b.damage = damage; b.homing = homing; b.chill = chill;
        CharacterBuilder.Part(null, PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * size, mat);
        CharacterBuilder.Part(null, PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one * size * 0.7f, mat, new Vector3(45, 45, 0));
        b.glow = go.AddComponent<Light>();
        b.glow.type = LightType.Point;
        b.glow.color = mat.color;
        b.glow.range = 4f; b.glow.intensity = 2.5f;
        var tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = GameAssets.I.trail;
        tr.time = 0.25f; tr.startWidth = size * 0.8f; tr.endWidth = 0f;
        var c = b.glow.color;
        tr.startColor = new Color(c.r, c.g, c.b, 0.9f); tr.endColor = new Color(c.r, c.g, c.b, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        var p = PlayerController.I;
        if (!reflected && homing > 0f && p != null && !p.health.IsDead)
        {
            var want = (p.transform.position + Vector3.up * 1.2f - transform.position).normalized * vel.magnitude;
            vel = Vector3.RotateTowards(vel, want, homing * dt, 0f);
        }
        var step = vel * dt;
        transform.Rotate(720f * dt, 360f * dt, 0f);

        // walls / ground
        if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + 0.2f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<Enemy>() == null && hit.collider.GetComponentInParent<PlayerController>() == null)
        {
            Burst(); return;
        }
        transform.position += step;
        if (transform.position.y < 0.05f || life <= 0f) { Burst(); return; }

        if (!reflected)
        {
            if (p != null && !p.health.IsDead && Vector3.Distance(transform.position, p.transform.position + Vector3.up * 1.1f) < 1.0f)
            {
                var res = p.ReceiveAttack(transform.position - vel.normalized, null, damage, 6f);
                if (res == HitResult.Parried)
                {
                    reflected = true;
                    Vector3 target = owner != null && !owner.health.IsDead ? owner.transform.position + Vector3.up * 1.5f : transform.position - vel;
                    vel = (target - transform.position).normalized * vel.magnitude * 1.8f;
                    life = 3f;
                    GameManager.I?.ShowMessage("REFLECTED!", 0.8f);
                    return;
                }
                if (res == HitResult.Dodged) return;
                if (res == HitResult.Hit && chill) p.Chill(2f);
                Burst();
            }
        }
        else
        {
            foreach (var c in Physics.OverlapSphere(transform.position, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var e = c.GetComponentInParent<Enemy>();
                if (e == null || e.health.IsDead) continue;
                float dmg = damage * 3.5f;
                if (e.health.TakeDamage(new DamageInfo { amount = dmg, source = transform.position - vel, knockback = 10f, heavy = true, fromPlayer = true }))
                    p?.RegisterHit(dmg);
                e.Stagger(1.2f, true);
                Burst();
                return;
            }
        }
    }

    void Burst()
    {
        FX.Shards(transform.position, GameAssets.I.bolt, 6, 4f, 0.08f);
        Destroy(gameObject);
    }
}

// Gentle bobbing / spinning for floating rocks and crystals.
public class Floater : MonoBehaviour
{
    public float amp = 0.6f, speed = 0.6f, spin = 8f;
    Vector3 basePos; float seed;
    void Start() { basePos = transform.position; seed = Random.value * 10f; }
    void Update()
    {
        transform.position = basePos + Vector3.up * Mathf.Sin((Time.time + seed) * speed) * amp;
        transform.Rotate(0, spin * Time.deltaTime, 0, Space.World);
    }
}

// Random distant lightning strikes + sky flashes for the storm realm.
public class StormAmbience : MonoBehaviour
{
    float next = 2f;
    Light sky;
    void Start()
    {
        sky = new GameObject("SkyFlash").AddComponent<Light>();
        sky.type = LightType.Directional;
        sky.transform.rotation = Quaternion.Euler(70, 20, 0);
        sky.color = new Color(0.7f, 0.75f, 1f);
        sky.intensity = 0f;
        sky.shadows = LightShadows.None;
    }
    void Update()
    {
        sky.intensity = Mathf.MoveTowards(sky.intensity, 0f, Time.deltaTime * 6f);
        next -= Time.deltaTime;
        if (next > 0f) return;
        next = Random.Range(2.5f, 7f);
        float a = Random.Range(0f, Mathf.PI * 2f);
        var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Random.Range(45f, 80f);
        LightningBolt.Create(p + Vector3.up * 70f, p + Vector3.down * 10f, GameAssets.I.lightning, 0.9f, 0.35f);
        sky.intensity = 1.6f;
        SFX.Play(SFX.S.Thunder, 0.35f, 0.7f, 0.15f);
    }
}
