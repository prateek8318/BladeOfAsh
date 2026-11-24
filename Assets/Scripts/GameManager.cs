using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    // Survive scene reloads (retry / next realm).
    public static int SelectedHero;
    public static int SelectedMap;
    static bool skipTitle;
    static int carriedScore, carriedKills;
    static float carriedTime;

    enum Phase { Title, Countdown, Fighting, Victory, GameOver }
    Phase phase = Phase.Title;

    public float arenaSpawnRadius = 17f;

    MapDef Map => MapDef.All[SelectedMap];
    EnemyType[][] Waves => Map.waves;

    int waveIndex = -1;
    int score;
    int kills;
    float messageTimer;
    string message;
    float weaponMsgTimer;
    string weaponMsg;
    float countdown;
    float helpTimer = 10f;
    Enemy boss;
    float runTime;
    readonly Queue<EnemyType> pending = new Queue<EnemyType>();
    float spawnTimer;
    int startWave;

    GUIStyle big, mid, small, center, tiny, cardTitle;

    public bool InputEnabled => phase == Phase.Fighting || phase == Phase.Countdown;
    public bool IsTitle => phase == Phase.Title;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        Enemy.All.Clear();
        if (GameAssets.I == null) GameAssets.I = GetComponent<GameAssets>();

        if (AutoTest.Enabled)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-hero") int.TryParse(args[i + 1], out SelectedHero);
                if (args[i] == "-map") int.TryParse(args[i + 1], out SelectedMap);
                if (args[i] == "-wave") { int.TryParse(args[i + 1], out startWave); startWave = Mathf.Max(0, startWave - 1); }
            }
            SelectedHero = Mathf.Clamp(SelectedHero, 0, HeroDef.All.Length - 1);
            SelectedMap = Mathf.Clamp(SelectedMap, 0, MapDef.All.Length - 1);
            gameObject.AddComponent<AutoTest>();
            skipTitle = System.Array.IndexOf(args, "-title") < 0;
        }

        MapBuilder.Build(SelectedMap);

        if (skipTitle)
        {
            skipTitle = false;
            score = carriedScore; kills = carriedKills; runTime = carriedTime;
            BeginRun();
        }
        else
        {
            carriedScore = carriedKills = 0; carriedTime = 0f;
        }
    }

    void BeginRun()
    {
        phase = Phase.Countdown;
        waveIndex = startWave - 1;
        NextWave();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ShowMessage(Map.name, 2.4f);
    }

    void Update()
    {
        messageTimer -= Time.unscaledDeltaTime;
        weaponMsgTimer -= Time.unscaledDeltaTime;
        if (helpTimer > 0f && phase != Phase.Title) helpTimer -= Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.H)) helpTimer = helpTimer > 0f ? 0f : 9999f;

        switch (phase)
        {
            case Phase.Title:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PickHero((SelectedHero + HeroDef.All.Length - 1) % HeroDef.All.Length);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) PickHero((SelectedHero + 1) % HeroDef.All.Length);
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) PickMap((SelectedMap + MapDef.All.Length - 1) % MapDef.All.Length);
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) PickMap((SelectedMap + 1) % MapDef.All.Length);
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) BeginRun();
                break;
            case Phase.Countdown:
                runTime += Time.deltaTime;
                countdown -= Time.deltaTime;
                if (countdown <= 0f)
                {
                    phase = Phase.Fighting;
                    foreach (var t in Waves[waveIndex]) pending.Enqueue(t);
                    ShowMessage(waveIndex == Waves.Length - 1 ? BossDefs.Rise[Map.bossKind] : $"WAVE {waveIndex + 1}", 1.6f);
                }
                break;
            case Phase.Fighting:
                runTime += Time.deltaTime;
                spawnTimer -= Time.deltaTime;
                int cap = waveIndex >= 3 ? 6 : 5;
                if (pending.Count > 0 && spawnTimer <= 0f && Enemy.All.Count < cap)
                {
                    var e = Spawn(pending.Dequeue(), RandomSpawnPoint());
                    if (e.type == EnemyType.Boss) boss = e;
                    spawnTimer = 0.55f;
                }
                if (pending.Count == 0 && Enemy.All.Count == 0)
                {
                    if (waveIndex >= Waves.Length - 1) { phase = Phase.Victory; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                    else { phase = Phase.Countdown; NextWave(); }
                }
                break;
            case Phase.Victory:
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    if (SelectedMap < MapDef.All.Length - 1) { SelectedMap++; Carry(true); }
                    else { SelectedMap = 0; Carry(false); }
                    Reload();
                }
                if (Input.GetKeyDown(KeyCode.M)) { Carry(false); Reload(); }
                break;
            case Phase.GameOver:
                if (Input.GetKeyDown(KeyCode.Return)) { Carry(true); carriedScore = 0; carriedKills = 0; carriedTime = 0; Reload(); }
                if (Input.GetKeyDown(KeyCode.M)) { Carry(false); Reload(); }
                break;
        }
    }

    void Carry(bool next)
    {
        skipTitle = next;
        carriedScore = next ? score : 0;
        carriedKills = next ? kills : 0;
        carriedTime = next ? runTime : 0f;
    }

    void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    void PickHero(int i)
    {
        if (i == SelectedHero) return;
        SelectedHero = i;
        PlayerController.I?.ApplyHero(i);
        SFX.Play(SFX.S.Throw, 0.5f, 1.3f);
    }

    void PickMap(int i)
    {
        if (i == SelectedMap) return;
        SelectedMap = i;
        MapBuilder.Build(i);
        SFX.Play(SFX.S.Spawn, 0.5f);
    }

    void NextWave()
    {
        waveIndex++;
        countdown = waveIndex == 0 ? 2.5f : 4f;
        if (waveIndex > 0 && waveIndex != startWave)
        {
            ShowMessage("WAVE CLEARED", 2f);
            PlayerController.I?.health.Heal(40f);
        }
    }

    Vector3 RandomSpawnPoint()
    {
        var p = PlayerController.I != null ? PlayerController.I.transform.position : Vector3.zero;
        for (int i = 0; i < 10; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            var pos = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * arenaSpawnRadius * Random.Range(0.6f, 1f);
            if (Vector3.Distance(pos, p) > 7f) return pos;
        }
        return -p.normalized * arenaSpawnRadius;
    }

    public Enemy Spawn(EnemyType t, Vector3 pos)
    {
        var go = new GameObject(t.ToString());
        go.transform.position = pos + Vector3.up * 0.1f;
        var toCenter = -pos; toCenter.y = 0;
        if (toCenter.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(toCenter);
        go.AddComponent<CharacterController>();
        go.AddComponent<Health>();
        var e = go.AddComponent<Enemy>();
        e.Init(t, SelectedMap);
        return e;
    }

    public void SpawnAdds(EnemyType t, int n)
    {
        for (int i = 0; i < n; i++) pending.Enqueue(t);
    }

    public void OnEnemyKilled(Enemy e)
    {
        kills++;
        int baseScore = e.type switch
        {
            EnemyType.Grunt => 100, EnemyType.Brute => 350, EnemyType.Caster => 200, EnemyType.Stalker => 250, _ => 5000
        };
        int combo = PlayerController.I != null ? PlayerController.I.Combo : 0;
        score += Mathf.RoundToInt(baseScore * (1f + combo * 0.1f));
        if (e == boss)
        {
            ShowMessage(BossDefs.Fallen[e.bossKind], 3f);
            pending.Clear();
            foreach (var other in Enemy.All.ToArray())
                other.health.TakeDamage(new DamageInfo { amount = 9999, source = e.transform.position, knockback = 10, fromPlayer = true });
        }
    }

    public void OnPlayerDied()
    {
        phase = Phase.GameOver;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowMessage(string text, float seconds)
    {
        message = text;
        messageTimer = seconds;
    }

    public void ShowWeapon(string text)
    {
        weaponMsg = text;
        weaponMsgTimer = 1.2f;
    }

    // ---------------------------------------------------------------- HUD
    void Styles()
    {
        if (big != null) return;
        big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        mid = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperLeft, richText = true };
        center = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        tiny = new GUIStyle(small) { fontSize = 14, wordWrap = true, alignment = TextAnchor.UpperCenter };
        cardTitle = new GUIStyle(mid) { fontSize = 22 };
    }

    static void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Frame(Rect r, Color c, float t)
    {
        Box(new Rect(r.x, r.y, r.width, t), c);
        Box(new Rect(r.x, r.yMax - t, r.width, t), c);
        Box(new Rect(r.x, r.y, t, r.height), c);
        Box(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static void Bar(Rect r, float frac, Color fill, Color back)
    {
        Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0, 0, 0, 0.75f));
        Box(r, back);
        Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fill);
    }

    static void Shadowed(Rect r, string text, GUIStyle s, Color c)
    {
        GUI.color = new Color(0, 0, 0, c.a * 0.85f);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
        GUI.color = c;
        GUI.Label(r, text, s);
        GUI.color = Color.white;
    }

    static bool Clicked(Rect r)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { e.Use(); return true; }
        return false;
    }

    void OnGUI()
    {
        Styles();
        float w = Screen.width, h = Screen.height;
        float ui = Mathf.Clamp(Mathf.Min(h / 900f, w / 1500f), 0.6f, 1.6f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1));
        w /= ui; h /= ui;

        if (phase == Phase.Title) { DrawTitle(w, h); return; }

        var p = PlayerController.I;
        if (p != null)
        {
            var hero = p.Hero;
            float x = 30, y = h - 120;
            Shadowed(new Rect(x, y - 30, 400, 28), hero.title.ToUpper(), new GUIStyle(small) { fontSize = 20, fontStyle = FontStyle.Bold }, hero.accent);
            Bar(new Rect(x, y, 380, 22), p.health.Fraction, new Color(0.85f, 0.12f, 0.1f), new Color(0.2f, 0.05f, 0.05f));
            Shadowed(new Rect(x + 390, y - 2, 120, 26), $"{Mathf.CeilToInt(p.health.current)}", small, Color.white);
            bool full = p.rage >= p.maxRage && !p.UltActive;
            var a = hero.accent;
            var ultCol = p.UltActive ? Color.Lerp(a, Color.white, Mathf.PingPong(Time.time * 4f, 0.6f))
                                     : full ? Color.Lerp(a, Color.yellow, Mathf.PingPong(Time.time * 3f, 1f)) : a;
            Bar(new Rect(x, y + 32, 300, 14), p.rage / p.maxRage, ultCol, new Color(a.r * 0.2f, a.g * 0.2f, a.b * 0.2f));
            Shadowed(new Rect(x + 310, y + 26, 340, 26), p.UltActive ? $"{hero.ultName} ACTIVE" : full ? $"{hero.ultName} READY — press R" : hero.ultName, small, ultCol);
            Shadowed(new Rect(x, y + 56, 400, 26), p.SpearInHand ? "Galeshard Javelin: ready  [E] throw" : "Galeshard Javelin: thrown  [E] recall", small, new Color(0.5f, 0.85f, 1f));
            if (p.Chilled) Shadowed(new Rect(x, y + 80, 300, 26), "<b>CHILLED</b> — slowed", small, new Color(0.6f, 0.9f, 1f));

            // Weapon slots
            float sw = 172, sx = w - 3 * (sw + 8) - 22, sy = h - 74;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(sx + i * (sw + 8), sy, sw, 50);
                bool sel = (int)p.Weapon == i;
                var wc = WeaponDef.All[i].trailColor;
                Box(r, new Color(0, 0, 0, sel ? 0.75f : 0.45f));
                if (sel) Frame(r, wc, 3);
                Shadowed(new Rect(r.x + 8, r.y + 4, 30, 22), $"<b>{i + 1}</b>", small, sel ? wc : new Color(1, 1, 1, 0.5f));
                Shadowed(new Rect(r.x, r.y + 14, r.width, 30), WeaponDef.All[i].name, new GUIStyle(center) { fontSize = 13, fontStyle = FontStyle.Bold }, sel ? Color.white : new Color(1, 1, 1, 0.5f));
            }
            Shadowed(new Rect(sx, sy - 24, 3 * (sw + 8), 22), "Q / 1-2-3  switch weapon", new GUIStyle(center) { fontSize = 13 }, new Color(1, 1, 1, 0.5f));

            if (p.Combo >= 2)
            {
                float pulse = 1f + Mathf.Clamp01(p.Combo / 30f) * 0.3f;
                var cs = new GUIStyle(big) { fontSize = Mathf.RoundToInt(48 * pulse) };
                Shadowed(new Rect(w - 330, h * 0.38f, 300, 70), $"{p.Combo}", cs, new Color(1f, 0.75f, 0.2f));
                Shadowed(new Rect(w - 330, h * 0.38f + 60, 300, 30), p.Combo >= 25 ? "ASHEN FURY" : p.Combo >= 12 ? "SAVAGE" : "HIT COMBO", mid, Color.white);
            }
        }

        // Wave / score
        Shadowed(new Rect(w - 330, 22, 300, 30), $"SCORE  {score:N0}", new GUIStyle(mid) { alignment = TextAnchor.MiddleRight }, Color.white);
        Shadowed(new Rect(w - 430, 54, 400, 26), $"{Map.name}  ·  Wave {Mathf.Max(1, waveIndex + 1)} / {Waves.Length}  ·  Kills {kills}", new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.8f));

        // Boss bar
        if (boss != null && !boss.health.IsDead)
        {
            float bw = Mathf.Min(760f, w * 0.6f);
            Color bc = boss.bossKind == 1 ? new Color(0.4f, 0.8f, 1f) : boss.bossKind == 2 ? new Color(0.6f, 0.6f, 1f) : new Color(0.75f, 0.4f, 1f);
            Shadowed(new Rect(0, 20, w, 30), boss.displayName, mid, bc);
            Bar(new Rect((w - bw) * 0.5f, 56, bw, 20), boss.health.Fraction, boss.Phase2 ? Color.Lerp(bc, Color.red, 0.5f) : bc, new Color(0.08f, 0.04f, 0.12f));
        }

        if (phase == Phase.Countdown && countdown < 3.2f)
            Shadowed(new Rect(0, h * 0.2f, w, 80), waveIndex == Waves.Length - 1 ? $"Something stirs... {Mathf.CeilToInt(countdown)}" : $"Next wave in {Mathf.CeilToInt(countdown)}", mid, new Color(1, 1, 1, 0.9f));

        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
        {
            float al = Mathf.Clamp01(messageTimer * 2f);
            Shadowed(new Rect(0, h * 0.28f, w, 90), message, big, new Color(1f, 0.6f, 0.25f, al));
        }
        if (weaponMsgTimer > 0f && p != null)
        {
            var wc = WeaponDef.All[(int)p.Weapon].trailColor;
            Shadowed(new Rect(0, h * 0.62f, w, 40), weaponMsg, mid, new Color(wc.r, wc.g, wc.b, Mathf.Clamp01(weaponMsgTimer * 2f)));
        }

        if (helpTimer > 0f) DrawHelp(30, 110);
        else Shadowed(new Rect(30, 20, 300, 24), "[H] controls", small, new Color(1, 1, 1, 0.5f));

        if (phase == Phase.GameOver || phase == Phase.Victory)
        {
            Box(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.6f));
            bool win = phase == Phase.Victory;
            bool last = SelectedMap >= MapDef.All.Length - 1;
            string head = win ? (last ? "CAMPAIGN COMPLETE" : "REALM CONQUERED") : "YOU HAVE FALLEN";
            Shadowed(new Rect(0, h * 0.3f, w, 90), head, big, win ? new Color(1f, 0.8f, 0.3f) : new Color(0.9f, 0.15f, 0.1f));
            int mins = Mathf.FloorToInt(runTime / 60f), secs = Mathf.FloorToInt(runTime % 60f);
            Shadowed(new Rect(0, h * 0.3f + 90, w, 40), $"Score {score:N0}   ·   Kills {kills}   ·   Time {mins}:{secs:00}", mid, Color.white);
            string next = win ? (last ? "ENTER  play again from the first realm" : $"ENTER  journey on to {MapDef.All[SelectedMap + 1].name}") : "ENTER  retry this realm";
            Shadowed(new Rect(0, h * 0.3f + 150, w, 40), next, mid, new Color(1, 1, 1, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f)));
            Shadowed(new Rect(0, h * 0.3f + 195, w, 30), "M  main menu", center, new Color(1, 1, 1, 0.6f));
        }
    }

    void DrawTitle(float w, float h)
    {
        Box(new Rect(0, 0, w, h * 0.2f), new Color(0, 0, 0, 0.45f));
        Box(new Rect(0, h * 0.45f, w, h * 0.55f), new Color(0, 0, 0, 0.5f));
        Shadowed(new Rect(0, h * 0.03f, w, 90), "BLADE OF ASH", new GUIStyle(big) { fontSize = 72 }, new Color(1f, 0.55f, 0.2f));
        Shadowed(new Rect(0, h * 0.03f + 78, w, 30), "Three heroes  ·  Three weapons  ·  Three realms  ·  Three tyrants", center, new Color(1, 1, 1, 0.8f));

        // Hero cards
        float cw = Mathf.Min(330f, (w - 120f) / 3f), ch = 150f, gap = 18f;
        float cx = (w - (cw * 3 + gap * 2)) * 0.5f, cy = h * 0.47f;
        Shadowed(new Rect(0, cy - 34, w, 30), "CHOOSE YOUR HERO   ( A / D )", center, Color.white);
        for (int i = 0; i < HeroDef.All.Length; i++)
        {
            var d = HeroDef.All[i];
            var r = new Rect(cx + i * (cw + gap), cy, cw, ch);
            bool sel = i == SelectedHero;
            Box(r, new Color(0.05f, 0.04f, 0.04f, sel ? 0.9f : 0.6f));
            if (sel) Frame(r, d.accent, 3);
            Shadowed(new Rect(r.x, r.y + 8, r.width, 28), d.title.ToUpper(), cardTitle, sel ? d.accent : new Color(1, 1, 1, 0.7f));
            Shadowed(new Rect(r.x, r.y + 38, r.width, 22), $"HP {d.maxHealth}   ·   SPD {d.runSpeed:0.0}   ·   ULT: {d.ultName}", new GUIStyle(center) { fontSize = 14 }, new Color(1, 1, 1, 0.85f));
            GUI.color = new Color(1, 1, 1, sel ? 0.95f : 0.6f);
            GUI.Label(new Rect(r.x + 12, r.y + 66, r.width - 24, ch - 70), d.blurb, tiny);
            GUI.color = Color.white;
            if (Clicked(r)) PickHero(i);
        }

        // Realm cards
        float my = cy + ch + 52;
        Shadowed(new Rect(0, my - 34, w, 30), "CHOOSE YOUR REALM   ( W / S )", center, Color.white);
        for (int i = 0; i < MapDef.All.Length; i++)
        {
            var m = MapDef.All[i];
            var r = new Rect(cx + i * (cw + gap), my, cw, 96);
            bool sel = i == SelectedMap;
            Color mc = i == 1 ? new Color(0.5f, 0.85f, 1f) : i == 2 ? new Color(0.7f, 0.6f, 1f) : new Color(1f, 0.5f, 0.2f);
            Box(r, new Color(0.05f, 0.04f, 0.04f, sel ? 0.9f : 0.6f));
            if (sel) Frame(r, mc, 3);
            Shadowed(new Rect(r.x, r.y + 6, r.width, 26), $"{i + 1}. {m.name}", cardTitle, sel ? mc : new Color(1, 1, 1, 0.7f));
            GUI.color = new Color(1, 1, 1, sel ? 0.9f : 0.55f);
            GUI.Label(new Rect(r.x + 10, r.y + 34, r.width - 20, 36), m.subtitle, tiny);
            GUI.color = Color.white;
            Shadowed(new Rect(r.x, r.y + 70, r.width, 22), "Boss: " + BossDefs.Names[m.bossKind].Split(',')[0], new GUIStyle(center) { fontSize = 14 }, sel ? mc : new Color(1, 1, 1, 0.5f));
            if (Clicked(r)) PickMap(i);
        }

        // Weapons + start
        float wy = my + 112;
        Shadowed(new Rect(0, wy, w, 24), "ARSENAL:  1 ASHBRAND greatsword   ·   2 EMBERFANG TWINS   ·   3 THUNDERJAW MAUL   ·   E  Galeshard Javelin", new GUIStyle(center) { fontSize = 15 }, new Color(1f, 0.85f, 0.6f));
        var btn = new Rect(w * 0.5f - 170, wy + 34, 340, 50);
        Box(btn, new Color(0.6f, 0.2f, 0.05f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f)));
        Frame(btn, new Color(1f, 0.6f, 0.2f), 2);
        Shadowed(btn, "FIGHT  ( ENTER )", mid, Color.white);
        if (Clicked(btn)) BeginRun();
    }

    void DrawHelp(float x, float y)
    {
        Box(new Rect(x - 12, y - 10, 470, 290), new Color(0, 0, 0, 0.55f));
        GUI.Label(new Rect(x, y, 450, 280),
            "<b>CONTROLS</b>\n" +
            "WASD  move     Shift  sprint     Mouse  camera\n" +
            "Left click  light combo   ·   Right click  heavy\n" +
            "1 / 2 / 3 or Q  switch weapon\n" +
            "   Ashbrand: slam  ·  Emberfangs: cyclone  ·  Thunderjaw: skyfall\n" +
            "Space  dodge roll (roll at the last moment = slow-mo)\n" +
            "F (hold)  block   ·   tap F right before a hit = PARRY\n" +
            "   (parry a magic bolt to send it back!)\n" +
            "E  throw javelin (freezes enemies)  ·  E again = recall\n" +
            "R  unleash your ULTIMATE when the bar is full\n" +
            "Esc  free mouse    H  toggle this help", small);
    }
}
