using UnityEngine;

// Optional realistic hero: if a rigged humanoid model (e.g. from Mixamo) has been dropped into
// Assets/Models/<HeroName>/ the SceneBuilder wires it up and the player uses it instead of the
// primitive body. Weapons are re-parented to sockets on the model's real hand bones and the
// combat code drives animation states by name (Attack1, Heavy, Dodge, ...).
public class HeroModel : MonoBehaviour
{
    Animator anim;
    string current;
    static readonly int SpeedId = Animator.StringToHash("Speed");

    public static HeroModel TryAttach(PlayerController owner, int heroIndex, Rig rig, float heroScale)
    {
        var A = GameAssets.I;
        if (A == null || A.heroModels == null || heroIndex >= A.heroModels.Length) return null;
        var prefab = A.heroModels[heroIndex];
        var ctrl = A.heroControllers != null && heroIndex < A.heroControllers.Length ? A.heroControllers[heroIndex] : null;
        if (prefab == null || ctrl == null) return null;

        var go = Instantiate(prefab, owner.transform);
        go.name = "HeroModel";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        var animator = go.GetComponentInChildren<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        if (animator.avatar == null || !animator.avatar.isHuman) { Destroy(go); return null; }
        animator.runtimeAnimatorController = ctrl;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Fit the model to the hero's height.
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) { b.Encapsulate(r.bounds); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            float h = Mathf.Max(0.1f, b.size.y);
            go.transform.localScale *= 2.15f * heroScale / h;
        }

        // Hide the primitive body (weapons are added to the rig after this, so they stay visible).
        foreach (var r in rig.renderers) if (r != null) r.enabled = false;
        foreach (var r in rig.glowRenderers) if (r != null) r.enabled = false;
        rig.renderers.Clear(); rig.glowRenderers.Clear();
        rig.cape = null; rig.hairTail = null;

        var hm = go.AddComponent<HeroModel>();
        hm.anim = animator;
        var right = hm.Socket(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbProximal, "SocketR");
        var left = hm.Socket(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftThumbProximal, "SocketL");
        if (right != null) rig.handR = right;
        if (left != null) rig.handL = left;
        hm.Play("Locomotion");
        return hm;
    }

    // Weapon socket in the palm. Weapons point along +Z: perpendicular to the fingers, toward the thumb.
    Transform Socket(HumanBodyBones handBone, HumanBodyBones midBone, HumanBodyBones thumbBone, string name)
    {
        var hand = anim.GetBoneTransform(handBone);
        if (hand == null) return null;
        var mid = anim.GetBoneTransform(midBone);
        var thumb = anim.GetBoneTransform(thumbBone);
        Vector3 fingers = mid != null ? (mid.position - hand.position) : hand.up * 0.1f;
        if (fingers.sqrMagnitude < 1e-6f) fingers = hand.up;
        fingers.Normalize();
        Vector3 th = thumb != null ? thumb.position - hand.position : transform.forward;
        Vector3 blade = Vector3.ProjectOnPlane(th, fingers);
        if (blade.sqrMagnitude < 1e-6f) blade = Vector3.ProjectOnPlane(transform.forward, fingers);
        blade.Normalize();
        var s = new GameObject(name).transform;
        s.SetParent(hand, false);
        s.position = hand.position + fingers * Vector3.Distance(hand.position, mid != null ? mid.position : hand.position) * 0.55f;
        s.rotation = Quaternion.LookRotation(blade, Vector3.Cross(fingers, blade));
        // weapon meshes are authored in "hero units"; undo the model's scale so they keep their size
        s.localScale = Vector3.one;
        var ls = s.lossyScale;
        s.localScale = new Vector3(1f / Mathf.Max(1e-4f, ls.x), 1f / Mathf.Max(1e-4f, ls.y), 1f / Mathf.Max(1e-4f, ls.z)) * PlayerScale() * 0.68f;
        return s;
    }

    float PlayerScale() { var p = GetComponentInParent<PlayerController>(); return p != null ? p.Hero.scale : 1f; }

    public bool Has(string state) => anim != null && anim.HasState(0, Animator.StringToHash(state));

    public void Play(string state, float fade = 0.08f)
    {
        if (anim == null) return;
        if (!Has(state))
        {
            // graceful fallbacks when a clip was not downloaded
            string alt = state switch
            {
                "Attack2" => "Attack1", "Attack3" => "Heavy", "Spin" => "Heavy", "Execute" => "Heavy",
                "Ult" => "Heavy", "Throw" => "Attack1", "Heavy" => "Attack3", _ => null
            };
            if (alt == null || !Has(alt)) return;
            state = alt;
        }
        current = state;
        anim.CrossFadeInFixedTime(state, fade, 0, 0f);
    }

    public void SetLocomotion(float speed01, bool blocking)
    {
        if (anim == null) return;
        anim.SetFloat(SpeedId, speed01, 0.08f, Time.deltaTime);
        if (blocking && current != "Block" && Has("Block")) Play("Block", 0.1f);
        else if (!blocking && current == "Block") Play("Locomotion", 0.15f);
    }

    // after a one-shot clip has finished, return to running/idle
    public void BackToLocomotionIfIdle()
    {
        if (anim == null || current == "Locomotion" || current == "Death" || current == "Block") return;
        var st = anim.GetCurrentAnimatorStateInfo(0);
        if (!anim.IsInTransition(0) && st.normalizedTime >= 0.92f) BackToLocomotion();
    }

    public void BackToLocomotion()
    {
        if (anim == null || current == "Locomotion" || current == "Death") return;
        current = "Locomotion";
        anim.CrossFadeInFixedTime("Locomotion", 0.15f, 0, 0f);
    }
}
