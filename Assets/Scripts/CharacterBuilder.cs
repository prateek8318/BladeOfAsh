using System.Collections.Generic;
using UnityEngine;

// A humanoid made of primitives with pivots for procedural animation.
public class Rig
{
    public Transform visual, torso, head, armL, armR, handL, handR, legL, legR;
    public Transform cape, hairTail; // optional cloth / hair pivots for secondary motion
    public float scale = 1f;
    public List<Renderer> renderers = new List<Renderer>();
    public List<Renderer> glowRenderers = new List<Renderer>();
}

public static class CharacterBuilder
{
    public static Transform Pivot(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    public static Transform Part(Rig rig, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default, bool glow = false)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localEulerAngles = euler;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        if (rig != null)
        {
            if (glow) rig.glowRenderers.Add(r); else rig.renderers.Add(r);
        }
        return g.transform;
    }

    public struct Look
    {
        public Material skin, armor, cape, eye;
        public float scale;
        public bool horns, helmet, bulky, ponytail, hood;
        public Material glow;
        // Detailed hero body (used by the playable heroes)
        public bool hero, beard, bareChest, longHair, greatHelm;
        public Material hair, leather, trim;
    }

    public static Rig Build(Transform root, Look look)
    {
        if (look.hero) return BuildHero(root, look);
        var rig = new Rig { scale = look.scale };
        rig.visual = Pivot("Visual", root, Vector3.zero);
        rig.visual.localScale = Vector3.one * look.scale;
        float bulk = look.bulky ? 1.35f : 1f;

        // Hips + legs
        Part(rig, PrimitiveType.Cube, rig.visual, new Vector3(0, 0.95f, 0), new Vector3(0.52f * bulk, 0.26f, 0.32f), look.armor);
        rig.legL = Pivot("LegL", rig.visual, new Vector3(-0.17f * bulk, 0.9f, 0));
        rig.legR = Pivot("LegR", rig.visual, new Vector3(0.17f * bulk, 0.9f, 0));
        foreach (var leg in new[] { rig.legL, rig.legR })
        {
            Part(rig, PrimitiveType.Cube, leg, new Vector3(0, -0.42f, 0), new Vector3(0.24f * bulk, 0.86f, 0.26f), look.skin);
            Part(rig, PrimitiveType.Cube, leg, new Vector3(0, -0.5f, 0.02f), new Vector3(0.28f * bulk, 0.3f, 0.3f), look.armor);
            Part(rig, PrimitiveType.Cube, leg, new Vector3(0, -0.86f, 0.06f), new Vector3(0.3f * bulk, 0.14f, 0.4f), look.armor);
        }

        // Torso
        rig.torso = Pivot("Torso", rig.visual, new Vector3(0, 1.05f, 0));
        Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0, 0.38f, 0), new Vector3(0.72f * bulk, 0.74f, 0.4f * bulk), look.skin);
        Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0, 0.12f, 0.02f), new Vector3(0.76f * bulk, 0.22f, 0.44f * bulk), look.armor); // belt
        Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0.12f, 0.45f, 0.21f * bulk), new Vector3(0.12f, 0.6f, 0.04f), look.armor, new Vector3(0, 0, 35)); // chest strap
        if (look.cape != null)
            Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0, 0.1f, -0.24f * bulk), new Vector3(0.66f * bulk, 1.15f, 0.04f), look.cape, new Vector3(-8, 0, 0));

        // Head
        rig.head = Pivot("Head", rig.torso, new Vector3(0, 0.78f, 0));
        Part(rig, PrimitiveType.Sphere, rig.head, new Vector3(0, 0.2f, 0), new Vector3(0.42f, 0.46f, 0.42f), look.skin);
        Part(rig, PrimitiveType.Cube, rig.head, new Vector3(-0.09f, 0.24f, 0.19f), new Vector3(0.1f, 0.04f, 0.04f), look.eye, default, true);
        Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0.09f, 0.24f, 0.19f), new Vector3(0.1f, 0.04f, 0.04f), look.eye, default, true);
        if (look.helmet)
        {
            Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.38f, -0.02f), new Vector3(0.48f, 0.16f, 0.48f), look.armor);
            Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.3f, -0.2f), new Vector3(0.46f, 0.4f, 0.1f), look.armor);
            Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.46f, 0.0f), new Vector3(0.06f, 0.14f, 0.5f), look.armor); // crest
        }
        if (look.ponytail)
        {
            Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.36f, -0.12f), new Vector3(0.44f, 0.14f, 0.3f), look.armor);
            var tail = Pivot("Tail", rig.head, new Vector3(0, 0.34f, -0.22f));
            Part(rig, PrimitiveType.Cube, tail, new Vector3(0, -0.28f, -0.05f), new Vector3(0.12f, 0.6f, 0.1f), look.armor, new Vector3(-20, 0, 0));
            if (look.glow != null)
            {
                Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.33f, 0.2f), new Vector3(0.3f, 0.04f, 0.04f), look.glow, default, true); // circlet
                Part(rig, PrimitiveType.Cube, tail, new Vector3(0, -0.6f, -0.1f), new Vector3(0.14f, 0.08f, 0.12f), look.glow, default, true);
            }
        }
        if (look.hood)
        {
            Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.3f, -0.04f), new Vector3(0.52f, 0.5f, 0.52f), look.armor);
            Part(rig, PrimitiveType.Cube, rig.head, new Vector3(0, 0.5f, -0.16f), new Vector3(0.3f, 0.3f, 0.3f), look.armor, new Vector3(-30, 0, 0));
        }
        if (look.horns)
        {
            Part(rig, PrimitiveType.Cylinder, rig.head, new Vector3(-0.24f, 0.45f, 0), new Vector3(0.07f, 0.2f, 0.07f), look.armor, new Vector3(0, 0, 35));
            Part(rig, PrimitiveType.Cylinder, rig.head, new Vector3(0.24f, 0.45f, 0), new Vector3(0.07f, 0.2f, 0.07f), look.armor, new Vector3(0, 0, -35));
        }

        // Arms
        rig.armL = Pivot("ArmL", rig.torso, new Vector3(-0.47f * bulk, 0.68f, 0));
        rig.armR = Pivot("ArmR", rig.torso, new Vector3(0.47f * bulk, 0.68f, 0));
        foreach (var arm in new[] { rig.armL, rig.armR })
        {
            Part(rig, PrimitiveType.Sphere, arm, new Vector3(0, 0.02f, 0), new Vector3(0.36f * bulk, 0.28f, 0.36f * bulk), look.armor);
            Part(rig, PrimitiveType.Cube, arm, new Vector3(0, -0.4f, 0), new Vector3(0.2f * bulk, 0.8f, 0.2f * bulk), look.skin);
            Part(rig, PrimitiveType.Cube, arm, new Vector3(0, -0.62f, 0), new Vector3(0.25f * bulk, 0.26f, 0.25f * bulk), look.armor);
        }
        if (look.glow != null)
        {
            foreach (var arm in new[] { rig.armL, rig.armR })
                Part(rig, PrimitiveType.Cube, arm, new Vector3(0, 0.12f, 0), new Vector3(0.38f * bulk, 0.05f, 0.38f * bulk), look.glow, default, true);
        }
        rig.handL = Pivot("HandL", rig.armL, new Vector3(0, -0.82f, 0));
        rig.handR = Pivot("HandR", rig.armR, new Vector3(0, -0.82f, 0));
        return rig;
    }

    // ------------------------------------------------------------------------------------------
    // Detailed hero body. Same pivot layout as Build() so every animation and weapon still fits,
    // but with sculpted limbs (capsules), a real face, hair / beard, layered pauldrons, bracers,
    // boots, belt + tabard and a cape on its own pivot so it can sway.
    public static Rig BuildHero(Transform root, Look look)
    {
        var rig = new Rig { scale = look.scale };
        rig.visual = Pivot("Visual", root, Vector3.zero);
        rig.visual.localScale = Vector3.one * look.scale;
        float b = look.bulky ? 1.3f : 1f;
        var skin = look.skin; var armor = look.armor;
        var leather = look.leather != null ? look.leather : armor;
        var trim = look.trim != null ? look.trim : armor;
        var hair = look.hair != null ? look.hair : leather;
        var cloth = look.cape != null ? look.cape : leather;
        var cap = PrimitiveType.Capsule; var sph = PrimitiveType.Sphere; var cube = PrimitiveType.Cube; var cyl = PrimitiveType.Cylinder;

        // ---- Pelvis, belt, tabard
        Part(rig, cube, rig.visual, new Vector3(0, 0.97f, 0), new Vector3(0.5f * b, 0.24f, 0.3f * b), leather);
        Part(rig, cyl, rig.visual, new Vector3(0, 1.08f, 0), new Vector3(0.6f * b, 0.07f, 0.4f * b), leather);
        Part(rig, cyl, rig.visual, new Vector3(0, 1.11f, 0), new Vector3(0.62f * b, 0.015f, 0.42f * b), trim);
        Part(rig, cube, rig.visual, new Vector3(0, 1.08f, 0.2f * b), new Vector3(0.16f, 0.13f, 0.05f), trim);
        if (look.glow != null) Part(rig, cube, rig.visual, new Vector3(0, 1.08f, 0.23f * b), new Vector3(0.07f, 0.07f, 0.02f), look.glow, new Vector3(0, 0, 45), true);
        Part(rig, cube, rig.visual, new Vector3(0, 0.76f, 0.17f * b), new Vector3(0.28f * b, 0.5f, 0.03f), cloth, new Vector3(-5, 0, 0));
        Part(rig, cube, rig.visual, new Vector3(0, 0.53f, 0.19f * b), new Vector3(0.29f * b, 0.035f, 0.035f), trim);
        Part(rig, cube, rig.visual, new Vector3(0, 0.78f, -0.16f * b), new Vector3(0.36f * b, 0.46f, 0.03f), cloth, new Vector3(6, 0, 0));
        for (int s = -1; s <= 1; s += 2)
        {
            Part(rig, cube, rig.visual, new Vector3(s * 0.28f * b, 0.93f, 0.02f), new Vector3(0.07f, 0.3f, 0.3f * b), armor, new Vector3(0, 0, s * 14f));
            Part(rig, cube, rig.visual, new Vector3(s * 0.3f * b, 0.79f, 0.02f), new Vector3(0.015f, 0.04f, 0.31f * b), trim, new Vector3(0, 0, s * 14f));
        }

        // ---- Legs (pivot at the hip)
        rig.legL = Pivot("LegL", rig.visual, new Vector3(-0.15f * b, 0.9f, 0));
        rig.legR = Pivot("LegR", rig.visual, new Vector3(0.15f * b, 0.9f, 0));
        foreach (var leg in new[] { rig.legL, rig.legR })
        {
            Part(rig, cap, leg, new Vector3(0, -0.22f, 0), new Vector3(0.27f * b, 0.25f, 0.29f * b), leather);        // thigh
            Part(rig, sph, leg, new Vector3(0, -0.46f, 0.08f), new Vector3(0.2f * b, 0.17f, 0.14f), armor);           // knee cop
            Part(rig, cap, leg, new Vector3(0, -0.64f, 0), new Vector3(0.21f * b, 0.2f, 0.22f * b), leather);         // calf
            Part(rig, cube, leg, new Vector3(0, -0.63f, 0.07f), new Vector3(0.19f * b, 0.28f, 0.1f), armor);          // greave
            Part(rig, cube, leg, new Vector3(0, -0.6f, 0.125f), new Vector3(0.03f, 0.24f, 0.02f), trim);              // greave ridge
            Part(rig, cube, leg, new Vector3(0, -0.76f, 0.01f), new Vector3(0.25f * b, 0.06f, 0.27f * b), trim);      // boot cuff
            Part(rig, cube, leg, new Vector3(0, -0.84f, 0.05f), new Vector3(0.24f * b, 0.13f, 0.34f), leather);       // boot
            Part(rig, cube, leg, new Vector3(0, -0.86f, 0.2f), new Vector3(0.22f * b, 0.09f, 0.1f), armor);           // toe cap
        }

        // ---- Torso (pivot at the waist)
        rig.torso = Pivot("Torso", rig.visual, new Vector3(0, 1.05f, 0));
        var T = rig.torso;
        Part(rig, cap, T, new Vector3(0, 0.2f, 0), new Vector3(0.5f * b, 0.22f, 0.3f * b), look.bareChest ? skin : leather); // waist
        if (look.bareChest)
        {
            Part(rig, cube, T, new Vector3(0, 0.48f, 0), new Vector3(0.68f * b, 0.44f, 0.36f * b), skin);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(rig, sph, T, new Vector3(s * 0.15f * b, 0.55f, 0.13f * b), new Vector3(0.3f * b, 0.24f, 0.16f), skin);  // pecs
                for (int r = 0; r < 3; r++)
                    Part(rig, sph, T, new Vector3(s * 0.07f * b, 0.36f - r * 0.1f, 0.13f * b), new Vector3(0.12f * b, 0.08f, 0.07f), skin); // abs
                Part(rig, cube, T, new Vector3(s * 0.3f * b, 0.42f, 0), new Vector3(0.12f, 0.5f, 0.32f * b), skin, new Vector3(0, 0, s * 8f)); // lats
            }
            // diagonal leather harness with a glowing sigil
            Part(rig, cube, T, new Vector3(0, 0.46f, 0.21f * b), new Vector3(0.1f, 0.9f, 0.03f), leather, new Vector3(0, 0, 38));
            Part(rig, cube, T, new Vector3(0, 0.46f, -0.2f * b), new Vector3(0.1f, 0.9f, 0.03f), leather, new Vector3(0, 0, -38));
        }
        else
        {
            Part(rig, cube, T, new Vector3(0, 0.48f, 0), new Vector3(0.66f * b, 0.46f, 0.36f * b), leather);
            // sculpted cuirass: two chest plates, a sternum ridge and stacked belly lames
            for (int s = -1; s <= 1; s += 2)
                Part(rig, cube, T, new Vector3(s * 0.15f * b, 0.53f, 0.14f * b), new Vector3(0.32f * b, 0.3f, 0.14f), armor, new Vector3(0, s * 14f, 0));
            Part(rig, cube, T, new Vector3(0, 0.53f, 0.2f * b), new Vector3(0.05f, 0.3f, 0.06f), trim);
            for (int r = 0; r < 3; r++)
                Part(rig, cube, T, new Vector3(0, 0.33f - r * 0.085f, 0.13f * b), new Vector3((0.5f - r * 0.04f) * b, 0.08f, 0.12f), armor, new Vector3(-6, 0, 0));
            Part(rig, cube, T, new Vector3(0, 0.48f, -0.12f * b), new Vector3(0.6f * b, 0.44f, 0.14f), armor);
        }
        if (look.glow != null)
            Part(rig, cube, T, new Vector3(0, 0.56f, 0.24f * b), new Vector3(0.09f, 0.09f, 0.02f), look.glow, new Vector3(0, 0, 45), true);
        Part(rig, cyl, T, new Vector3(0, 0.74f, 0), new Vector3(0.36f * b, 0.05f, 0.3f * b), trim);              // gorget ring
        Part(rig, cyl, T, new Vector3(0, 0.8f, 0.01f), new Vector3(0.17f, 0.07f, 0.17f), skin);                  // neck
        Part(rig, cube, T, new Vector3(0, 0.72f, 0.02f), new Vector3(0.64f * b, 0.08f, 0.3f * b), armor);        // collar plate

        // ---- Cape on its own pivot (PlayerController sways it)
        if (look.cape != null)
        {
            rig.cape = Pivot("Cape", T, new Vector3(0, 0.72f, -0.21f * b));
            Part(rig, cube, rig.cape, new Vector3(0, 0, 0), new Vector3(0.72f * b, 0.1f, 0.1f), trim);
            Part(rig, cube, rig.cape, new Vector3(0, -0.62f, -0.03f), new Vector3(0.66f * b, 1.24f, 0.035f), look.cape);
            float[] tatters = { 0.16f, 0.26f, 0.12f, 0.22f };
            for (int i = 0; i < tatters.Length; i++)
                Part(rig, cube, rig.cape, new Vector3((-0.24f + i * 0.16f) * b, -1.24f - tatters[i] * 0.5f, -0.03f), new Vector3(0.14f * b, tatters[i], 0.03f), look.cape, new Vector3(0, 0, (i - 1.5f) * 4f));
            for (int s = -1; s <= 1; s += 2)
                Part(rig, sph, rig.cape, new Vector3(s * 0.27f * b, 0.02f, 0.07f), new Vector3(0.1f, 0.1f, 0.06f), look.glow != null ? look.glow : trim, default, look.glow != null);
        }

        // ---- Head
        rig.head = Pivot("Head", T, new Vector3(0, 0.78f, 0));
        var H = rig.head;
        Part(rig, sph, H, new Vector3(0, 0.24f, -0.01f), new Vector3(0.36f, 0.42f, 0.4f), skin);               // skull
        Part(rig, sph, H, new Vector3(0, 0.11f, 0.05f), new Vector3(0.29f, 0.2f, 0.3f), skin);                 // jaw
        Part(rig, cube, H, new Vector3(0, 0.29f, 0.17f), new Vector3(0.3f, 0.05f, 0.08f), skin, new Vector3(8, 0, 0)); // brow ridge
        Part(rig, cube, H, new Vector3(0, 0.215f, 0.2f), new Vector3(0.06f, 0.11f, 0.07f), skin, new Vector3(-12, 0, 0)); // nose
        Part(rig, sph, H, new Vector3(-0.09f, 0.17f, 0.15f), new Vector3(0.1f, 0.07f, 0.06f), skin);           // cheek
        Part(rig, sph, H, new Vector3(0.09f, 0.17f, 0.15f), new Vector3(0.1f, 0.07f, 0.06f), skin);
        for (int s = -1; s <= 1; s += 2)
        {
            Part(rig, sph, H, new Vector3(s * 0.18f, 0.22f, 0), new Vector3(0.05f, 0.1f, 0.08f), skin);       // ear
            Part(rig, cube, H, new Vector3(s * 0.075f, 0.245f, 0.188f), new Vector3(0.075f, 0.028f, 0.02f), look.eye, new Vector3(0, 0, s * -6f), true); // eyes
        }
        Part(rig, cube, H, new Vector3(0, 0.11f, 0.19f), new Vector3(0.09f, 0.018f, 0.02f), leather);         // mouth line

        if (look.beard)
        {
            Part(rig, sph, H, new Vector3(0, 0.07f, 0.1f), new Vector3(0.31f, 0.22f, 0.26f), hair);
            Part(rig, cube, H, new Vector3(0, 0.15f, 0.195f), new Vector3(0.17f, 0.035f, 0.04f), hair);        // moustache
            Part(rig, cube, H, new Vector3(0, -0.04f, 0.15f), new Vector3(0.12f, 0.16f, 0.1f), hair, new Vector3(-15, 0, 0));
            if (look.bulky) // braided forge-beard with metal rings
            {
                Part(rig, cube, H, new Vector3(0, -0.16f, 0.17f), new Vector3(0.1f, 0.16f, 0.09f), hair, new Vector3(-10, 0, 0));
                Part(rig, cyl, H, new Vector3(0, -0.1f, 0.165f), new Vector3(0.12f, 0.02f, 0.11f), trim, new Vector3(-10, 0, 0));
                Part(rig, cyl, H, new Vector3(0, -0.22f, 0.18f), new Vector3(0.11f, 0.02f, 0.1f), trim, new Vector3(-10, 0, 0));
            }
        }

        if (look.greatHelm)
        {
            // closed forge helm with a glowing visor slit and swept horns
            Part(rig, sph, H, new Vector3(0, 0.27f, 0), new Vector3(0.44f, 0.46f, 0.46f), armor);
            Part(rig, cube, H, new Vector3(0, 0.24f, 0.2f), new Vector3(0.3f, 0.035f, 0.06f), look.glow != null ? look.glow : look.eye, default, true);
            Part(rig, cube, H, new Vector3(0, 0.15f, 0.2f), new Vector3(0.08f, 0.14f, 0.06f), trim);
            Part(rig, cube, H, new Vector3(0, 0.46f, 0), new Vector3(0.06f, 0.1f, 0.42f), trim);
        }
        else if (look.longHair)
        {
            Part(rig, sph, H, new Vector3(0, 0.3f, -0.03f), new Vector3(0.39f, 0.36f, 0.43f), hair);
            Part(rig, cube, H, new Vector3(0, 0.36f, 0.14f), new Vector3(0.34f, 0.08f, 0.14f), hair, new Vector3(25, 0, 0)); // swept fringe
            for (int s = -1; s <= 1; s += 2)
                Part(rig, cube, H, new Vector3(s * 0.17f, 0.12f, 0.05f), new Vector3(0.06f, 0.32f, 0.1f), hair, new Vector3(0, 0, s * -4f)); // side locks
            rig.hairTail = Pivot("Tail", H, new Vector3(0, 0.36f, -0.2f));
            for (int i = 0; i < 4; i++)
                Part(rig, sph, rig.hairTail, new Vector3(0, -0.06f - i * 0.17f, -0.06f - i * 0.02f), new Vector3(0.13f - i * 0.015f, 0.2f, 0.12f - i * 0.012f), hair);
            if (look.glow != null)
            {
                Part(rig, cube, H, new Vector3(0, 0.36f, 0.19f), new Vector3(0.34f, 0.03f, 0.03f), look.glow, default, true); // circlet
                Part(rig, cube, H, new Vector3(0, 0.37f, 0.21f), new Vector3(0.06f, 0.06f, 0.02f), look.glow, new Vector3(0, 0, 45), true);
                Part(rig, cyl, rig.hairTail, new Vector3(0, -0.66f, -0.12f), new Vector3(0.12f, 0.03f, 0.12f), look.glow, default, true);
            }
        }
        else
        {
            // short swept-back hair with a top-knot
            Part(rig, sph, H, new Vector3(0, 0.31f, -0.04f), new Vector3(0.38f, 0.33f, 0.41f), hair);
            Part(rig, sph, H, new Vector3(0, 0.47f, -0.13f), new Vector3(0.13f, 0.13f, 0.13f), hair);
            Part(rig, cyl, H, new Vector3(0, 0.45f, -0.12f), new Vector3(0.1f, 0.02f, 0.1f), trim);
        }

        if (look.helmet && !look.greatHelm)
        {
            // open war-crown: a band around the brow with a front spike
            Part(rig, cyl, H, new Vector3(0, 0.35f, 0), new Vector3(0.41f, 0.035f, 0.45f), armor);
            Part(rig, cube, H, new Vector3(0, 0.42f, 0.2f), new Vector3(0.05f, 0.14f, 0.04f), trim, new Vector3(-12, 0, 0));
        }
        if (look.horns)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float y = look.greatHelm ? 0.36f : 0.37f;
                Part(rig, cyl, H, new Vector3(s * 0.22f, y, 0), new Vector3(0.07f, 0.1f, 0.07f), trim, new Vector3(0, 0, s * -70f));
                Part(rig, cyl, H, new Vector3(s * 0.33f, y + 0.08f, -0.02f), new Vector3(0.055f, 0.1f, 0.055f), trim, new Vector3(-15, 0, s * -30f));
                Part(rig, cyl, H, new Vector3(s * 0.37f, y + 0.22f, -0.05f), new Vector3(0.035f, 0.08f, 0.035f), trim, new Vector3(-25, 0, s * -5f));
            }
        }

        // ---- Arms (pivot at the shoulder)
        rig.armL = Pivot("ArmL", T, new Vector3(-0.45f * b, 0.66f, 0));
        rig.armR = Pivot("ArmR", T, new Vector3(0.45f * b, 0.66f, 0));
        foreach (var arm in new[] { rig.armL, rig.armR })
        {
            float s = arm == rig.armR ? 1f : -1f;
            // layered pauldron
            Part(rig, sph, arm, new Vector3(s * 0.02f, 0.04f, 0), new Vector3(0.36f * b, 0.28f, 0.38f * b), armor);
            Part(rig, cube, arm, new Vector3(s * 0.07f, -0.02f, 0), new Vector3(0.32f * b, 0.05f, 0.38f * b), armor, new Vector3(0, 0, s * -28f));
            Part(rig, cube, arm, new Vector3(s * 0.11f, -0.1f, 0), new Vector3(0.28f * b, 0.05f, 0.35f * b), armor, new Vector3(0, 0, s * -42f));
            Part(rig, cube, arm, new Vector3(s * 0.13f, -0.14f, 0), new Vector3(0.24f * b, 0.02f, 0.36f * b), trim, new Vector3(0, 0, s * -48f));
            // upper arm + bicep
            Part(rig, cap, arm, new Vector3(0, -0.26f, 0), new Vector3(0.19f * b, 0.2f, 0.19f * b), skin);
            Part(rig, sph, arm, new Vector3(0, -0.24f, 0.04f), new Vector3(0.2f * b, 0.24f, 0.19f * b), skin);
            Part(rig, cube, arm, new Vector3(0, -0.15f, 0), new Vector3(0.21f * b, 0.04f, 0.21f * b), leather);  // arm band
            // elbow + forearm bracer with a fin
            Part(rig, sph, arm, new Vector3(0, -0.45f, -0.01f), new Vector3(0.16f * b, 0.14f, 0.16f * b), leather);
            Part(rig, cap, arm, new Vector3(0, -0.6f, 0), new Vector3(0.17f * b, 0.16f, 0.17f * b), skin);
            Part(rig, cube, arm, new Vector3(0, -0.62f, 0), new Vector3(0.2f * b, 0.26f, 0.2f * b), armor);
            Part(rig, cube, arm, new Vector3(s * 0.11f * b, -0.6f, 0), new Vector3(0.04f, 0.24f, 0.1f), trim, new Vector3(0, 0, s * 6f));
            Part(rig, cube, arm, new Vector3(0, -0.52f, 0), new Vector3(0.21f * b, 0.03f, 0.21f * b), look.glow != null ? look.glow : trim, default, look.glow != null);
            // gauntlet fist
            Part(rig, sph, arm, new Vector3(0, -0.8f, 0.02f), new Vector3(0.15f * b, 0.16f, 0.17f * b), leather);
            Part(rig, cube, arm, new Vector3(0, -0.79f, 0.08f), new Vector3(0.13f * b, 0.05f, 0.05f), armor);
        }
        rig.handL = Pivot("HandL", rig.armL, new Vector3(0, -0.82f, 0));
        rig.handR = Pivot("HandR", rig.armR, new Vector3(0, -0.82f, 0));
        return rig;
    }

    // Greatsword in the right hand. The blade points along the hand's +Z.
    public static Transform GreatSword(Rig rig, Material grip, Material blade, float length = 1.5f)
    {
        var w = Pivot("Sword", rig.handR, Vector3.zero);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, -0.05f), new Vector3(0.08f, 0.08f, 0.3f), grip);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 0.12f), new Vector3(0.5f, 0.08f, 0.08f), grip);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 0.16f + length * 0.5f), new Vector3(0.16f, 0.04f, length), blade, default, true);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 0.16f + length), new Vector3(0.11f, 0.04f, 0.11f), blade, new Vector3(0, 45, 0), true);
        return w;
    }

    public static Transform Club(Rig rig, Material mat, float length = 1.0f, float headSize = 0.28f)
    {
        var w = Pivot("Club", rig.handR, Vector3.zero);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, length * 0.45f), new Vector3(0.09f, 0.09f, length), mat);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, length), new Vector3(headSize, headSize, headSize * 1.4f), mat, new Vector3(0, 0, 45));
        return w;
    }

    // Short curved blade for one hand. Returns the blade tip pivot (for trails).
    public static Transform Fang(Rig rig, Transform hand, Material grip, Material blade, string name, out Transform tip)
    {
        var w = Pivot(name, hand, Vector3.zero);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, -0.02f), new Vector3(0.07f, 0.07f, 0.22f), grip);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 0.1f), new Vector3(0.26f, 0.06f, 0.06f), grip);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 0.42f), new Vector3(0.11f, 0.035f, 0.62f), blade, new Vector3(0, 6, 0), true);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0.04f, 0, 0.76f), new Vector3(0.08f, 0.035f, 0.18f), blade, new Vector3(0, 30, 0), true);
        Part(rig, PrimitiveType.Cube, w, new Vector3(-0.07f, 0, 0.18f), new Vector3(0.05f, 0.04f, 0.16f), blade, new Vector3(0, -35, 0), true); // back hook
        tip = Pivot("Tip", w, new Vector3(0.05f, 0, 0.85f));
        return w;
    }

    // Two-handed thunder maul. Head sits at the far end along +Z.
    public static Transform Maul(Rig rig, Material haft, Material head, Material glow, out Transform tip)
    {
        var w = Pivot("Maul", rig.handR, Vector3.zero);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 0.55f), new Vector3(0.1f, 0.1f, 1.7f), haft);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, -0.3f), new Vector3(0.16f, 0.16f, 0.14f), head);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 1.45f), new Vector3(0.75f, 0.42f, 0.42f), head);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 1.45f), new Vector3(0.8f, 0.2f, 0.2f), glow, default, true);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0.45f, 0, 1.45f), new Vector3(0.12f, 0.5f, 0.5f), head);
        Part(rig, PrimitiveType.Cube, w, new Vector3(-0.45f, 0, 1.45f), new Vector3(0.12f, 0.5f, 0.5f), head);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, 0, 1.75f), new Vector3(0.18f, 0.18f, 0.25f), glow, new Vector3(0, 0, 45), true);
        tip = Pivot("Tip", w, new Vector3(0, 0, 1.75f));
        return w;
    }

    public static Transform Orb(Rig rig, Transform hand, Material glow, float size)
    {
        var w = Pivot("Orb", hand, Vector3.zero);
        Part(rig, PrimitiveType.Sphere, w, new Vector3(0, -0.1f, 0.15f), Vector3.one * size, glow, default, true);
        Part(rig, PrimitiveType.Cube, w, new Vector3(0, -0.1f, 0.15f), Vector3.one * size * 0.9f, glow, new Vector3(45, 45, 0), true);
        return w;
    }

    public static TrailRenderer Trail(Transform tip, Transform baseT, Color color, float width)
    {
        var tr = tip.gameObject.AddComponent<TrailRenderer>();
        tr.sharedMaterial = GameAssets.I.trail;
        tr.time = 0.16f;
        tr.minVertexDistance = 0.05f;
        tr.startWidth = width; tr.endWidth = 0f;
        tr.startColor = new Color(color.r, color.g, color.b, 0.85f);
        tr.endColor = new Color(color.r, color.g, color.b, 0f);
        tr.emitting = false;
        tr.alignment = LineAlignment.View;
        return tr;
    }
}
