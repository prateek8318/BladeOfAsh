using UnityEngine;

// Static definitions for the original heroes, weapons and realms of Blade of Ash.

public enum UltimateKind { Rage, Tempest, MoltenAegis }

public class HeroDef
{
    public string name, title, ultName, blurb;
    public float maxHealth, runSpeed, sprintSpeed, dodgeCooldown, damageMul, attackSpeed, damageTaken;
    public UltimateKind ult;
    public Color accent;
    public float scale;
    public bool horns, helmet, bulky, ponytail, beard, bareChest, greatHelm;

    public static readonly HeroDef[] All =
    {
        new HeroDef
        {
            name = "KAEL", title = "Kael the Ashborn", ultName = "RAGE",
            blurb = "Last sworn blade of the burnt kingdom.\nBalanced fighter. RAGE: +80% damage, half damage taken, lifesteal.",
            maxHealth = 200, runSpeed = 6.5f, sprintSpeed = 10f, dodgeCooldown = 0.15f, damageMul = 1f, attackSpeed = 1f, damageTaken = 1f,
            ult = UltimateKind.Rage, accent = new Color(1f, 0.5f, 0.15f), scale = 1.2f, horns = true, helmet = true, beard = true,
        },
        new HeroDef
        {
            name = "YSRA", title = "Ysra Stormveil", ultName = "TEMPEST",
            blurb = "Exiled storm-warden who stole the sky's voice.\nFast and fragile. TEMPEST: lightning hunts every foe nearby.",
            maxHealth = 160, runSpeed = 7.6f, sprintSpeed = 11.5f, dodgeCooldown = 0.02f, damageMul = 0.95f, attackSpeed = 1.22f, damageTaken = 1f,
            ult = UltimateKind.Tempest, accent = new Color(0.45f, 0.85f, 1f), scale = 1.12f, ponytail = true,
        },
        new HeroDef
        {
            name = "GORRUN", title = "Gorrun Cinderwall", ultName = "MOLTEN AEGIS",
            blurb = "Forge-giant who carried a mountain's heart out of the fire.\nSlow tank. MOLTEN AEGIS: immovable, burning aura, 75% less damage.",
            maxHealth = 300, runSpeed = 5.6f, sprintSpeed = 8.6f, dodgeCooldown = 0.35f, damageMul = 1.2f, attackSpeed = 0.88f, damageTaken = 0.85f,
            ult = UltimateKind.MoltenAegis, accent = new Color(1f, 0.75f, 0.2f), scale = 1.34f, horns = true, helmet = true, bulky = true, beard = true, bareChest = true, greatHelm = true,
        },
    };
}

public enum WeaponKind { Ashbrand, Emberfangs, Thunderjaw }

public class WeaponDef
{
    public WeaponKind kind;
    public string name, light, heavy;
    public Color trailColor;

    public static readonly WeaponDef[] All =
    {
        new WeaponDef { kind = WeaponKind.Ashbrand,   name = "ASHBRAND",        light = "3-hit greatsword combo",  heavy = "Leaping ground slam",       trailColor = new Color(1f, 0.55f, 0.15f) },
        new WeaponDef { kind = WeaponKind.Emberfangs, name = "EMBERFANG TWINS", light = "5-hit blade flurry",      heavy = "Cyclone spin (move while spinning)", trailColor = new Color(1f, 0.25f, 0.1f) },
        new WeaponDef { kind = WeaponKind.Thunderjaw, name = "THUNDERJAW MAUL", light = "Crushing 2-hit swings",   heavy = "Skyfall slam + chain lightning", trailColor = new Color(0.5f, 0.8f, 1f) },
    };
}

public class MapDef
{
    public string name, subtitle;
    public EnemyType[][] waves;
    public string gruntName, bruteName;
    public Color enemyTint;
    public int bossKind;

    public static readonly MapDef[] All =
    {
        new MapDef
        {
            name = "CINDER COLOSSEUM", subtitle = "Where the burnt kingdom fed its champions to the fire",
            gruntName = "Ash Ghoul", bruteName = "Cinder Brute", enemyTint = Color.white, bossKind = 0,
            waves = new[]
            {
                W(EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Grunt, EnemyType.Grunt, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Caster, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Caster, EnemyType.Grunt, EnemyType.Stalker, EnemyType.Brute, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Brute, EnemyType.Caster, EnemyType.Caster, EnemyType.Grunt, EnemyType.Stalker, EnemyType.Brute, EnemyType.Grunt),
                W(EnemyType.Boss, EnemyType.Grunt, EnemyType.Grunt),
            }
        },
        new MapDef
        {
            name = "FROSTVEIL HOLLOW", subtitle = "A frozen grove where the dead forget how to sleep",
            gruntName = "Rime Thrall", bruteName = "Glacier Brute", enemyTint = new Color(0.75f, 0.9f, 1.25f), bossKind = 1,
            waves = new[]
            {
                W(EnemyType.Grunt, EnemyType.Grunt, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Caster, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Caster, EnemyType.Grunt, EnemyType.Stalker),
                W(EnemyType.Brute, EnemyType.Stalker, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Caster, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Brute, EnemyType.Caster, EnemyType.Caster, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Caster, EnemyType.Stalker, EnemyType.Brute, EnemyType.Caster, EnemyType.Stalker, EnemyType.Brute, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Boss, EnemyType.Stalker, EnemyType.Grunt),
            }
        },
        new MapDef
        {
            name = "STORMSPIRE SUMMIT", subtitle = "The mountain that tore a hole in the sky",
            gruntName = "Storm Husk", bruteName = "Thunder Brute", enemyTint = new Color(0.95f, 0.85f, 1.2f), bossKind = 2,
            waves = new[]
            {
                W(EnemyType.Grunt, EnemyType.Grunt, EnemyType.Caster, EnemyType.Grunt, EnemyType.Stalker, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Caster, EnemyType.Caster, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Brute, EnemyType.Stalker, EnemyType.Stalker, EnemyType.Caster, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Caster, EnemyType.Caster, EnemyType.Caster, EnemyType.Brute, EnemyType.Stalker, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Brute, EnemyType.Brute, EnemyType.Brute, EnemyType.Caster, EnemyType.Caster, EnemyType.Stalker, EnemyType.Stalker, EnemyType.Stalker, EnemyType.Grunt, EnemyType.Grunt),
                W(EnemyType.Boss, EnemyType.Caster, EnemyType.Grunt, EnemyType.Grunt),
            }
        },
    };

    static EnemyType[] W(params EnemyType[] e) => e;
}

public static class BossDefs
{
    public static readonly string[] Names = { "VORTHAK, THE HOLLOW KING", "SKALDRA, THE RIME MATRIARCH", "OZREK, THE THUNDER TYRANT" };
    public static readonly string[] Rise = { "THE HOLLOW KING RISES", "THE RIME MATRIARCH AWAKENS", "THE THUNDER TYRANT DESCENDS" };
    public static readonly string[] Enrage = { "THE HOLLOW KING ENRAGES!", "SKALDRA SHATTERS HER CROWN!", "OZREK CALLS THE STORM!" };
    public static readonly string[] Fallen = { "THE HOLLOW KING HAS FALLEN", "THE RIME MATRIARCH IS SHATTERED", "THE THUNDER TYRANT IS SILENCED" };
}
