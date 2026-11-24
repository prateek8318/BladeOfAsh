using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Game-feel helpers: screen shake, hit-stop, debris, shockwave rings, floating damage numbers.
public static class FX
{
    public static float shake;

    static FXRunner runner;
    static FXRunner Runner
    {
        get
        {
            if (runner == null)
            {
                var go = new GameObject("FXRunner");
                runner = go.AddComponent<FXRunner>();
            }
            return runner;
        }
    }

    public static void Shake(float amount) { shake = Mathf.Max(shake, amount); }

    public static void HitStop(float seconds) { Runner.DoHitStop(seconds); }

    // Brief slow-motion (perfect dodge, boss kill). Stacks with hit-stop.
    public static void SlowMo(float realSeconds, float scale)
    {
        Runner.slowTimer = Mathf.Max(Runner.slowTimer, realSeconds);
        Runner.slowScale = scale;
    }

    public static void Shards(Vector3 pos, Material mat, int count, float force, float size = 0.12f)
    {
        if (mat == null) return;
        for (int i = 0; i < count; i++)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.position = pos + Random.insideUnitSphere * 0.3f;
            g.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.4f);
            g.transform.rotation = Random.rotation;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            var s = g.AddComponent<Shard>();
            var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.4f;
            s.velocity = dir.normalized * force * Random.Range(0.5f, 1.2f);
        }
    }

    public static void Ring(Vector3 pos, Material mat, float radius, float duration)
    {
        if (mat == null) return;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.position = new Vector3(pos.x, 0.05f, pos.z);
        g.GetComponent<Renderer>().sharedMaterial = mat;
        var r = g.AddComponent<ExpandingRing>();
        r.radius = radius; r.duration = duration;
    }

    public static void DamageNumber(Vector3 pos, float amount, bool onPlayer)
    {
        Runner.numbers.Add(new FXRunner.Number
        {
            pos = pos + Random.insideUnitSphere * 0.3f,
            text = Mathf.RoundToInt(amount).ToString(),
            color = onPlayer ? new Color(1f, 0.3f, 0.3f) : (amount >= 30 ? new Color(1f, 0.75f, 0.2f) : Color.white),
            big = amount >= 30,
            t = 0f
        });
    }
}

public class FXRunner : MonoBehaviour
{
    public struct Number { public Vector3 pos; public string text; public Color color; public bool big; public float t; }
    public readonly List<Number> numbers = new List<Number>();
    public float slowTimer, slowScale = 1f;
    bool stopping;
    GUIStyle style;

    public void DoHitStop(float seconds)
    {
        if (!stopping) StartCoroutine(HitStopRoutine(seconds));
    }

    IEnumerator HitStopRoutine(float seconds)
    {
        stopping = true;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(seconds);
        if (Time.timeScale > 0f) Time.timeScale = slowTimer > 0f ? slowScale : 1f;
        stopping = false;
    }

    void Update()
    {
        FX.shake = Mathf.MoveTowards(FX.shake, 0f, Time.unscaledDeltaTime * 2.5f);
        if (slowTimer > 0f)
        {
            slowTimer -= Time.unscaledDeltaTime;
            if (!stopping && Time.timeScale > 0f) Time.timeScale = slowTimer > 0f ? slowScale : 1f;
        }
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            var n = numbers[i];
            n.t += Time.unscaledDeltaTime;
            n.pos += Vector3.up * Time.unscaledDeltaTime * 1.2f;
            if (n.t > 0.9f) numbers.RemoveAt(i); else numbers[i] = n;
        }
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        foreach (var n in numbers)
        {
            var sp = cam.WorldToScreenPoint(n.pos);
            if (sp.z < 0) continue;
            style.fontSize = n.big ? 30 : 22;
            float a = 1f - Mathf.Clamp01((n.t - 0.5f) / 0.4f);
            var rect = new Rect(sp.x - 60, Screen.height - sp.y - 20, 120, 40);
            GUI.color = new Color(0, 0, 0, a * 0.8f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), n.text, style);
            GUI.color = new Color(n.color.r, n.color.g, n.color.b, a);
            GUI.Label(rect, n.text, style);
        }
        GUI.color = Color.white;
    }
}

public class Shard : MonoBehaviour
{
    public Vector3 velocity;
    float life;
    Vector3 spin;
    Vector3 startScale;

    void Start()
    {
        spin = Random.insideUnitSphere * 720f;
        startScale = transform.localScale;
        life = Random.Range(0.6f, 1.1f);
    }

    void Update()
    {
        velocity += Physics.gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        if (transform.position.y < 0.05f)
        {
            transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            velocity = new Vector3(velocity.x * 0.5f, -velocity.y * 0.3f, velocity.z * 0.5f);
        }
        transform.Rotate(spin * Time.deltaTime);
        life -= Time.deltaTime;
        transform.localScale = startScale * Mathf.Clamp01(life * 3f);
        if (life <= 0f) Destroy(gameObject);
    }
}

public class ExpandingRing : MonoBehaviour
{
    public float radius = 4f;
    public float duration = 0.4f;
    float t;

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        float r = Mathf.Lerp(0.3f, radius, 1f - (1f - k) * (1f - k));
        float h = Mathf.Lerp(0.08f, 0.01f, k);
        transform.localScale = new Vector3(r * 2f, h, r * 2f);
        if (t >= duration) Destroy(gameObject);
    }
}
