using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Grunt, Brute, Boss, Caster, Stalker }

[RequireComponent(typeof(CharacterController))]
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public EnemyType type;
    public int bossKind;
    public string displayName;
    public Health health { get; private set; }
    public bool Phase2 => phase2;

    enum State { Spawning, Chase, Windup, Charging, Recover, Stagger, Dead }
    enum Attack { Swipe, Slam, Charge, Bolt, Volley, Nova, SpikeLine, Storm }

    State state;
    Attack nextAttack;
    float stateTimer;
    float attackCooldownTimer;
    CharacterController cc;
    Rig rig;
    MaterialPropertyBlock mpb;
    Color mapTint = Color.white;
    Material boltMat;
    bool boltChill;

    float speed, damage, range, windup, cooldown, poiseMax, knockResist, scale;
    float poise;
    Vector3 knock;
    float vy;
    float freezeTimer;
    float staggerImmune;
    float flash;
    float blinkCooldown = 3f;
    float strafeSign = 1f;
    int attackCount;
    bool phase2;
    bool summoned;
    float walkCycle;
    Vector3 chargeDir;
    bool chargeHit;

    Quaternion armRT, armLT, torsoT, handRT;

    bool IsLight => type == EnemyType.Grunt || type == EnemyType.Caster || type == EnemyType.Stalker;

    public void Init(EnemyType t, int mapIndex)
    {
        type = t;
        var map = MapDef.All[Mathf.Clamp(mapIndex, 0, MapDef.All.Length - 1)];
        bossKind = map.bossKind;
        mapTint = map.enemyTint;
        cc = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        var A = GameAssets.I;
        var look = new CharacterBuilder.Look { eye = A.enemyEye };
        boltMat = mapIndex == 1 ? A.frostGlow : mapIndex == 2 ? A.stormGlow : A.bolt;
        boltChill = mapIndex == 1;
        strafeSign = Random.value < 0.5f ? -1f : 1f;

        switch (t)
        {
            case EnemyType.Grunt:
                displayName = map.gruntName;
                health.SetMax(55f); speed = 4.4f; damage = 12f; range = 2.0f; windup = 0.55f; cooldown = 1.4f;
                poiseMax = 0f; knockResist = 0f; scale = 0.95f;
                look.skin = A.gruntSkin; look.armor = A.gruntArmor; look.scale = scale;
                break;
            case EnemyType.Brute:
                displayName = map.bruteName;
                health.SetMax(190f); speed = 3.2f; damage = 24f; range = 2.8f; windup = 0.85f; cooldown = 2.0f;
                poiseMax = 45f; knockResist = 0.6f; scale = 1.45f;
                look.skin = A.bruteSkin; look.armor = A.bruteArmor; look.scale = scale; look.horns = true; look.bulky = true;
                break;
            case EnemyType.Caster:
                displayName = "Hexcaller";
                health.SetMax(45f); speed = 3.8f; damage = 14f; range = 16f; windup = 0.8f; cooldown = 2.3f;
                poiseMax = 0f; knockResist = 0f; scale = 1.0f;
                look.skin = A.gruntSkin; look.armor = A.casterRobe; look.cape = A.casterRobe; look.scale = scale; look.hood = true;
                break;
            case EnemyType.Stalker:
                displayName = "Shade Stalker";
                health.SetMax(42f); speed = 7.0f; damage = 11f; range = 1.9f; windup = 0.38f; cooldown = 1.1f;
                poiseMax = 0f; knockResist = 0f; scale = 0.9f;
                look.skin = A.stalkerSkin; look.armor = A.stalkerSkin; look.scale = scale; look.hood = true;
                break;
            case EnemyType.Boss:
                displayName = BossDefs.Names[bossKind];
                look.skin = A.bossSkin; look.scale = 2.3f; look.horns = bossKind != 1; look.helmet = true; look.bulky = true;
                switch (bossKind)
                {
                    case 1:
                        health.SetMax(1150f); speed = 3.6f; damage = 26f; range = 3.4f; windup = 0.7f; cooldown = 1.4f;
                        look.armor = A.frostBossArmor; look.cape = A.frostBossArmor; look.eye = A.frostGlow; look.bulky = false; look.scale = 2.2f;
                        break;
                    case 2:
                        health.SetMax(1250f); speed = 3.9f; damage = 32f; range = 3.8f; windup = 0.8f; cooldown = 1.5f;
                        look.armor = A.stormBossArmor; look.cape = A.stormBossArmor; look.eye = A.stormGlow; look.scale = 2.4f;
                        break;
                    default:
                        health.SetMax(1100f); speed = 3.8f; damage = 30f; range = 3.6f; windup = 0.75f; cooldown = 1.5f;
                        look.armor = A.bossArmor; look.cape = A.bossArmor;
                        break;
                }
                poiseMax = 140f; knockResist = 0.9f; scale = look.scale;
                break;
        }

        rig = CharacterBuilder.Build(transform, look);
        switch (t)
        {
            case EnemyType.Grunt: CharacterBuilder.Club(rig, A.gruntArmor, 0.9f, 0.24f); break;
            case EnemyType.Brute: CharacterBuilder.Club(rig, A.bruteArmor, 1.3f, 0.42f); break;
            case EnemyType.Caster:
                CharacterBuilder.Orb(rig, rig.handL, boltMat, 0.3f);
                CharacterBuilder.Club(rig, A.iron, 1.4f, 0.12f); // staff
                break;
            case EnemyType.Stalker:
                CharacterBuilder.Fang(rig, rig.handR, A.stalkerSkin, A.bolt, "ClawR", out _);
                CharacterBuilder.Fang(rig, rig.handL, A.stalkerSkin, A.bolt, "ClawL", out _);
                break;
            case EnemyType.Boss: BuildBoss(A); break;
        }

        cc.height = 2f * scale;
        cc.radius = 0.45f * scale;
        cc.center = new Vector3(0, scale, 0);
        cc.stepOffset = 0.4f;

        mpb = new MaterialPropertyBlock();
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        All.Add(this);

        state = State.Spawning;
        stateTimer = 0.9f;
        attackCooldownTimer = Random.Range(0.3f, 1.2f);
        rig.visual.localPosition = Vector3.down * 2.2f * scale;
        SetIdle();
        FX.Ring(transform.position, A.spawnRing, 1.6f * scale, 0.9f);
        FX.Shards(transform.position, A.spawnRing, 8, 5f, 0.1f);
        SFX.Play(SFX.S.Spawn, t == EnemyType.Boss ? 1f : 0.25f, t == EnemyType.Boss ? 0.6f : 1f);
        if (t == EnemyType.Boss) SFX.Play(SFX.S.Roar, 1f, bossKind == 1 ? 1.3f : bossKind == 2 ? 0.8f : 1f);
    }

    void BuildBoss(GameAssets A)
    {
        Material glow; Color lightCol;
        switch (bossKind)
        {
            case 1:
                glow = A.frostGlow; lightCol = new Color(0.4f, 0.8f, 1f);
                CharacterBuilder.GreatSword(rig, A.frostBossArmor, A.frostGlow, 2.2f); // rime glaive
                for (int i = -2; i <= 2; i++) // crown of ice
                    CharacterBuilder.Part(rig, PrimitiveType.Cube, rig.head, new Vector3(i * 0.1f, 0.55f + (2 - Mathf.Abs(i)) * 0.05f, 0.05f),
                        new Vector3(0.06f, 0.22f + (2 - Mathf.Abs(i)) * 0.1f, 0.06f), A.crystal, new Vector3(0, 45, -i * 12f), true);
                CharacterBuilder.Part(rig, PrimitiveType.Cube, rig.torso, new Vector3(0, 0.95f, -0.25f), new Vector3(1.0f, 0.5f, 0.06f), A.crystal, new Vector3(-20, 0, 0), true);
                break;
            case 2:
                glow = A.stormGlow; lightCol = new Color(0.6f, 0.6f, 1f);
                CharacterBuilder.Maul(rig, A.stormBossArmor, A.stormBossArmor, A.stormGlow, out _);
                // lightning-rod spikes on the shoulders
                foreach (var arm in new[] { rig.armL, rig.armR })
                    CharacterBuilder.Part(rig, PrimitiveType.Cube, arm, new Vector3(0, 0.35f, 0), new Vector3(0.07f, 0.5f, 0.07f), A.stormGlow, default, true);
                break;
            default:
                glow = A.bossGlow; lightCol = new Color(0.7f, 0.25f, 1f);
                CharacterBuilder.GreatSword(rig, A.bossArmor, A.bossGlow, 1.8f);
                break;
        }
        CharacterBuilder.Part(rig, PrimitiveType.Sphere, rig.torso, new Vector3(0, 0.45f, 0.2f), new Vector3(0.22f, 0.22f, 0.1f), glow, default, true);
        var l = new GameObject("BossLight").AddComponent<Light>();
        l.transform.SetParent(transform, false);
        l.transform.localPosition = new Vector3(0, 3.5f, 1f);
        l.type = LightType.Point; l.color = lightCol; l.range = 9f; l.intensity = 2.5f;
    }

    void OnDestroy() { All.Remove(this); }

    void SetPose(Vector3 armR, Vector3 armL, Vector3 torso, Vector3 handR)
    {
        armRT = Quaternion.Euler(armR); armLT = Quaternion.Euler(armL); torsoT = Quaternion.Euler(torso); handRT = Quaternion.Euler(handR);
    }

    void SetIdle()
    {
        if (type == EnemyType.Caster) SetPose(new Vector3(-20, 0, 12), new Vector3(-45, 20, -10), new Vector3(8, 0, 0), new Vector3(80, 0, 0));
        else if (type == EnemyType.Stalker) SetPose(new Vector3(-30, 0, 25), new Vector3(-30, 0, -25), new Vector3(25, 0, 0), new Vector3(60, 0, 0));
        else SetPose(new Vector3(15, 0, 12), new Vector3(15, 0, -12), new Vector3(8, 0, 0), new Vector3(30, 0, 0));
    }

    PlayerController Player => PlayerController.I;

    void Update()
    {
        if (state == State.Dead || rig == null) return;
        float dt = Time.deltaTime;
        float timeMul = freezeTimer > 0f ? 0.4f : 1f;
        freezeTimer -= dt;
        staggerImmune -= dt;
        blinkCooldown -= dt;
        flash = Mathf.MoveTowards(flash, 0f, dt * 6f);
        attackCooldownTimer -= dt * timeMul;
        stateTimer -= dt * timeMul;

        var p = Player;
        bool playerAlive = p != null && !p.health.IsDead && (GameManager.I == null || GameManager.I.InputEnabled);
        Vector3 toPlayer = playerAlive ? p.transform.position - transform.position : transform.forward * 100f;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;
        Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : transform.forward;
        Vector3 move = Vector3.zero;

        switch (state)
        {
            case State.Spawning:
                rig.visual.localPosition = Vector3.Lerp(rig.visual.localPosition, Vector3.zero, dt * 6f);
                if (stateTimer <= 0f) { rig.visual.localPosition = Vector3.zero; state = State.Chase; }
                break;

            case State.Chase:
                SetIdle();
                if (!playerAlive) break;
                Face(dirToPlayer, 8f);
                float spd = speed * (phase2 ? 1.3f : 1f) * timeMul;
                if (type == EnemyType.Caster)
                {
                    // keep a casting distance and circle the player
                    var strafe = Vector3.Cross(Vector3.up, dirToPlayer) * strafeSign;
                    if (dist < 7f) move = (-dirToPlayer + strafe * 0.4f).normalized * spd;
                    else if (dist > 13f) move = dirToPlayer * spd;
                    else move = strafe * spd * 0.5f;
                    if (Random.value < dt * 0.3f) strafeSign = -strafeSign;
                    if (dist < range && attackCooldownTimer <= 0f) BeginWindup(Attack.Bolt);
                    break;
                }
                if (type == EnemyType.Stalker && dist > 5f && dist < 16f && blinkCooldown <= 0f)
                {
                    Blink(p);
                    break;
                }
                if (dist > range * 0.85f) move = dirToPlayer * spd;
                var choice = ChooseAttack(dist);
                if (choice.HasValue) BeginWindup(choice.Value);
                break;

            case State.Windup:
                if (playerAlive) Face(dirToPlayer, nextAttack == Attack.Charge || nextAttack == Attack.Bolt || nextAttack == Attack.Volley ? 10f : 3f);
                if (stateTimer <= 0f) Strike(p, dist, dirToPlayer, playerAlive);
                break;

            case State.Charging:
                move = chargeDir * 18f * timeMul;
                if (!chargeHit && playerAlive && dist < range * 0.8f)
                {
                    chargeHit = true;
                    p.ReceiveAttack(transform.position, this, damage * 1.1f, 20f);
                }
                if (bossKind == 2 && Random.value < dt * 8f) LightningBolt.Create(transform.position + Vector3.up * 4f, transform.position + Random.insideUnitSphere * 3f, GameAssets.I.lightning, 0.12f, 0.12f);
                if (stateTimer <= 0f) { state = State.Recover; stateTimer = 0.9f; FX.Shake(0.4f); }
                break;

            case State.Recover:
                if (stateTimer <= 0f) { state = State.Chase; attackCooldownTimer = cooldown * (phase2 ? 0.7f : 1f) * Random.Range(0.8f, 1.3f); SetIdle(); }
                break;

            case State.Stagger:
                SetPose(new Vector3(40, 0, 40), new Vector3(40, 0, -40), new Vector3(-25, 0, 0), new Vector3(30, 0, 0));
                if (stateTimer <= 0f) { state = State.Chase; SetIdle(); attackCooldownTimer = Mathf.Max(attackCooldownTimer, 0.25f); staggerImmune = 1.6f; }
                break;
        }

        // Separation from other enemies
        foreach (var o in All)
        {
            if (o == this || o.state == State.Dead) continue;
            var d = transform.position - o.transform.position; d.y = 0;
            float min = (cc.radius + o.cc.radius) * 1.1f;
            if (d.sqrMagnitude < min * min && d.sqrMagnitude > 0.0001f)
                move += d.normalized * (min - d.magnitude) * 6f;
        }

        knock = Vector3.MoveTowards(knock, Vector3.zero, dt * 25f);
        if (cc.isGrounded && vy < 0) vy = -2f;
        vy += -28f * dt;
        if (cc.enabled) cc.Move((move + knock + Vector3.up * vy) * dt);

        Animate(move.magnitude, dt);
    }

    Attack? ChooseAttack(float dist)
    {
        if (attackCooldownTimer > 0f) return null;
        bool inMelee = dist <= range + 0.3f;
        if (type != EnemyType.Boss)
        {
            if (!inMelee) return null;
            attackCount++;
            return type == EnemyType.Brute && attackCount % 3 == 0 ? Attack.Slam : Attack.Swipe;
        }
        Attack? a = null;
        switch (bossKind)
        {
            case 1: // Skaldra
                if (dist > 7f) a = phase2 && attackCount % 2 == 0 ? Attack.SpikeLine : Attack.Volley;
                else if (inMelee) a = attackCount % 3 == 2 ? Attack.Nova : Attack.Swipe;
                break;
            case 2: // Ozrek
                if (dist > 7f) a = phase2 && attackCount % 2 == 0 ? Attack.Charge : Attack.Storm;
                else if (inMelee) a = attackCount % 3 == 2 ? Attack.Slam : (attackCount % 5 == 4 ? Attack.Storm : Attack.Swipe);
                break;
            default: // Vorthak
                if (phase2 && dist > 8f) a = Attack.Charge;
                else if (inMelee) a = attackCount % 3 == 2 ? Attack.Slam : Attack.Swipe;
                break;
        }
        if (a.HasValue) attackCount++;
        return a;
    }

    void Blink(PlayerController p)
    {
        var A = GameAssets.I;
        FX.Shards(transform.position + Vector3.up, A.stalkerSkin, 10, 5f, 0.1f);
        FX.Ring(transform.position, A.spawnRing, 1.5f, 0.3f);
        var behind = p.transform.position - p.transform.forward * 2.4f + p.transform.right * Random.Range(-1f, 1f);
        behind.y = transform.position.y;
        cc.enabled = false;
        transform.position = behind;
        cc.enabled = true;
        transform.rotation = Quaternion.LookRotation(Flat(p.transform.position - behind));
        FX.Shards(transform.position + Vector3.up, A.stalkerSkin, 10, 5f, 0.1f);
        SFX.Play(SFX.S.Blink, 0.7f);
        blinkCooldown = Random.Range(4.5f, 7f);
        BeginWindup(Attack.Swipe);
        stateTimer = 0.55f; // fair reaction window after the teleport
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward; }

    void Face(Vector3 dir, float rate)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rate);
    }

    void BeginWindup(Attack a)
    {
        nextAttack = a;
        state = State.Windup;
        float w = windup * (phase2 ? 0.8f : 1f);
        switch (a)
        {
            case Attack.Slam: w *= 1.3f; SetPose(new Vector3(-170, 0, 10), new Vector3(-170, 0, -10), new Vector3(-20, 0, 0), new Vector3(0, 0, 0)); break;
            case Attack.Charge: w = 0.9f; SetPose(new Vector3(-60, 0, 30), new Vector3(-60, 0, -30), new Vector3(30, 0, 0), new Vector3(90, 0, 0)); break;
            case Attack.Bolt: SetPose(new Vector3(-20, 0, 12), new Vector3(-100, 0, 0), new Vector3(-10, -20, 0), new Vector3(80, 0, 0)); SFX.Play(SFX.S.Cast, 0.35f); break;
            case Attack.Volley: w = 0.85f; SetPose(new Vector3(-120, 0, 30), new Vector3(-120, 0, -30), new Vector3(-15, 0, 0), new Vector3(0, 0, 0)); SFX.Play(SFX.S.Cast, 0.7f, 0.7f); break;
            case Attack.Nova: w = 0.75f; SetPose(new Vector3(-175, 0, 30), new Vector3(-175, 0, -30), new Vector3(-20, 0, 0), new Vector3(0, 0, 0)); break;
            case Attack.SpikeLine: w = 0.7f; SetPose(new Vector3(-160, 0, 0), new Vector3(-30, 0, -30), new Vector3(-15, 0, 0), new Vector3(0, 0, 0)); break;
            case Attack.Storm: w = 0.7f; SetPose(new Vector3(-175, 0, 20), new Vector3(-175, 0, -20), new Vector3(-25, 0, 0), new Vector3(0, 0, 0)); SFX.Play(SFX.S.Zap, 0.6f, 0.6f); break;
            default: SetPose(new Vector3(-160, 30, 0), new Vector3(10, 0, -30), new Vector3(-10, 30, 0), new Vector3(10, 0, 0)); break;
        }
        stateTimer = w;
    }

    void Strike(PlayerController p, float dist, Vector3 dirToPlayer, bool alive)
    {
        var A = GameAssets.I;
        switch (nextAttack)
        {
            case Attack.Swipe:
                SetPose(new Vector3(-30, -40, 0), new Vector3(20, 0, -20), new Vector3(20, -30, 0), new Vector3(60, 0, 0));
                SFX.Play(SFX.S.Swing, type == EnemyType.Boss ? 0.8f : 0.4f, type == EnemyType.Boss ? 0.6f : 1f);
                if (alive && dist <= range + 0.8f && Vector3.Angle(transform.forward, dirToPlayer) < 75f)
                    p.ReceiveAttack(transform.position, this, damage, IsLight ? 5f : 10f);
                state = State.Recover; stateTimer = type == EnemyType.Stalker ? 0.35f : 0.5f;
                break;

            case Attack.Slam:
            {
                SetPose(new Vector3(-25, 0, 0), new Vector3(-25, 0, 0), new Vector3(35, 0, 0), new Vector3(60, 0, 0));
                var c = transform.position + transform.forward * range * 0.7f;
                float radius = type == EnemyType.Boss ? 6f : 3.8f;
                var ringMat = type != EnemyType.Boss ? A.shockwave : bossKind == 2 ? A.lightning : A.bossGlow;
                FX.Ring(c, ringMat, radius, 0.4f);
                FX.Shards(c, A.shard, 14, 8f, 0.16f);
                FX.Shake(type == EnemyType.Boss ? 1.1f : 0.6f);
                SFX.Play(SFX.S.Boom, type == EnemyType.Boss ? 1f : 0.6f, type == EnemyType.Boss ? 0.8f : 1.1f);
                if (alive)
                {
                    var d = p.transform.position - c; d.y = 0;
                    if (d.magnitude <= radius && p.transform.position.y < transform.position.y + 1.2f)
                        p.ReceiveAttack(c, this, damage * 1.3f, 16f);
                }
                if (type == EnemyType.Boss && bossKind == 2 && phase2)
                    for (int i = 0; i < 7; i++)
                        Hazard.Spawn(c + Quaternion.Euler(0, i * 360f / 7f, 0) * Vector3.forward * 8f, 2f, 0.7f, damage, Hazard.Kind.Lightning, this);
                state = State.Recover; stateTimer = 0.9f;
                break;
            }

            case Attack.Charge:
                chargeDir = transform.forward;
                chargeHit = false;
                SFX.Play(SFX.S.Roar, 0.5f, 1.3f);
                state = State.Charging; stateTimer = 0.75f;
                break;

            case Attack.Bolt:
            {
                SetPose(new Vector3(-20, 0, 12), new Vector3(-80, 0, 0), new Vector3(10, 10, 0), new Vector3(80, 0, 0));
                var from = transform.position + Vector3.up * 1.6f * scale + transform.forward * 0.8f;
                var target = alive ? p.transform.position + Vector3.up * 1.1f : from + transform.forward;
                EnemyBolt.Fire(this, from, target - from, 15f, damage, boltMat, 1.1f, boltChill);
                SFX.Play(SFX.S.Cast, 0.5f, 1.4f);
                state = State.Recover; stateTimer = 0.5f;
                break;
            }

            case Attack.Volley:
            {
                SetPose(new Vector3(-80, 0, 20), new Vector3(-80, 0, -20), new Vector3(15, 0, 0), new Vector3(30, 0, 0));
                var from = transform.position + Vector3.up * 2.2f * scale * 0.6f + transform.forward * 1.5f;
                var target = alive ? p.transform.position + Vector3.up * 1.1f : from + transform.forward;
                int n = phase2 ? 5 : 3;
                for (int i = 0; i < n; i++)
                {
                    float ang = (i - (n - 1) * 0.5f) * 13f;
                    var dir = Quaternion.Euler(0, ang, 0) * (target - from);
                    EnemyBolt.Fire(this, from, dir, 17f, damage * 0.8f, A.frostGlow, 0.5f, true, 0.6f);
                }
                SFX.Play(SFX.S.Shatter, 0.6f, 1.3f);
                state = State.Recover; stateTimer = 0.6f;
                break;
            }

            case Attack.Nova:
                SetPose(new Vector3(-25, 0, 40), new Vector3(-25, 0, -40), new Vector3(30, 0, 0), new Vector3(60, 0, 0));
                FX.Ring(transform.position, A.frostGlow, 3f, 0.3f);
                for (int i = 0; i < 8; i++)
                    Hazard.Spawn(transform.position + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 5f, 1.9f, 0.65f, damage * 1.1f, Hazard.Kind.Ice, this);
                if (phase2)
                    for (int i = 0; i < 12; i++)
                        Hazard.Spawn(transform.position + Quaternion.Euler(0, i * 30f + 15f, 0) * Vector3.forward * 9f, 1.9f, 1.1f, damage * 1.1f, Hazard.Kind.Ice, this);
                SFX.Play(SFX.S.Cast, 0.8f, 0.5f);
                state = State.Recover; stateTimer = 1.0f;
                break;

            case Attack.SpikeLine:
            {
                SetPose(new Vector3(-20, 0, 0), new Vector3(-30, 0, -30), new Vector3(35, 0, 0), new Vector3(70, 0, 0));
                var dir = alive ? Flat(p.transform.position - transform.position) : transform.forward;
                for (int i = 0; i < 9; i++)
                    Hazard.Spawn(transform.position + dir * (3f + i * 2.3f), 1.5f, 0.45f + i * 0.09f, damage, Hazard.Kind.Ice, this);
                FX.Shake(0.4f);
                state = State.Recover; stateTimer = 0.9f;
                break;
            }

            case Attack.Storm:
            {
                SetPose(new Vector3(-150, 0, 30), new Vector3(-150, 0, -30), new Vector3(-10, 0, 0), new Vector3(0, 0, 0));
                LightningBolt.Create(rig.handR.position, rig.handR.position + Vector3.up * 30f, A.lightning, 0.3f, 0.3f);
                SFX.Play(SFX.S.Thunder, 0.6f, 0.9f);
                int n = phase2 ? 8 : 5;
                var center = alive ? p.transform.position : transform.position + transform.forward * 5f;
                for (int i = 0; i < n; i++)
                {
                    var off = i == 0 ? Vector3.zero : Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(2.5f, 7f);
                    Hazard.Spawn(center + off, 2.2f, 1.0f + i * 0.12f, damage * 1.1f, Hazard.Kind.Lightning, this);
                }
                state = State.Recover; stateTimer = 0.8f;
                break;
            }
        }
    }

    void Animate(float moveSpeed, float dt)
    {
        walkCycle += dt * (moveSpeed > 0.3f ? 4f + moveSpeed * 1.2f : 0f);
        float swing = moveSpeed > 0.3f ? Mathf.Sin(walkCycle) * 35f : 0f;
        rig.legL.localRotation = Quaternion.Slerp(rig.legL.localRotation, Quaternion.Euler(swing, 0, 0), dt * 12f);
        rig.legR.localRotation = Quaternion.Slerp(rig.legR.localRotation, Quaternion.Euler(-swing, 0, 0), dt * 12f);
        float rate = state == State.Recover ? 30f : (state == State.Windup ? 6f : 10f);
        float k = 1f - Mathf.Exp(-rate * dt);
        var armMod = state == State.Chase ? Quaternion.Euler(-swing * 0.6f, 0, 0) : Quaternion.identity;
        rig.armR.localRotation = Quaternion.Slerp(rig.armR.localRotation, armRT * armMod, k);
        rig.armL.localRotation = Quaternion.Slerp(rig.armL.localRotation, armLT * Quaternion.Inverse(armMod), k);
        rig.torso.localRotation = Quaternion.Slerp(rig.torso.localRotation, torsoT, k);
        rig.handR.localRotation = Quaternion.Slerp(rig.handR.localRotation, handRT, k);

        // Map tint, telegraph glow during windup, freeze tint, hit flash
        Color tint = type == EnemyType.Boss ? Color.white : mapTint;
        if (freezeTimer > 0f) tint = new Color(0.55f, 0.8f, 1.2f);
        if (state == State.Windup) tint = Color.Lerp(tint, new Color(1.6f, 0.5f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
        tint = Color.Lerp(tint, new Color(3f, 3f, 3f), flash);
        foreach (var r in rig.renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            var baseCol = r.sharedMaterial.color;
            mpb.SetColor("_Color", new Color(baseCol.r * tint.r, baseCol.g * tint.g, baseCol.b * tint.b, 1f));
            r.SetPropertyBlock(mpb);
        }
    }

    public void Freeze(float seconds) { freezeTimer = Mathf.Max(freezeTimer, seconds); }

    public void Stagger(float seconds, bool force)
    {
        if (state == State.Dead || state == State.Spawning) return;
        if (!force && !IsLight && poise < poiseMax) return;
        poise = 0f;
        state = State.Stagger;
        stateTimer = seconds;
    }

    void OnDamaged(DamageInfo info)
    {
        flash = 1f;
        var dir = transform.position - info.source; dir.y = 0;
        if (dir.sqrMagnitude > 0.001f) knock = dir.normalized * info.knockback * (1f - knockResist);
        FX.Shards(transform.position + Vector3.up * scale * 1.2f, GameAssets.I.shard, 4, 4f, 0.07f);

        poise += info.amount * (info.heavy ? 1.6f : 1f);
        if (IsLight) { if (info.heavy || staggerImmune <= 0f) Stagger(info.heavy ? 0.6f : 0.3f, true); }
        else if (poise >= poiseMax) Stagger(type == EnemyType.Boss ? 1.3f : 0.8f, true);

        if (type == EnemyType.Boss && !phase2 && health.Fraction <= 0.5f && !health.IsDead)
        {
            phase2 = true;
            GameManager.I?.ShowMessage(BossDefs.Enrage[bossKind], 2f);
            var mat = bossKind == 1 ? GameAssets.I.frostGlow : bossKind == 2 ? GameAssets.I.stormGlow : GameAssets.I.bossGlow;
            FX.Ring(transform.position, mat, 9f, 0.7f);
            FX.Shake(1.2f);
            SFX.Play(SFX.S.Roar, 1f, 0.8f);
            if (bossKind == 2) for (int i = 0; i < 4; i++) LightningBolt.Create(transform.position + Vector3.up * 30f + Random.insideUnitSphere * 6f, transform.position + Vector3.up * 3f, GameAssets.I.lightning, 0.5f, 0.4f);
            if (!summoned)
            {
                summoned = true;
                GameManager.I?.SpawnAdds(bossKind == 1 ? EnemyType.Stalker : bossKind == 2 ? EnemyType.Caster : EnemyType.Grunt, 3);
            }
        }
    }

    void OnDied()
    {
        state = State.Dead;
        All.Remove(this);
        cc.enabled = false;
        if (Player != null) Player.AddRage(type == EnemyType.Boss ? 0f : 10f);
        FX.Shards(transform.position + Vector3.up * scale, GameAssets.I.shard, type == EnemyType.Boss ? 40 : 14, 7f, 0.12f * scale);
        FX.Shake(type == EnemyType.Boss ? 1.5f : 0.3f);
        if (type == EnemyType.Boss)
        {
            FX.HitStop(0.3f);
            FX.SlowMo(2.2f, 0.25f);
            SFX.Play(SFX.S.Roar, 1f, 0.6f);
            SFX.Play(SFX.S.Boom, 1f, 0.5f);
        }
        float dropChance = type == EnemyType.Grunt ? 0.25f : type == EnemyType.Brute ? 0.75f : type == EnemyType.Boss ? 0f : 0.33f;
        if (Random.value < dropChance) HealthOrb.Spawn(transform.position + Vector3.up * 0.8f);
        GameManager.I?.OnEnemyKilled(this);
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        float t = 0f;
        var start = rig.visual.localRotation;
        var end = Quaternion.Euler(-88f, 0, 0);
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            rig.visual.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        yield return new WaitForSeconds(1.2f);
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            rig.visual.localPosition = Vector3.down * t * 1.5f * scale;
            yield return null;
        }
        Destroy(gameObject);
    }
}
