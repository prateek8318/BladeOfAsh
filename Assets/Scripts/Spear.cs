using System.Collections.Generic;
using UnityEngine;

// Storm-spear: thrown with E, sticks into enemies/walls (freezing enemies it pierces),
// recalled with E — it flies back along a curve and damages everything in its path.
public class Spear : MonoBehaviour
{
    enum State { Flying, Stuck, Returning }
    State state;
    PlayerController owner;
    Vector3 dir;
    float speed = 48f;
    float travelled;
    Enemy stuckIn;

    Vector3 returnStart, returnControl;
    float returnT, returnDuration;
    readonly HashSet<Enemy> returnHits = new HashSet<Enemy>();
    Light glow;

    public void Launch(PlayerController p, Vector3 direction)
    {
        owner = p;
        dir = direction;
        state = State.Flying;
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.4f, 0.8f, 1f);
        glow.range = 4f;
        glow.intensity = 2f;
    }

    void Update()
    {
        switch (state)
        {
            case State.Flying: Fly(); break;
            case State.Returning: Return(); break;
            case State.Stuck:
                if (stuckIn != null && stuckIn.health.IsDead) { transform.SetParent(null); stuckIn = null; }
                break;
        }
    }

    void Fly()
    {
        float step = speed * Time.deltaTime;
        var hits = Physics.SphereCastAll(transform.position, 0.3f, dir, step, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; RaycastHit? first = null;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (h.distance < best) { best = h.distance; first = h; }
        }
        if (first.HasValue)
        {
            var h = first.Value;
            transform.position = h.point == Vector3.zero ? transform.position + dir * h.distance : h.point - dir * 0.6f;
            var e = h.collider.GetComponentInParent<Enemy>();
            if (e != null && !e.health.IsDead)
            {
                float dmg = 34f * owner.OutgoingMul;
                if (e.health.TakeDamage(new DamageInfo { amount = dmg, source = transform.position - dir, knockback = 7f, heavy = true, fromPlayer = true }))
                    owner.RegisterHit(dmg);
                e.Freeze(3.5f);
                FX.HitStop(0.06f);
                FX.Shake(0.35f);
                FX.Shards(transform.position, GameAssets.I.spear, 10, 6f, 0.08f);
                SFX.Play(SFX.S.HeavyHit, 0.8f, 1.2f);
                if (!e.health.IsDead) { transform.SetParent(e.transform, true); stuckIn = e; }
            }
            else
            {
                FX.Shards(transform.position, GameAssets.I.shard, 6, 4f, 0.08f);
            }
            state = State.Stuck;
            glow.intensity = 1f;
            return;
        }
        transform.position += dir * step;
        travelled += step;
        dir = (dir + Vector3.down * 0.004f).normalized; // slight drop
        transform.rotation = Quaternion.LookRotation(dir);
        if (travelled > 70f || transform.position.y < 0f) { state = State.Stuck; }
    }

    public void Recall(PlayerController p)
    {
        if (state == State.Returning) return;
        owner = p;
        transform.SetParent(null, true);
        stuckIn = null;
        state = State.Returning;
        returnStart = transform.position;
        var target = owner.HandLPosition;
        float dist = Vector3.Distance(returnStart, target);
        returnDuration = Mathf.Clamp(dist / 38f, 0.2f, 0.9f);
        var side = Vector3.Cross(Vector3.up, (target - returnStart).normalized);
        returnControl = (returnStart + target) * 0.5f + side * dist * 0.25f + Vector3.up * 1.5f;
        returnT = 0f;
        returnHits.Clear();
        glow.intensity = 3f;
        SFX.Play(SFX.S.Blink, 0.5f, 0.6f);
    }

    void Return()
    {
        returnT += Time.deltaTime / returnDuration;
        var target = owner.HandLPosition;
        float t = Mathf.Clamp01(returnT);
        var prev = transform.position;
        var a = Vector3.Lerp(returnStart, returnControl, t);
        var b = Vector3.Lerp(returnControl, target, t);
        transform.position = Vector3.Lerp(a, b, t);
        var v = transform.position - prev;
        if (v.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-v.normalized);

        foreach (var c in Physics.OverlapSphere(transform.position, 1.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            var e = c.GetComponentInParent<Enemy>();
            if (e == null || e.health.IsDead || returnHits.Contains(e)) continue;
            returnHits.Add(e);
            float dmg = 22f * owner.OutgoingMul;
            if (e.health.TakeDamage(new DamageInfo { amount = dmg, source = transform.position - v.normalized, knockback = 6f, fromPlayer = true }))
                owner.RegisterHit(dmg);
            FX.Shards(transform.position, GameAssets.I.spear, 6, 4f, 0.07f);
        }

        if (t >= 1f)
        {
            owner.CatchSpear();
            Destroy(gameObject);
        }
    }
}
