using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The playable hero. Hero (Kael / Ysra / Gorrun) and weapon (Ashbrand / Emberfangs / Thunderjaw) are swappable.
// Controls: WASD move, Shift sprint, Space dodge-roll, LMB light combo, RMB heavy, F (hold) block (tap = parry),
// E throw / recall javelin, R ultimate, 1/2/3 or Q switch weapon.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController I;

    [Header("Movement")]
    public float runSpeed = 6.5f;
    public float sprintSpeed = 10f;
    public float gravity = -28f;
    public float dodgeSpeed = 15f;
    public float dodgeDuration = 0.42f;

    [Header("Ultimate")]
    public float maxRage = 100f;
    public float rage;
    public float rageDuration = 12f;

    public Health health { get; private set; }
    public HeroDef Hero { get; private set; }
    public WeaponKind Weapon { get; private set; }
    public int Combo { get; private set; }
    public bool UltActive => rageTimer > 0f;
    public bool RageActive => UltActive && Hero.ult == UltimateKind.Rage;
    public float RageTimer => rageTimer;
    public bool SpearInHand => spearInHand;
    public bool IsBlocking => blocking;
    public bool Chilled => chillTimer > 0f;
    public float OutgoingMul => Hero.damageMul * (RageActive ? 1.8f : (UltActive && Hero.ult == UltimateKind.Tempest ? 1.25f : 1f));

    CharacterController cc;
    ThirdPersonCamera cam;
    Rig rig;
    Transform spearVisual, swordGO, fangR, fangL, maulGO;
    readonly Dictionary<WeaponKind, List<TrailRenderer>> trails = new Dictionary<WeaponKind, List<TrailRenderer>>();
    Light rageLight;

    Vector3 velocity;
    Vector3 knock;
    float rageTimer;
    float comboTimer;
    bool attacking;
    bool spinning;
    bool queuedLight;
    int lightStep;
    float lightResetTimer;
    float dodgeTimer;
    float dodgeCooldown;
    Vector3 dodgeDir;
    bool blocking;
    float blockStart;
    bool spearInHand = true;
    Spear thrown;
    float walkCycle;
    bool dead;
    float moveAmount;
    float chillTimer;
    float ultTick;
    float lastPerfectDodge = -10f;

    // Procedural pose targets
    Quaternion armRT, handRT, armLT, handLT, torsoT;
    float poseSpeed = 14f;
    float attackMoveForward;

    void Awake()
    {
        I = this;
        cc = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        health.isPlayer = true;
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        Hero = HeroDef.All[0];
    }

    void Start()
    {
        cam = FindAnyObjectByType<ThirdPersonCamera>();
        var lg = new GameObject("UltLight");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0, 1.4f, 0);
        rageLight = lg.AddComponent<Light>();
        rageLight.type = LightType.Point;
        rageLight.range = 7f;
        rageLight.intensity = 0f;
        ApplyHero(GameManager.SelectedHero);
    }

    // Rebuilds the hero's body and weapons for the given hero index.
    public void ApplyHero(int index)
    {
        Hero = HeroDef.All[Mathf.Clamp(index, 0, HeroDef.All.Length - 1)];
        var A = GameAssets.I;
        if (rig != null && rig.visual != null) { rig.visual.name = "OldVisual"; Destroy(rig.visual.gameObject); }
        trails.Clear();

        Material skin, armor, cape, glow;
        switch (Hero.ult)
        {
            case UltimateKind.Tempest: skin = A.ysraSkin; armor = A.ysraArmor; cape = A.ysraCape; glow = A.ysraGlow; break;
            case UltimateKind.MoltenAegis: skin = A.gorrunSkin; armor = A.gorrunArmor; cape = A.gorrunCape; glow = A.gorrunGlow; break;
            default: skin = A.heroSkin; armor = A.heroArmor; cape = A.heroCape; glow = A.heroEmber; break;
        }
        Material hairMat = Hero.ult == UltimateKind.Tempest ? A.ysraHair : Hero.ult == UltimateKind.MoltenAegis ? A.gorrunHair : A.kaelHair;
        rig = CharacterBuilder.Build(transform, new CharacterBuilder.Look
        {
            hero = true, skin = skin, armor = armor, cape = cape, eye = glow, glow = glow,
            hair = hairMat, leather = A.leather, trim = A.gold,
            scale = Hero.scale, helmet = Hero.helmet, horns = Hero.horns, bulky = Hero.bulky,
            longHair = Hero.ponytail, beard = Hero.beard, bareChest = Hero.bareChest, greatHelm = Hero.greatHelm
        });
        if (Hero.ult == UltimateKind.MoltenAegis)
        {
            // molten heart burning through the chest
            CharacterBuilder.Part(rig, PrimitiveType.Sphere, rig.torso, new Vector3(0, 0.5f, 0.2f), new Vector3(0.2f, 0.2f, 0.06f), glow, default, true);
            CharacterBuilder.Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0, 0.5f, 0.21f), new Vector3(0.36f, 0.04f, 0.03f), glow, new Vector3(0, 0, 30), true);
            CharacterBuilder.Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0, 0.5f, 0.21f), new Vector3(0.36f, 0.04f, 0.03f), glow, new Vector3(0, 0, -30), true);
        }

        // Weapons (only the equipped one is visible)
        swordGO = CharacterBuilder.GreatSword(rig, A.heroArmor, A.blade, 1.55f);
        var sTip = CharacterBuilder.Pivot("Tip", swordGO, new Vector3(0, 0, 1.7f));
        var sBase = CharacterBuilder.Pivot("Base", swordGO, new Vector3(0, 0, 0.3f));
        AddTrail(WeaponKind.Ashbrand, CharacterBuilder.Trail(sTip, sBase, WeaponDef.All[0].trailColor, 0.5f));

        fangR = CharacterBuilder.Fang(rig, rig.handR, A.heroArmor, A.fang, "FangR", out var fTipR);
        fangL = CharacterBuilder.Fang(rig, rig.handL, A.heroArmor, A.fang, "FangL", out var fTipL);
        AddTrail(WeaponKind.Emberfangs, CharacterBuilder.Trail(fTipR, fangR, WeaponDef.All[1].trailColor, 0.3f));
        AddTrail(WeaponKind.Emberfangs, CharacterBuilder.Trail(fTipL, fangL, WeaponDef.All[1].trailColor, 0.3f));

        maulGO = CharacterBuilder.Maul(rig, A.iron, A.maulHead, A.lightning, out var mTip);
        AddTrail(WeaponKind.Thunderjaw, CharacterBuilder.Trail(mTip, maulGO, WeaponDef.All[2].trailColor, 0.7f));

        spearVisual = BuildSpearMesh(rig.handL, A.heroArmor, A.spear);
        spearVisual.gameObject.SetActive(spearInHand);

        health.SetMax(Hero.maxHealth);
        health.damageTakenMultiplier = Hero.damageTaken;
        runSpeed = Hero.runSpeed;
        sprintSpeed = Hero.sprintSpeed;
        rageLight.color = Hero.accent;
        cc.height = 2f * Hero.scale; cc.center = new Vector3(0, Hero.scale, 0);
        SetWeapon((int)Weapon, false);
        IdlePose();
    }

    void AddTrail(WeaponKind k, TrailRenderer t)
    {
        if (!trails.TryGetValue(k, out var list)) trails[k] = list = new List<TrailRenderer>();
        list.Add(t);
    }

    void SetWeapon(int i, bool flourish)
    {
        Weapon = (WeaponKind)Mathf.Clamp(i, 0, 2);
        swordGO.gameObject.SetActive(Weapon == WeaponKind.Ashbrand);
        fangR.gameObject.SetActive(Weapon == WeaponKind.Emberfangs);
        fangL.gameObject.SetActive(Weapon == WeaponKind.Emberfangs);
        maulGO.gameObject.SetActive(Weapon == WeaponKind.Thunderjaw);
        spearVisual.gameObject.SetActive(spearInHand && Weapon != WeaponKind.Emberfangs);
        lightStep = 0;
        if (flourish)
        {
            var mat = Weapon == WeaponKind.Thunderjaw ? GameAssets.I.lightning : Weapon == WeaponKind.Emberfangs ? GameAssets.I.fang : GameAssets.I.blade;
            FX.Shards(rig.handR.position, mat, 10, 3f, 0.07f);
            SFX.Play(SFX.S.Throw, 0.5f, 1.4f);
            GameManager.I?.ShowWeapon(WeaponDef.All[(int)Weapon].name);
        }
    }

    public static Transform BuildSpearMesh(Transform parent, Material shaft, Material tip)
    {
        var s = CharacterBuilder.Pivot("Spear", parent, Vector3.zero);
        CharacterBuilder.Part(null, PrimitiveType.Cylinder, s, new Vector3(0, 0, 0.3f), new Vector3(0.06f, 0.9f, 0.06f), shaft, new Vector3(90, 0, 0));
        CharacterBuilder.Part(null, PrimitiveType.Cube, s, new Vector3(0, 0, 1.3f), new Vector3(0.12f, 0.03f, 0.35f), tip, new Vector3(0, 0, 0));
        CharacterBuilder.Part(null, PrimitiveType.Cube, s, new Vector3(0, 0, 1.12f), new Vector3(0.24f, 0.04f, 0.06f), tip);
        return s;
    }

    // ---------------------------------------------------------------- Update
    void Update()
    {
        if (dead || rig == null) return;
        if (GameManager.I != null && !GameManager.I.InputEnabled) { ApplyGravityOnly(); AnimatePose(); return; }

        float dt = Time.deltaTime;
        dodgeCooldown -= dt;
        comboTimer -= dt;
        chillTimer -= dt;
        if (comboTimer <= 0f) Combo = 0;
        lightResetTimer -= dt;
        if (lightResetTimer <= 0f && !attacking) lightStep = 0;

        UpdateUltimate(dt);

        // Input
        var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);
        Vector3 moveDir = cam != null ? (cam.FlatForward * input.z + cam.FlatRight * input.x) : input;

        bool wantBlock = Input.GetKey(KeyCode.F) && dodgeTimer <= 0f && !attacking;
        if (wantBlock && !blocking) blockStart = Time.time;
        blocking = wantBlock;

        if (Input.GetKeyDown(KeyCode.Space) && dodgeTimer <= 0f && dodgeCooldown <= 0f && !blocking) StartDodge(moveDir);

        if (dodgeTimer <= 0f && !blocking)
        {
            if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
            {
                if (attacking) queuedLight = true; else StartCoroutine(LightAttack());
            }
            if (Input.GetMouseButtonDown(1) && !attacking) StartCoroutine(Heavy());
            if (Input.GetKeyDown(KeyCode.E) && !attacking)
            {
                if (spearInHand) StartCoroutine(ThrowSpear());
                else if (thrown != null) thrown.Recall(this);
            }
            if (Input.GetKeyDown(KeyCode.R) && rage >= maxRage && !UltActive) StartCoroutine(Ultimate());
            if (!attacking)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && Weapon != WeaponKind.Ashbrand) SetWeapon(0, true);
                if (Input.GetKeyDown(KeyCode.Alpha2) && Weapon != WeaponKind.Emberfangs) SetWeapon(1, true);
                if (Input.GetKeyDown(KeyCode.Alpha3) && Weapon != WeaponKind.Thunderjaw) SetWeapon(2, true);
                if (Input.GetKeyDown(KeyCode.Q)) SetWeapon(((int)Weapon + 1) % 3, true);
            }
            if (!attacking && queuedLight) { queuedLight = false; StartCoroutine(LightAttack()); }
        }

        // Movement
        Vector3 horizontal;
        if (dodgeTimer > 0f)
        {
            dodgeTimer -= dt;
            float k = 1f - dodgeTimer / dodgeDuration;
            horizontal = dodgeDir * dodgeSpeed * Mathf.Lerp(1.1f, 0.4f, k);
            health.invulnerable = k < 0.8f;
            float angle = k * 360f;
            var rot = Quaternion.Euler(angle, 0, 0);
            var c = new Vector3(0, 0.75f * rig.scale, 0);
            rig.visual.localRotation = rot;
            rig.visual.localPosition = c - rot * c;
            if (dodgeTimer <= 0f)
            {
                health.invulnerable = false;
                rig.visual.localRotation = Quaternion.identity;
                rig.visual.localPosition = Vector3.zero;
            }
        }
        else
        {
            float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : runSpeed;
            if (UltActive) speed *= Hero.ult == UltimateKind.Tempest ? 1.3f : 1.15f;
            if (Chilled) speed *= 0.6f;
            if (attacking && !spinning) speed *= 0.2f;
            if (spinning) speed *= 0.85f;
            if (blocking) speed *= 0.35f;
            horizontal = moveDir * speed + transform.forward * attackMoveForward;

            Vector3 face = Vector3.zero;
            if ((attacking || blocking) && !spinning && cam != null) face = cam.FlatForward;
            else if (moveDir.sqrMagnitude > 0.01f && !spinning) face = moveDir;
            if (face != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * (attacking ? 20f : 12f));
        }

        knock = Vector3.MoveTowards(knock, Vector3.zero, dt * 30f);
        if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * dt;
        cc.Move((horizontal + knock + Vector3.up * velocity.y) * dt);

        moveAmount = Mathf.Lerp(moveAmount, (dodgeTimer > 0f || (attacking && !spinning)) ? 0f : new Vector3(horizontal.x, 0, horizontal.z).magnitude / sprintSpeed, dt * 10f);
        AnimatePose();
    }

    void StartDodge(Vector3 moveDir)
    {
        StopAllCoroutines();
        attacking = false; queuedLight = false; spinning = false; attackMoveForward = 0f;
        dodgeDir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : -transform.forward;
        dodgeTimer = dodgeDuration;
        dodgeCooldown = dodgeDuration + Hero.dodgeCooldown;
        transform.rotation = Quaternion.LookRotation(dodgeDir);
        rig.visual.localRotation = Quaternion.identity;
        SFX.Play(SFX.S.Swing, 0.4f, 0.7f);
        IdlePose();
    }

    void ApplyGravityOnly()
    {
        if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        cc.Move(Vector3.up * velocity.y * Time.deltaTime);
    }

    // ---------------------------------------------------------------- Animation
    void SetPose(Vector3 armR, Vector3 handR, Vector3 armL, Vector3 torso, float speed = 14f, Vector3? handL = null)
    {
        armRT = Quaternion.Euler(armR);
        handRT = Quaternion.Euler(handR);
        armLT = Quaternion.Euler(armL);
        torsoT = Quaternion.Euler(torso);
        handLT = Quaternion.Euler(handL ?? (Weapon == WeaponKind.Emberfangs ? new Vector3(35, 0, 0) : Vector3.zero));
        poseSpeed = speed;
    }

    void IdlePose()
    {
        if (Weapon == WeaponKind.Thunderjaw) SetPose(new Vector3(-25, 0, 18), new Vector3(-55, 0, 0), new Vector3(10, 0, -10), new Vector3(0, 0, 0), 12f);
        else SetPose(new Vector3(10, 0, 10), new Vector3(35, 0, 0), new Vector3(10, 0, -10), Vector3.zero, 12f);
    }

    void AnimatePose()
    {
        float dt = Time.deltaTime;
        bool free = !attacking && dodgeTimer <= 0f;
        if (free && !blocking) IdlePose();
        if (blocking) SetPose(new Vector3(-70, -35, 0), new Vector3(90, 0, 80), new Vector3(-60, 30, 0), new Vector3(0, -10, 0), 20f);

        walkCycle += dt * Mathf.Lerp(6f, 13f, moveAmount) * (moveAmount > 0.05f ? 1f : 0f);
        float swing = Mathf.Sin(walkCycle) * 40f * Mathf.Clamp01(moveAmount * 1.6f);
        rig.legL.localRotation = Quaternion.Slerp(rig.legL.localRotation, Quaternion.Euler(swing, 0, 0), dt * 15f);
        rig.legR.localRotation = Quaternion.Slerp(rig.legR.localRotation, Quaternion.Euler(-swing, 0, 0), dt * 15f);

        var armR = armRT; var armL = armLT;
        if (free && !blocking)
        {
            armR = armRT * Quaternion.Euler(swing * 0.5f, 0, 0);
            armL = armLT * Quaternion.Euler(-swing * 0.7f, 0, 0);
        }
        float k = 1f - Mathf.Exp(-poseSpeed * dt);
        rig.armR.localRotation = Quaternion.Slerp(rig.armR.localRotation, armR, k);
        rig.handR.localRotation = Quaternion.Slerp(rig.handR.localRotation, handRT, k);
        rig.armL.localRotation = Quaternion.Slerp(rig.armL.localRotation, armL, k);
        rig.handL.localRotation = Quaternion.Slerp(rig.handL.localRotation, handLT, k);
        var breathe = Quaternion.Euler(Mathf.Sin(Time.time * 2f) * 1.5f + moveAmount * 12f, 0, 0);
        rig.torso.localRotation = Quaternion.Slerp(rig.torso.localRotation, torsoT * breathe, k);

        foreach (var kv in trails)
            foreach (var t in kv.Value)
                if (t != null) t.emitting = attacking && kv.Key == Weapon;

        if (spinning) rig.visual.localRotation = Quaternion.Euler(0, Time.time * 1080f, 0);

        // secondary motion: cape and hair trail behind when moving
        float flutter = Mathf.Sin(Time.time * (3f + moveAmount * 9f)) * (2f + moveAmount * 6f);
        if (rig.cape != null)
            rig.cape.localRotation = Quaternion.Slerp(rig.cape.localRotation, Quaternion.Euler(8f + moveAmount * 38f + (dodgeTimer > 0f ? 25f : 0f) + flutter, 0, flutter * 0.4f), dt * 8f);
        if (rig.hairTail != null)
            rig.hairTail.localRotation = Quaternion.Slerp(rig.hairTail.localRotation, Quaternion.Euler(-15f + moveAmount * 45f + flutter * 1.5f, 0, flutter), dt * 10f);
    }

    // ---------------------------------------------------------------- Attacks
    float Spd => Hero.attackSpeed * (RageActive ? 1.35f : (UltActive && Hero.ult == UltimateKind.Tempest ? 1.2f : 1f));

    IEnumerator Wait(float s) { yield return new WaitForSeconds(s / Spd); }

    IEnumerator LightAttack()
    {
        attacking = true;
        switch (Weapon)
        {
            case WeaponKind.Emberfangs: yield return FangCombo(); break;
            case WeaponKind.Thunderjaw: yield return MaulCombo(); break;
            default: yield return SwordCombo(); break;
        }
        attackMoveForward = 0f;
        attacking = false;
    }

    IEnumerator SwordCombo()
    {
        int step = lightStep;
        lightStep = (lightStep + 1) % 3;
        if (step == 0)
        {
            SetPose(new Vector3(-80, 75, 0), new Vector3(90, 0, 0), new Vector3(0, 0, -30), new Vector3(0, 35, 0), 22f);
            yield return Wait(0.13f);
            SetPose(new Vector3(-80, -75, 0), new Vector3(90, 0, 0), new Vector3(20, 0, -20), new Vector3(0, -35, 0), 40f);
            attackMoveForward = 5f; SFX.Play(SFX.S.Swing, 0.7f);
            yield return Wait(0.05f);
            Hit(2.9f, 150f, 16f, 4f, false);
        }
        else if (step == 1)
        {
            SetPose(new Vector3(-80, -80, 0), new Vector3(90, 0, 0), new Vector3(10, 0, -20), new Vector3(0, -35, 0), 22f);
            yield return Wait(0.12f);
            SetPose(new Vector3(-80, 70, 0), new Vector3(90, 0, 0), new Vector3(0, 0, -30), new Vector3(0, 35, 0), 40f);
            attackMoveForward = 5f; SFX.Play(SFX.S.Swing, 0.7f, 1.1f);
            yield return Wait(0.05f);
            Hit(2.9f, 150f, 19f, 4f, false);
        }
        else
        {
            SetPose(new Vector3(-170, 0, 0), new Vector3(0, 0, 0), new Vector3(-150, 0, 0), new Vector3(-12, 0, 0), 18f);
            yield return Wait(0.2f);
            SetPose(new Vector3(-35, 0, 0), new Vector3(50, 0, 0), new Vector3(-40, 0, 0), new Vector3(25, 0, 0), 45f);
            attackMoveForward = 8f; SFX.Play(SFX.S.SwingHeavy, 0.8f);
            yield return Wait(0.06f);
            Hit(3.2f, 100f, 30f, 10f, true);
            FX.Shards(transform.position + transform.forward * 2.2f, GameAssets.I.shard, 6, 5f);
        }
        attackMoveForward = 0f;
        yield return Wait(step == 2 ? 0.3f : 0.16f);
        lightResetTimer = 0.55f;
    }

    IEnumerator FangCombo()
    {
        int step = lightStep;
        lightStep = (lightStep + 1) % 5;
        if (step < 4)
        {
            bool right = step % 2 == 0;
            if (right)
            {
                SetPose(new Vector3(-80, 70, 0), new Vector3(90, 0, 0), new Vector3(-20, 0, -30), new Vector3(0, 30, 0), 30f);
                yield return Wait(0.07f);
                SetPose(new Vector3(-80, -80, 0), new Vector3(90, 0, 0), new Vector3(-20, 0, -30), new Vector3(0, -30, 0), 55f);
            }
            else
            {
                SetPose(new Vector3(-20, 0, 30), new Vector3(35, 0, 0), new Vector3(-80, -70, 0), new Vector3(0, -30, 0), 30f, new Vector3(90, 0, 0));
                yield return Wait(0.07f);
                SetPose(new Vector3(-20, 0, 30), new Vector3(35, 0, 0), new Vector3(-80, 80, 0), new Vector3(0, 30, 0), 55f, new Vector3(90, 0, 0));
            }
            attackMoveForward = 4f; SFX.Play(SFX.S.Swing, 0.55f, 1.3f);
            yield return Wait(0.03f);
            Hit(2.5f, 140f, step < 2 ? 10f : 12f, 2.5f, false);
            attackMoveForward = 0f;
            yield return Wait(0.08f);
            lightResetTimer = 0.45f;
        }
        else
        {
            // X-slash finisher
            SetPose(new Vector3(-160, 35, 0), new Vector3(60, 0, 0), new Vector3(-160, -35, 0), new Vector3(-15, 0, 0), 22f, new Vector3(60, 0, 0));
            yield return Wait(0.16f);
            SetPose(new Vector3(-40, -50, 0), new Vector3(90, 0, 0), new Vector3(-40, 50, 0), new Vector3(25, 0, 0), 55f, new Vector3(90, 0, 0));
            attackMoveForward = 9f; SFX.Play(SFX.S.SwingHeavy, 0.8f, 1.3f);
            yield return Wait(0.05f);
            Hit(3f, 200f, 26f, 9f, true);
            FX.Shards(transform.position + transform.forward * 2f + Vector3.up, GameAssets.I.fang, 10, 6f, 0.08f);
            attackMoveForward = 0f;
            yield return Wait(0.26f);
            lightResetTimer = 0.4f;
        }
    }

    IEnumerator MaulCombo()
    {
        int step = lightStep;
        lightStep = (lightStep + 1) % 2;
        if (step == 0)
        {
            SetPose(new Vector3(-95, 95, 0), new Vector3(90, 0, 0), new Vector3(-60, 40, 0), new Vector3(0, 55, 0), 16f);
            yield return Wait(0.28f);
            SetPose(new Vector3(-75, -85, 0), new Vector3(90, 0, 0), new Vector3(-50, -30, 0), new Vector3(0, -45, 0), 34f);
            attackMoveForward = 6f; SFX.Play(SFX.S.SwingHeavy, 0.9f, 0.8f);
            yield return Wait(0.07f);
            Hit(3.4f, 170f, 36f, 14f, true);
            attackMoveForward = 0f;
            yield return Wait(0.3f);
        }
        else
        {
            SetPose(new Vector3(-175, 0, 0), new Vector3(-10, 0, 0), new Vector3(-165, 0, 0), new Vector3(-18, 0, 0), 15f);
            yield return Wait(0.34f);
            SetPose(new Vector3(-25, 0, 0), new Vector3(65, 0, 0), new Vector3(-30, 0, 0), new Vector3(32, 0, 0), 48f);
            attackMoveForward = 6f; SFX.Play(SFX.S.SwingHeavy, 1f, 0.7f);
            yield return Wait(0.07f);
            var p = transform.position + transform.forward * 2.6f;
            FX.Ring(p, GameAssets.I.lightning, 3.2f, 0.3f);
            FX.Shards(p, GameAssets.I.shard, 10, 7f, 0.14f);
            SFX.Play(SFX.S.Zap, 0.7f);
            Hit(3.6f, 360f, 50f, 16f, true, p);
            attackMoveForward = 0f;
            yield return Wait(0.42f);
        }
        lightResetTimer = 0.6f;
    }

    // Used by AutoTest (-autotest) to drive the hero without a human.
    public void AutoAction(int kind)
    {
        if (dead || dodgeTimer > 0f || attacking) return;
        switch (kind)
        {
            case 0: StartCoroutine(LightAttack()); break;
            case 1: StartCoroutine(Heavy()); break;
            case 2: if (spearInHand) StartCoroutine(ThrowSpear()); else if (thrown != null) thrown.Recall(this); break;
            case 3: if (rage >= maxRage && !UltActive) StartCoroutine(Ultimate()); break;
            case 4: SetWeapon(((int)Weapon + 1) % 3, true); break;
        }
    }

    IEnumerator Heavy()
    {
        attacking = true;
        switch (Weapon)
        {
            case WeaponKind.Emberfangs: yield return Cyclone(); break;
            case WeaponKind.Thunderjaw: yield return Skyfall(); break;
            default: yield return HeavySlam(); break;
        }
        attackMoveForward = 0f;
        attacking = false;
    }

    IEnumerator HeavySlam()
    {
        SetPose(new Vector3(-175, 0, 0), new Vector3(0, 0, 0), new Vector3(-175, 0, 0), new Vector3(-15, 0, 0), 16f);
        if (cc.isGrounded) velocity.y = 8f;
        attackMoveForward = 4f;
        SFX.Play(SFX.S.SwingHeavy, 0.6f, 0.8f);
        yield return Wait(0.32f);
        SetPose(new Vector3(-20, 0, 0), new Vector3(65, 0, 0), new Vector3(-30, 0, 0), new Vector3(30, 0, 0), 50f);
        velocity.y = -20f;
        float t = 0f;
        while (!cc.isGrounded && t < 0.5f) { t += Time.deltaTime; yield return null; }
        attackMoveForward = 0f;
        var p = transform.position + transform.forward * 1.6f;
        FX.Ring(p, GameAssets.I.shockwave, 5.5f, 0.4f);
        FX.Shards(p, GameAssets.I.shard, 16, 9f, 0.18f);
        FX.Shake(1f);
        SFX.Play(SFX.S.Boom, 0.9f);
        Hit(4.8f, 360f, 42f, 15f, true, p);
        yield return Wait(0.5f);
    }

    IEnumerator Cyclone()
    {
        SetPose(new Vector3(-90, 0, 80), new Vector3(90, 0, 0), new Vector3(-90, 0, -80), Vector3.zero, 30f, new Vector3(90, 0, 0));
        yield return Wait(0.08f);
        spinning = true;
        float dur = 1.1f, t = 0f, tick = 0f;
        while (t < dur)
        {
            t += Time.deltaTime * Spd;
            tick -= Time.deltaTime * Spd;
            if (tick <= 0f)
            {
                tick = 0.14f;
                SFX.Play(SFX.S.Swing, 0.5f, 1.5f);
                Hit(3.2f, 360f, 9f, 3.5f, false, transform.position);
                FX.Ring(transform.position, GameAssets.I.rageAura, 3.2f, 0.18f);
            }
            yield return null;
        }
        spinning = false;
        rig.visual.localRotation = Quaternion.identity;
        Hit(3.6f, 360f, 20f, 12f, true, transform.position);
        FX.Shards(transform.position + Vector3.up, GameAssets.I.fang, 12, 7f, 0.08f);
        SFX.Play(SFX.S.SwingHeavy, 0.8f, 1.3f);
        yield return Wait(0.25f);
    }

    IEnumerator Skyfall()
    {
        SetPose(new Vector3(-175, 0, 0), new Vector3(-20, 0, 0), new Vector3(-170, 0, 0), new Vector3(-20, 0, 0), 14f);
        if (cc.isGrounded) velocity.y = 11f;
        attackMoveForward = 5f;
        SFX.Play(SFX.S.Zap, 0.6f, 0.8f);
        yield return Wait(0.42f);
        SetPose(new Vector3(-20, 0, 0), new Vector3(65, 0, 0), new Vector3(-30, 0, 0), new Vector3(34, 0, 0), 50f);
        velocity.y = -26f;
        float t = 0f;
        while (!cc.isGrounded && t < 0.6f) { t += Time.deltaTime; yield return null; }
        attackMoveForward = 0f;
        var p = transform.position + transform.forward * 2f;
        LightningBolt.Create(p + Vector3.up * 30f, p, GameAssets.I.lightning, 0.6f, 0.35f);
        FX.Ring(p, GameAssets.I.lightning, 6.5f, 0.45f);
        FX.Shards(p, GameAssets.I.shard, 20, 10f, 0.2f);
        FX.Shake(1.3f);
        SFX.Play(SFX.S.Boom, 1f, 0.8f);
        SFX.Play(SFX.S.Thunder, 0.8f);
        Hit(5.2f, 360f, 46f, 17f, true, p);
        ChainLightning(p, 6, 12f, 30f);
        yield return Wait(0.55f);
    }

    // Arcs lightning from a point through up to n enemies.
    void ChainLightning(Vector3 from, int n, float range, float damage)
    {
        var hit = new HashSet<Enemy>();
        Vector3 cur = from + Vector3.up;
        for (int i = 0; i < n; i++)
        {
            Enemy best = null; float bd = range;
            foreach (var e in Enemy.All)
            {
                if (e.health.IsDead || hit.Contains(e)) continue;
                float d = Vector3.Distance(cur, e.transform.position);
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) break;
            hit.Add(best);
            var to = best.transform.position + Vector3.up * 1.2f * best.transform.lossyScale.y;
            LightningBolt.Create(cur, to, GameAssets.I.lightning, 0.18f, 0.25f);
            float dmg = damage * OutgoingMul;
            if (best.health.TakeDamage(new DamageInfo { amount = dmg, source = cur, knockback = 4f, heavy = true, fromPlayer = true })) RegisterHit(dmg);
            best.Stagger(0.7f, true);
            cur = to;
        }
        if (hit.Count > 0) SFX.Play(SFX.S.Zap, 0.8f);
    }

    IEnumerator ThrowSpear()
    {
        attacking = true;
        SetPose(new Vector3(10, 0, 10), new Vector3(35, 0, 0), new Vector3(-165, 0, -15), new Vector3(-5, -25, 0), 20f, Vector3.zero);
        yield return new WaitForSeconds(0.16f);
        SetPose(new Vector3(10, 0, 10), new Vector3(35, 0, 0), new Vector3(-75, 0, 0), new Vector3(10, 20, 0), 40f, Vector3.zero);
        yield return new WaitForSeconds(0.04f);

        Vector3 start = rig.handL.position + Vector3.up * 0.3f;
        Vector3 aim = start + transform.forward * 60f;
        if (cam != null)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            aim = ray.origin + ray.direction * 80f;
            var hits = Physics.RaycastAll(ray, 80f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
                if (h.distance < best) { best = h.distance; aim = h.point; }
            }
        }
        var go = new GameObject("ThrownSpear");
        go.transform.position = start;
        go.transform.rotation = Quaternion.LookRotation((aim - start).normalized);
        BuildSpearMesh(go.transform, GameAssets.I.heroArmor, GameAssets.I.spear);
        thrown = go.AddComponent<Spear>();
        thrown.Launch(this, (aim - start).normalized);
        spearInHand = false;
        spearVisual.gameObject.SetActive(false);
        FX.Shake(0.15f);
        SFX.Play(SFX.S.Throw, 0.8f);
        yield return new WaitForSeconds(0.2f);
        attacking = false;
    }

    public Vector3 HandLPosition => rig != null ? rig.handL.position : transform.position + Vector3.up * 1.2f;

    public void CatchSpear()
    {
        spearInHand = true;
        thrown = null;
        spearVisual.gameObject.SetActive(Weapon != WeaponKind.Emberfangs);
        FX.Shake(0.3f);
        FX.Shards(rig.handL.position, GameAssets.I.spear, 6, 3f, 0.08f);
        SFX.Play(SFX.S.Parry, 0.35f, 1.6f);
    }

    // ---------------------------------------------------------------- Ultimates
    IEnumerator Ultimate()
    {
        attacking = true;
        rageTimer = Hero.ult == UltimateKind.Rage ? rageDuration : 10f;
        ultTick = 0.6f;
        SetPose(new Vector3(-150, 0, 40), new Vector3(0, 0, 0), new Vector3(-150, 0, -40), new Vector3(-20, 0, 0), 15f, Vector3.zero);
        FX.HitStop(0.12f);
        SFX.Play(SFX.S.Roar, 0.8f, Hero.ult == UltimateKind.Tempest ? 1.4f : (Hero.ult == UltimateKind.MoltenAegis ? 0.75f : 1f));
        yield return new WaitForSeconds(0.3f);
        var A = GameAssets.I;
        switch (Hero.ult)
        {
            case UltimateKind.Rage:
                FX.Ring(transform.position, A.rageAura, 7f, 0.5f);
                FX.Shards(transform.position + Vector3.up, A.heroEmber, 24, 10f, 0.15f);
                Hit(6f, 360f, 25f, 18f, true, transform.position);
                break;
            case UltimateKind.Tempest:
                FX.Ring(transform.position, A.lightning, 9f, 0.5f);
                FX.Shards(transform.position + Vector3.up, A.ysraGlow, 24, 10f, 0.12f);
                SFX.Play(SFX.S.Thunder, 1f);
                foreach (var e in Enemy.All.ToArray())
                {
                    if (Vector3.Distance(e.transform.position, transform.position) > 10f) continue;
                    LightningBolt.Create(e.transform.position + Vector3.up * 25f, e.transform.position, A.lightning, 0.4f, 0.3f);
                    float dmg = 30f * Hero.damageMul;
                    if (e.health.TakeDamage(new DamageInfo { amount = dmg, source = transform.position, knockback = 8f, heavy = true, fromPlayer = true })) RegisterHit(dmg);
                    e.Stagger(1f, true);
                }
                break;
            case UltimateKind.MoltenAegis:
                FX.Ring(transform.position, A.gorrunGlow, 8f, 0.6f);
                FX.Shards(transform.position + Vector3.up, A.fire, 30, 12f, 0.18f);
                SFX.Play(SFX.S.Boom, 1f, 0.7f);
                Hit(7f, 360f, 40f, 22f, true, transform.position);
                break;
        }
        FX.Shake(1.2f);
        GameManager.I?.ShowMessage(Hero.ultName + " UNLEASHED", 1.5f);
        yield return new WaitForSeconds(0.3f);
        attacking = false;
    }

    void UpdateUltimate(float dt)
    {
        if (rageTimer > 0f)
        {
            float total = Hero.ult == UltimateKind.Rage ? rageDuration : 10f;
            rageTimer -= dt;
            rage = maxRage * Mathf.Clamp01(rageTimer / total);
            rageLight.intensity = Mathf.Lerp(rageLight.intensity, 4f + Mathf.Sin(Time.time * 12f) * 1f, dt * 8f);
            health.damageTakenMultiplier = Hero.damageTaken * (Hero.ult == UltimateKind.Rage ? 0.5f : Hero.ult == UltimateKind.MoltenAegis ? 0.25f : 1f);

            ultTick -= dt;
            if (ultTick <= 0f)
            {
                if (Hero.ult == UltimateKind.Tempest)
                {
                    ultTick = 0.45f;
                    var targets = new List<Enemy>();
                    foreach (var e in Enemy.All) if (!e.health.IsDead && Vector3.Distance(e.transform.position, transform.position) < 15f) targets.Add(e);
                    if (targets.Count > 0)
                    {
                        var e = targets[Random.Range(0, targets.Count)];
                        LightningBolt.Create(e.transform.position + Vector3.up * 25f + Random.insideUnitSphere * 3f, e.transform.position, GameAssets.I.lightning, 0.35f, 0.25f);
                        FX.Ring(e.transform.position, GameAssets.I.lightning, 2f, 0.2f);
                        SFX.Play(SFX.S.Thunder, 0.45f, 1.3f);
                        float dmg = 26f * Hero.damageMul;
                        if (e.health.TakeDamage(new DamageInfo { amount = dmg, source = e.transform.position - e.transform.forward, knockback = 3f, heavy = true, fromPlayer = true })) RegisterHit(dmg);
                        e.Stagger(0.5f, false);
                    }
                }
                else if (Hero.ult == UltimateKind.MoltenAegis)
                {
                    ultTick = 0.4f;
                    FX.Ring(transform.position, GameAssets.I.gorrunGlow, 4.2f, 0.3f);
                    FX.Shards(transform.position + Vector3.up * 0.3f, GameAssets.I.fire, 3, 4f, 0.1f);
                    Hit(4.2f, 360f, 9f, 3f, false, transform.position, true);
                }
                else ultTick = 1f;
            }

            if (rageTimer <= 0f) { rage = 0f; health.damageTakenMultiplier = Hero.damageTaken; }
        }
        else rageLight.intensity = Mathf.Lerp(rageLight.intensity, 0f, dt * 5f);
    }

    // Returns number of enemies hit.
    public int Hit(float range, float arc, float damage, float knockback, bool heavy, Vector3? centerOverride = null, bool quiet = false)
    {
        damage *= OutgoingMul;
        Vector3 center = centerOverride ?? (transform.position + transform.forward * (range * 0.45f));
        center.y = transform.position.y + 1f;
        var cols = Physics.OverlapSphere(center, range, ~0, QueryTriggerInteraction.Ignore);
        var done = new HashSet<Enemy>();
        int count = 0;
        foreach (var c in cols)
        {
            var e = c.GetComponentInParent<Enemy>();
            if (e == null || done.Contains(e) || e.health.IsDead) continue;
            done.Add(e);
            var to = e.transform.position - transform.position; to.y = 0;
            if (arc < 360f && to.sqrMagnitude > 1f && Vector3.Angle(transform.forward, to) > arc * 0.5f) continue;
            var info = new DamageInfo { amount = damage, source = transform.position, knockback = knockback, heavy = heavy, fromPlayer = true };
            if (e.health.TakeDamage(info)) { count++; RegisterHit(damage); }
        }
        if (count > 0 && !quiet)
        {
            FX.HitStop(heavy ? 0.08f : 0.04f);
            FX.Shake(heavy ? 0.5f : 0.2f);
            SFX.Play(heavy ? SFX.S.HeavyHit : SFX.S.Hit, heavy ? 0.9f : 0.7f);
        }
        return count;
    }

    public void RegisterHit(float damage)
    {
        Combo++;
        comboTimer = 2.5f;
        if (!UltActive) rage = Mathf.Min(maxRage, rage + 4f + Combo * 0.2f);
        else if (Hero.ult == UltimateKind.Rage) health.Heal(damage * 0.08f);
    }

    public void AddRage(float amount) { if (!UltActive) rage = Mathf.Min(maxRage, rage + amount); }

    public void Chill(float seconds)
    {
        if (chillTimer <= 0f) FX.Shards(transform.position + Vector3.up, GameAssets.I.iceShard, 8, 3f, 0.08f);
        chillTimer = Mathf.Max(chillTimer, seconds);
    }

    // ---------------------------------------------------------------- Defense
    // Called by enemy attacks / projectiles / hazards. Handles dodge, block and parry.
    public HitResult ReceiveAttack(Vector3 source, Enemy attacker, float damage, float knockback, bool unblockable = false)
    {
        if (dead) return HitResult.Ignored;
        if (health.invulnerable)
        {
            // Perfect dodge: dodged right at the start of the roll -> slow-motion + ultimate charge.
            if (dodgeTimer > dodgeDuration * 0.55f && Time.time - lastPerfectDodge > 1.5f)
            {
                lastPerfectDodge = Time.time;
                FX.SlowMo(0.7f, 0.35f);
                AddRage(10f);
                GameManager.I?.ShowMessage("PERFECT DODGE", 0.8f);
                SFX.Play(SFX.S.Blink, 0.6f, 0.8f);
            }
            return HitResult.Dodged;
        }
        var toSrc = source - transform.position; toSrc.y = 0;
        if (!unblockable && blocking && Vector3.Angle(transform.forward, toSrc) < 75f)
        {
            if (Time.time - blockStart < 0.25f)
            {
                if (attacker != null) attacker.Stagger(1.6f, true);
                FX.HitStop(0.15f);
                FX.Shake(0.6f);
                FX.Shards(transform.position + transform.forward + Vector3.up * 1.4f, GameAssets.I.blade, 12, 6f, 0.08f);
                SFX.Play(SFX.S.Parry, 1f);
                AddRage(15f);
                if (attacker != null) GameManager.I?.ShowMessage("PARRY!", 0.8f);
                return HitResult.Parried;
            }
            damage *= 0.2f;
            knockback *= 0.4f;
            AddRage(3f);
            FX.Shards(transform.position + transform.forward + Vector3.up * 1.4f, GameAssets.I.blade, 5, 4f, 0.06f);
            SFX.Play(SFX.S.Parry, 0.45f, 0.7f);
            health.TakeDamage(new DamageInfo { amount = damage, source = source, knockback = knockback });
            return HitResult.Blocked;
        }
        if (UltActive && Hero.ult == UltimateKind.MoltenAegis) knockback = 0f;
        health.TakeDamage(new DamageInfo { amount = damage, source = source, knockback = knockback });
        return HitResult.Hit;
    }

    void OnDamaged(DamageInfo info)
    {
        var dir = transform.position - info.source; dir.y = 0;
        if (dir.sqrMagnitude > 0.001f) knock = dir.normalized * info.knockback;
        FX.Shake(0.4f);
        if (!blocking) SFX.Play(SFX.S.Hurt, 0.7f);
        bool immovable = UltActive && Hero.ult == UltimateKind.MoltenAegis;
        if (!blocking && info.amount > 5f && !immovable && !spinning)
        {
            StopAllCoroutines();
            attacking = false; attackMoveForward = 0f;
            SetPose(new Vector3(30, 0, 30), new Vector3(35, 0, 0), new Vector3(30, 0, -30), new Vector3(-20, 0, 0), 30f);
        }
    }

    void OnDied()
    {
        dead = true;
        StopAllCoroutines();
        spinning = false;
        foreach (var kv in trails) foreach (var t in kv.Value) if (t != null) t.emitting = false;
        StartCoroutine(DeathFall());
        GameManager.I?.OnPlayerDied();
    }

    IEnumerator DeathFall()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            rig.visual.localRotation = Quaternion.Euler(-85f * Mathf.SmoothStep(0, 1, t), 0, 0);
            yield return null;
        }
    }
}
