using System.Collections.Generic;
using UnityEngine;

// A humanoid made of primitives with pivots for procedural animation.
public class Rig
{
    public Transform visual, torso, head, armL, armR, handL, handR, legL, legR;
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
    }

    public static Rig Build(Transform root, Look look)
    {
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
