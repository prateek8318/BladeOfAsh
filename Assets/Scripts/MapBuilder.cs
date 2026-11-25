using UnityEngine;
using UnityEngine.Rendering;

// Builds one of the three realms at runtime (geometry, lights, fog, weather).
public static class MapBuilder
{
    static Transform root;
    static System.Random rnd;

    static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    public static void Build(int map)
    {
        if (root != null) Object.Destroy(root.gameObject);
        root = new GameObject("World").transform;
        rnd = new System.Random(7 + map * 31);
        switch (map)
        {
            case 1: Frostveil(); break;
            case 2: Stormspire(); break;
            default: Colosseum(); break;
        }
        // Invisible boundary so nobody leaves the arena.
        for (int i = 0; i < 24; i++)
        {
            float a = (i + 0.5f) / 24f * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(a) * 29.5f, 4f, Mathf.Sin(a) * 29.5f);
            var w = new GameObject("Boundary").AddComponent<BoxCollider>();
            w.transform.SetParent(root, false);
            w.transform.position = pos;
            w.transform.rotation = Quaternion.LookRotation(-new Vector3(pos.x, 0, pos.z).normalized);
            w.size = new Vector3(8.5f, 8f, 1.5f);
        }
    }

    // ------------------------------------------------------------------ shared helpers
    static void Atmosphere(Color sky, Color equator, Color ground, Color fog, float fogDensity, Color sunColor, float sunIntensity, Vector3 sunEuler)
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = sky;
        RenderSettings.ambientEquatorColor = equator;
        RenderSettings.ambientGroundColor = ground;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogColor = fog;
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = fog; }
        var A = GameAssets.I;
        if (A != null && A.sky != null && cam != null)
        {
            // procedural sky with a real sun disc, tinted to blend into the realm's fog at the horizon
            var m = new Material(A.sky);
            m.SetColor("_SkyTint", Color.Lerp(fog, sky, 0.5f));
            m.SetColor("_GroundColor", fog);
            m.SetFloat("_AtmosphereThickness", 0.75f);
            m.SetFloat("_Exposure", 0.55f);
            m.SetFloat("_SunSize", 0.035f);
            RenderSettings.skybox = m;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        var sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(root, false);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = sunColor;
        sun.intensity = sunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        sun.shadowBias = 0.03f; sun.shadowNormalBias = 0.3f;
        sunGo.transform.rotation = Quaternion.Euler(sunEuler);
        RenderSettings.sun = sun;
    }

    static Transform Deco(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.transform.eulerAngles = euler;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g.transform;
    }

    static Transform Solid(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(type);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.transform.eulerAngles = euler;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g.transform;
    }

    static Light PointLight(Vector3 pos, Color c, float range, float intensity, bool flicker, float flickerAmount = 0.6f, bool shadows = false)
    {
        var lgo = new GameObject("Light");
        lgo.transform.SetParent(root, false);
        lgo.transform.position = pos;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
        l.shadows = shadows ? LightShadows.Hard : LightShadows.None;
        if (flicker) { var f = lgo.AddComponent<Flicker>(); f.baseIntensity = intensity; f.amount = flickerAmount; }
        return l;
    }

    static Vector3 Polar(float angle, float radius, float y = 0f) => new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

    static void Ground(Material mat, bool visible = true)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root, false);
        ground.transform.localScale = new Vector3(30, 1, 30);
        ground.GetComponent<Renderer>().sharedMaterial = mat;
        ground.GetComponent<Renderer>().enabled = visible;
    }

    // Snow / embers / rain as a simple particle system parented to the world.
    static void Weather(Color color, float rate, float size, Vector3 velocity, bool stretch, float lifetime = 6f)
    {
        var go = new GameObject("Weather");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0, 16f, 0);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.maxParticles = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70, 1, 70);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(velocity.x - 0.6f, velocity.x + 0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(velocity.y * 1.2f, velocity.y * 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(velocity.z - 0.6f, velocity.z + 0.6f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = GameAssets.I.trail;
        if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1f; }
        ps.Play();
    }

    // ------------------------------------------------------------------ Realm 1: Cinder Colosseum
    static void Colosseum()
    {
        var A = GameAssets.I;
        Atmosphere(new Color(0.32f, 0.22f, 0.3f), new Color(0.3f, 0.16f, 0.1f), new Color(0.06f, 0.04f, 0.04f),
            new Color(0.2f, 0.1f, 0.08f), 0.016f, new Color(1f, 0.62f, 0.42f), 1.15f, new Vector3(38f, -35f, 0f));

        Ground(A.ashGround);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(46, 0.005f, 46), A.arenaFloor);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.012f, 0), new Vector3(14, 0.005f, 14), A.stone);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.018f, 0), new Vector3(3, 0.005f, 3), A.rune);

        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            var t = Deco(PrimitiveType.Cube, root, Polar(a, 21f, 0.02f), new Vector3(0.25f, 0.02f, 1.2f), A.rune);
            t.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
        }
        // radial cracks of fire
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f + 0.2f;
            var t = Deco(PrimitiveType.Cube, root, Polar(a, 11f, 0.022f), new Vector3(0.18f, 0.02f, 6f), A.rune);
            t.rotation = Quaternion.LookRotation(Polar(a, 1f));
        }

        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.PI * 2f + 0.1f;
            var pos = Polar(a, 25f);
            bool broken = i % 4 == 1;
            float hgt = broken ? R(2.5f, 4f) : 9f;
            var p = Solid(PrimitiveType.Cylinder, root, pos + Vector3.up * hgt * 0.5f, new Vector3(1.8f, hgt * 0.5f, 1.8f), A.stone);
            if (broken) p.rotation = Quaternion.Euler(R(-6f, 6f), R(0, 360f), R(-6f, 6f));
            Solid(PrimitiveType.Cube, root, pos + Vector3.up * 0.4f, new Vector3(2.6f, 0.8f, 2.6f), A.darkStone);
            if (!broken) Solid(PrimitiveType.Cube, root, pos + Vector3.up * (hgt + 0.4f), new Vector3(2.6f, 0.8f, 2.6f), A.darkStone);
            else Deco(PrimitiveType.Cube, root, pos + new Vector3(1.8f, 0.4f, 0.6f), new Vector3(1.2f, 0.8f, 0.9f), A.stone, new Vector3(R(0, 90), R(0, 90), R(0, 90)));
            // hanging chains of rune light on intact pillars
            if (!broken) Deco(PrimitiveType.Cube, root, pos + Vector3.up * 6f - pos.normalized * 0.92f, new Vector3(0.3f, 1.4f, 0.05f), A.rune, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
        }

        for (int i = 0; i < 20; i++)
        {
            float a = (i + 0.5f) / 20 * Mathf.PI * 2f;
            var pos = Polar(a, 31f);
            float hgt = 6f + Mathf.PerlinNoise(i * 0.7f, 0.3f) * 6f;
            var w = Solid(PrimitiveType.Cube, root, pos + Vector3.up * hgt * 0.5f, new Vector3(10.5f, hgt, 2.5f), A.darkStone);
            w.rotation = Quaternion.LookRotation(-pos.normalized);
            // spectator stands
            var s = Deco(PrimitiveType.Cube, root, Polar(a, 34f, hgt + 1.5f), new Vector3(10.5f, 3f, 4f), A.stone);
            s.rotation = w.rotation;
        }

        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f + Mathf.PI / 8f;
            var pos = Polar(a, 19f);
            Solid(PrimitiveType.Cylinder, root, pos + Vector3.up * 0.7f, new Vector3(0.5f, 0.7f, 0.5f), A.iron);
            Deco(PrimitiveType.Cylinder, root, pos + Vector3.up * 1.5f, new Vector3(1.3f, 0.15f, 1.3f), A.iron);
            Deco(PrimitiveType.Cube, root, pos + Vector3.up * 1.85f, new Vector3(0.6f, 0.6f, 0.6f), A.fire, new Vector3(45, 45, 0)).gameObject.AddComponent<Floater>().amp = 0.05f;
            Deco(PrimitiveType.Cube, root, pos + Vector3.up * 2.15f, new Vector3(0.35f, 0.35f, 0.35f), A.fire, new Vector3(20, 10, 45)).gameObject.AddComponent<Floater>().spin = -40f;
            PointLight(pos + Vector3.up * 2.6f, new Color(1f, 0.5f, 0.15f), 13f, 2.6f, true, 0.6f, i % 2 == 0);
        }

        // Monument: a colossal broken sword planted at the north edge
        var mon = new GameObject("Monument").transform; mon.SetParent(root);
        mon.position = new Vector3(0, 0, 28); mon.rotation = Quaternion.Euler(8, 0, -6);
        foreach (var (pos, sc, m, sph) in new[]
        {
            (new Vector3(0, 7, 0), new Vector3(2.2f, 14, 0.5f), A.iron, false),
            (new Vector3(0, 14.2f, 0), new Vector3(7, 1.0f, 1.0f), A.darkStone, false),
            (new Vector3(0, 16.5f, 0), new Vector3(0.9f, 3.6f, 0.9f), A.darkStone, false),
            (new Vector3(0, 18.6f, 0), new Vector3(1.6f, 1.6f, 1.6f), A.rune, true),
        })
        {
            var g = GameObject.CreatePrimitive(sph ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(mon, false); g.transform.localPosition = pos; g.transform.localScale = sc;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * Mathf.PI * 2f;
            float r = R(55f, 85f), hgt = R(18f, 53f);
            Deco(PrimitiveType.Cube, root, Polar(a, r, hgt * 0.4f), new Vector3(R(14, 24), hgt, 12f), A.darkStone, new Vector3(R(-6, 6), R(0, 360), R(-5, 5)));
        }
        // distant volcano glow
        for (int i = 0; i < 3; i++)
            Deco(PrimitiveType.Sphere, root, Polar(1.3f + i * 2.1f, 90f, R(2, 8)), new Vector3(30, 8, 30), A.fire);

        Weather(new Color(1f, 0.5f, 0.15f, 0.9f), 60f, 0.12f, new Vector3(0.6f, 0.9f, 0.3f), false, 8f); // rising embers
        var w0 = root.Find("Weather"); w0.position = new Vector3(0, -1f, 0);
    }

    // ------------------------------------------------------------------ Realm 2: Frostveil Hollow
    static void Frostveil()
    {
        var A = GameAssets.I;
        Atmosphere(new Color(0.34f, 0.4f, 0.55f), new Color(0.22f, 0.27f, 0.36f), new Color(0.12f, 0.13f, 0.17f),
            new Color(0.42f, 0.5f, 0.62f), 0.022f, new Color(0.7f, 0.8f, 1f), 0.75f, new Vector3(32f, 140f, 0f));

        Ground(A.snow);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.01f, 0), new Vector3(20, 0.005f, 20), A.ice);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.016f, 0), new Vector3(2.5f, 0.005f, 2.5f), A.crystal);
        // cracks in the frozen lake
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f + R(-0.2f, 0.2f);
            var t = Deco(PrimitiveType.Cube, root, Polar(a, R(3f, 6f), 0.02f), new Vector3(0.06f, 0.01f, R(2.5f, 5f)), A.crystal);
            t.rotation = Quaternion.LookRotation(Polar(a + R(-0.4f, 0.4f), 1f));
        }
        // snow drifts around the lake
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.PI * 2f;
            Deco(PrimitiveType.Sphere, root, Polar(a, R(11f, 13f), -0.2f), new Vector3(R(3, 6), R(0.6f, 1.1f), R(2, 4)), A.snow, new Vector3(0, R(0, 360), 0));
        }

        // Standing stones with glowing crystal caps
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f + 0.15f;
            var pos = Polar(a, 24f);
            bool toppled = i % 5 == 2;
            if (toppled)
            {
                Solid(PrimitiveType.Cube, root, pos + Vector3.up * 0.7f, new Vector3(1.6f, 1.4f, 5f), A.frostStone, new Vector3(R(-5, 5), a * Mathf.Rad2Deg + R(-30, 30), R(-8, 8)));
                continue;
            }
            float h = R(5f, 8f);
            Solid(PrimitiveType.Cube, root, pos + Vector3.up * h * 0.5f, new Vector3(1.7f, h, 1.2f), A.frostStone, new Vector3(R(-4, 4), -a * Mathf.Rad2Deg + 90f, R(-4, 4)));
            Deco(PrimitiveType.Cube, root, pos + Vector3.up * (h + 0.1f), new Vector3(1.9f, 0.35f, 1.4f), A.snow, new Vector3(0, -a * Mathf.Rad2Deg + 90f, 0));
            Deco(PrimitiveType.Cube, root, pos + Vector3.up * (h + 0.8f), new Vector3(0.5f, 1.2f, 0.5f), A.crystal, new Vector3(0, 45, 12)).gameObject.AddComponent<Floater>().amp = 0.15f;
        }

        // Ice crystal clusters
        for (int c = 0; c < 9; c++)
        {
            float a = c / 9f * Mathf.PI * 2f + 0.4f;
            var center = Polar(a, R(18f, 21f));
            int n = rnd.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                float h = R(1.2f, 3.5f) * (i == 0 ? 1.4f : 1f);
                var off = new Vector3(R(-1.2f, 1.2f), 0, R(-1.2f, 1.2f));
                var t = (i == 0 ? Solid(PrimitiveType.Cube, root, center + off + Vector3.up * h * 0.35f, new Vector3(0.6f, h, 0.6f), A.crystal)
                                : Deco(PrimitiveType.Cube, root, center + off + Vector3.up * h * 0.35f, new Vector3(0.45f, h, 0.45f), A.crystal));
                t.rotation = Quaternion.Euler(R(-25, 25), R(0, 360), R(-25, 25));
            }
            if (c % 3 == 0) PointLight(center + Vector3.up * 2f, new Color(0.4f, 0.8f, 1f), 9f, 1.8f, true, 0.3f);
        }

        // Frozen cliffs as boundary
        for (int i = 0; i < 22; i++)
        {
            float a = (i + 0.5f) / 22 * Mathf.PI * 2f;
            var pos = Polar(a, 32f);
            float hgt = 7f + Mathf.PerlinNoise(i * 0.6f, 2.3f) * 8f;
            var w = Solid(PrimitiveType.Cube, root, pos + Vector3.up * hgt * 0.5f, new Vector3(10f, hgt, 3.5f), A.frostStone);
            w.rotation = Quaternion.LookRotation(-pos.normalized) * Quaternion.Euler(R(-4, 4), 0, R(-3, 3));
            Deco(PrimitiveType.Cube, root, pos + Vector3.up * (hgt + 0.2f), new Vector3(10.4f, 0.6f, 3.9f), A.snow).rotation = w.rotation;
            // icicles
            for (int k = 0; k < 3; k++)
                Deco(PrimitiveType.Cube, root, pos - pos.normalized * 1.8f + w.right * R(-4, 4) + Vector3.up * (hgt - 0.8f), new Vector3(0.18f, R(0.8f, 1.8f), 0.18f), A.ice, new Vector3(0, 45, 0));
        }

        // Pine forest beyond the cliffs
        for (int i = 0; i < 70; i++)
        {
            float a = R(0, Mathf.PI * 2f), r = R(36f, 75f);
            var pos = Polar(a, r);
            float s = R(0.8f, 1.6f);
            Deco(PrimitiveType.Cylinder, root, pos + Vector3.up * 1.5f * s, new Vector3(0.5f, 1.5f, 0.5f) * s, A.bark);
            for (int k = 0; k < 3; k++)
            {
                float y = (2.2f + k * 1.6f) * s, w = (3.4f - k * 0.9f) * s;
                Deco(PrimitiveType.Cube, root, pos + Vector3.up * y, new Vector3(w, 1.6f * s, w), A.pineLeaf, new Vector3(0, 45 + k * 20, 0));
                Deco(PrimitiveType.Cube, root, pos + Vector3.up * (y + 0.75f * s), new Vector3(w * 0.75f, 0.25f * s, w * 0.75f), A.snow, new Vector3(0, 45 + k * 20, 0));
            }
        }

        // Monument: the Weeping Spire, a colossal glowing ice monolith
        var spire = new Vector3(0, 0, 40);
        Deco(PrimitiveType.Cube, root, spire + Vector3.up * 13f, new Vector3(4f, 26f, 4f), A.crystal, new Vector3(4, 45, 3));
        Deco(PrimitiveType.Cube, root, spire + new Vector3(-4, 7, 1), new Vector3(2.4f, 14f, 2.4f), A.crystal, new Vector3(0, 30, 16));
        Deco(PrimitiveType.Cube, root, spire + new Vector3(4, 6, -1), new Vector3(2.2f, 12f, 2.2f), A.crystal, new Vector3(0, 60, -14));
        PointLight(spire + Vector3.up * 8f - Vector3.forward * 6f, new Color(0.4f, 0.75f, 1f), 30f, 3f, true, 0.4f);

        Weather(new Color(1f, 1f, 1f, 0.85f), 450f, 0.09f, new Vector3(1.2f, -2.2f, 0.4f), false, 9f);
    }

    // ------------------------------------------------------------------ Realm 3: Stormspire Summit
    static void Stormspire()
    {
        var A = GameAssets.I;
        Atmosphere(new Color(0.28f, 0.26f, 0.45f), new Color(0.18f, 0.16f, 0.3f), new Color(0.05f, 0.04f, 0.08f),
            new Color(0.13f, 0.11f, 0.2f), 0.013f, new Color(0.65f, 0.7f, 1f), 0.9f, new Vector3(50f, 60f, 0f));

        Ground(A.stormStone, false);
        // the summit platform
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, -6f, 0), new Vector3(58, 6f, 58), A.obsidian);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, -16f, 0), new Vector3(48, 6f, 48), A.obsidian);
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.005f, 0), new Vector3(52, 0.01f, 52), A.stormStone);
        foreach (var r in new[] { 7f, 14f, 22f })
        {
            int n = Mathf.RoundToInt(r * 3f);
            for (int i = 0; i < n; i++)
            {
                if (i % 4 == 3) continue;
                float a = i / (float)n * Mathf.PI * 2f;
                var t = Deco(PrimitiveType.Cube, root, Polar(a, r, 0.02f), new Vector3(0.2f, 0.02f, 2f * Mathf.PI * r / n * 0.85f), A.stormRune);
                t.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            var t = Deco(PrimitiveType.Cube, root, Polar(a, 14.5f, 0.021f), new Vector3(0.2f, 0.02f, 15f), A.stormRune);
            t.rotation = Quaternion.LookRotation(Polar(a, 1f));
        }
        Deco(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0), new Vector3(3.5f, 0.01f, 3.5f), A.stormRune);

        // broken crenellations at the edge
        for (int i = 0; i < 36; i++)
        {
            if (i % 6 == 0) continue;
            float a = i / 36f * Mathf.PI * 2f;
            float h = R(0.8f, 2.2f);
            var b = Solid(PrimitiveType.Cube, root, Polar(a, 28.3f, h * 0.5f), new Vector3(3.2f, h, 1.2f), A.obsidian);
            b.rotation = Quaternion.LookRotation(Polar(a, 1f)) * Quaternion.Euler(0, 90, R(-4, 4));
        }

        // lightning rods
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f + Mathf.PI / 6f;
            var pos = Polar(a, 23f);
            Solid(PrimitiveType.Cube, root, pos + Vector3.up * 0.6f, new Vector3(2.2f, 1.2f, 2.2f), A.obsidian, new Vector3(0, 45, 0));
            Solid(PrimitiveType.Cylinder, root, pos + Vector3.up * 5.5f, new Vector3(0.45f, 5f, 0.45f), A.iron);
            for (int k = 0; k < 3; k++)
                Deco(PrimitiveType.Cylinder, root, pos + Vector3.up * (3f + k * 2.5f), new Vector3(1.1f - k * 0.2f, 0.12f, 1.1f - k * 0.2f), A.stormRune);
            var tip = Deco(PrimitiveType.Cube, root, pos + Vector3.up * 11f, new Vector3(0.6f, 0.6f, 0.6f), A.stormRune, new Vector3(45, 0, 45));
            tip.gameObject.AddComponent<Floater>().spin = 90f;
            PointLight(pos + Vector3.up * 10.5f, new Color(0.6f, 0.5f, 1f), 14f, 2.4f, true, 0.9f, i % 2 == 0);
        }

        // floating rocks
        for (int i = 0; i < 26; i++)
        {
            float a = R(0, Mathf.PI * 2f), r = R(34f, 70f);
            var p = Polar(a, r, R(-6f, 22f));
            float s = R(2f, 7f);
            var rock = Deco(PrimitiveType.Cube, root, p, new Vector3(s, s * 0.7f, s * 0.9f), A.obsidian, new Vector3(R(0, 360), R(0, 360), R(0, 360)));
            var f = rock.gameObject.AddComponent<Floater>(); f.amp = R(0.4f, 1.6f); f.speed = R(0.2f, 0.6f); f.spin = R(-6f, 6f);
        }

        // distant peaks rising out of the clouds
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.PI * 2f + R(-0.1f, 0.1f);
            float r = R(80f, 120f), hgt = R(40f, 90f);
            Deco(PrimitiveType.Cube, root, Polar(a, r, -40f + hgt * 0.5f), new Vector3(R(18, 30), hgt, R(18, 30)), A.obsidian, new Vector3(R(-8, 8), R(0, 360), R(-8, 8)));
        }
        // cloud sea below
        for (int i = 0; i < 30; i++)
        {
            float a = R(0, Mathf.PI * 2f), r = R(30f, 110f);
            Deco(PrimitiveType.Sphere, root, Polar(a, r, R(-22f, -14f)), new Vector3(R(20, 40), R(4, 8), R(20, 40)), A.cloud);
        }

        // Monument: the Sky-Wound, a floating broken ring of storm-stone
        var ring = new GameObject("SkyWound").transform; ring.SetParent(root, false);
        ring.position = new Vector3(0, 18f, 42f);
        for (int i = 0; i < 20; i++)
        {
            if (i == 4 || i == 13) continue;
            float a = i / 20f * Mathf.PI * 2f;
            var t = Deco(PrimitiveType.Cube, ring, ring.position + new Vector3(Mathf.Cos(a) * 11f, Mathf.Sin(a) * 11f, 0), new Vector3(3.2f, 1.4f, 1.6f), i % 2 == 0 ? A.obsidian : A.stormRune);
            t.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f);
        }
        Deco(PrimitiveType.Sphere, ring, ring.position, Vector3.one * 4f, A.lightning);
        var rf = ring.gameObject.AddComponent<Floater>(); rf.amp = 1.2f; rf.speed = 0.3f; rf.spin = 4f;
        PointLight(ring.position + Vector3.back * 6f, new Color(0.6f, 0.7f, 1f), 40f, 3.5f, true, 1.2f);

        Weather(new Color(0.7f, 0.75f, 1f, 0.55f), 700f, 0.05f, new Vector3(-3f, -26f, 1f), true, 1.6f);
        root.Find("Weather").position = new Vector3(0, 30f, 0);
        root.gameObject.AddComponent<StormAmbience>();
    }
}
