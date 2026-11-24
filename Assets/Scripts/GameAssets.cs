using UnityEngine;

// Holds material references (assigned by the SceneBuilder editor script) so runtime-built
// characters, maps and effects use real material assets that ship with the build.
public class GameAssets : MonoBehaviour
{
    public static GameAssets I;

    [Header("Hero - Kael")]
    public Material heroSkin;
    public Material heroArmor;
    public Material heroCape;
    public Material heroEmber;

    [Header("Hero - Ysra")]
    public Material ysraSkin;
    public Material ysraArmor;
    public Material ysraCape;
    public Material ysraGlow;

    [Header("Hero - Gorrun")]
    public Material gorrunSkin;
    public Material gorrunArmor;
    public Material gorrunCape;
    public Material gorrunGlow;

    [Header("Hero details")]
    public Material kaelHair;
    public Material ysraHair;
    public Material gorrunHair;
    public Material leather;
    public Material gold;

    [Header("Weapons")]
    public Material blade;
    public Material spear;
    public Material fang;
    public Material maulHead;
    public Material lightning;
    public Material trail;

    [Header("Enemies")]
    public Material gruntSkin;
    public Material gruntArmor;
    public Material bruteSkin;
    public Material bruteArmor;
    public Material casterRobe;
    public Material casterGlow;
    public Material stalkerSkin;
    public Material bossSkin;
    public Material bossArmor;
    public Material bossGlow;
    public Material frostBossArmor;
    public Material frostGlow;
    public Material stormBossArmor;
    public Material stormGlow;
    public Material enemyEye;
    public Material bolt;
    public Material telegraph;

    [Header("FX")]
    public Material shard;
    public Material iceShard;
    public Material spawnRing;
    public Material shockwave;
    public Material healOrb;
    public Material rageAura;

    [Header("World")]
    public Material stone, darkStone, arenaFloor, ashGround, rune, fire, iron;
    public Material snow, ice, frostStone, crystal, pineLeaf, bark;
    public Material obsidian, stormStone, stormRune, cloud;

    void Awake() { I = this; }
}
