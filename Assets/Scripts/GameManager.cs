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
    static bool storyMode;
    static bool pendingChapterIntro;
    static int carriedScore, carriedKills;
    static float carriedTime;

    enum Phase { MainMenu, Select, Story, Tutorial, Countdown, Fighting, Victory, GameOver }
    Phase phase = Phase.MainMenu;
    bool paused;

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
    float helpTimer = 0f;
    Enemy boss;
    float runTime;
    readonly Queue<EnemyType> pending = new Queue<EnemyType>();
    float spawnTimer;
    int startWave;
    float victoryTime;

    // Menus
    enum Page { Main, Controls, Credits, WorldMap }
    Page page = Page.Main;
    int menuIndex;
    int pauseIndex;
    bool pauseControls;

    // Story / cutscenes
    StoryPage[] pages;
    int pageIndex;
    float pageTime;
    System.Action onStoryDone;
    bool endingMusic;
    bool rollingCredits;
    float creditsT;

    const string SaveChapter = "boa_story_chapter", SaveHero = "boa_story_hero";
    static int SavedChapter => PlayerPrefs.GetInt(SaveChapter, 0);

    GUIStyle big, mid, small, center, tiny, cardTitle, menuItem, storyBody, storyHead;
    Texture2D worldMap, disc, ring, arrow, gradL, gradV;

    public bool InputEnabled => !paused && !cineActive && (phase == Phase.Fighting || phase == Phase.Countdown || phase == Phase.Tutorial);
    public bool CineActive => cineActive;

    // ---- Cinematics
    bool cineActive, cineSkippable, cineLetterbox;
    string cineTitle, cineSub;
    float cineT, cineDur;
    Coroutine cineCo;
    float deathT;

    // ---- Tutorial
    static bool tutorialThenStory;
    int tutStep;
    float tutDoneT = -1f, tutCompleteT = -1f;
    float tutMoved, tutLooked; Vector3 tutLastPos; float tutLastYaw;
    int[] tutBase = new int[10];
    Enemy dummy;
    float dummyRespawn = -1f;
    public bool IsTitle => phase == Phase.MainMenu || phase == Phase.Select || phase == Phase.Story;
    public bool IsMainMenu => phase == Phase.MainMenu;
    // Camera framing for the hero showcase: on the main menu the hero stands to the right of the menu.
    public float TitleShoulder => phase == Phase.MainMenu && page == Page.Main ? -1.7f : 0f;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        Enemy.All.Clear();
        if (GameAssets.I == null) GameAssets.I = GetComponent<GameAssets>();

        string debugScreen = null;
        if (AutoTest.Enabled)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-hero") int.TryParse(args[i + 1], out SelectedHero);
                if (args[i] == "-map") int.TryParse(args[i + 1], out SelectedMap);
                if (args[i] == "-wave") { int.TryParse(args[i + 1], out startWave); startWave = Mathf.Max(0, startWave - 1); }
                if (args[i] == "-screen") debugScreen = args[i + 1];
            }
            SelectedHero = Mathf.Clamp(SelectedHero, 0, HeroDef.All.Length - 1);
            SelectedMap = Mathf.Clamp(SelectedMap, 0, MapDef.All.Length - 1);
            if (GetComponent<AutoTest>() == null) gameObject.AddComponent<AutoTest>();
            skipTitle = debugScreen == null && System.Array.IndexOf(args, "-title") < 0;
        }

        MapBuilder.Build(SelectedMap);

        if (skipTitle)
        {
            skipTitle = false;
            score = carriedScore; kills = carriedKills; runTime = carriedTime;
            if (pendingChapterIntro)
            {
                pendingChapterIntro = false;
                StartStory(new[] { Story.ChapterIntro(SelectedMap) }, BeginRun);
            }
            else BeginRun();
        }
        else
        {
            carriedScore = carriedKills = 0; carriedTime = 0f;
        }
        if (debugScreen != null) DebugScreen(debugScreen);
    }

    // Used by AutoTest to screenshot menus and cutscenes:  -screen menu|select|prologue|chapter|outro|ending|credits|map|controls|pause
    void DebugScreen(string s)
    {
        int pg = 0;
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-page") int.TryParse(args[i + 1], out pg);
        switch (s)
        {
            case "select": phase = Phase.Select; break;
            case "storyselect": storyMode = true; phase = Phase.Select; break;
            case "prologue": StartStory(Story.Prologue(SelectedHero), null); break;
            case "chapter": StartStory(new[] { Story.ChapterIntro(SelectedMap) }, null); break;
            case "outro": StartStory(new[] { Story.ChapterOutro(SelectedMap) }, null); break;
            case "ending": StartStory(Story.Ending(SelectedHero), null); endingMusic = true; break;
            case "credits": StartStory(new StoryPage[0], null); endingMusic = true; rollingCredits = true; creditsT = 8f; break;
            case "map": page = Page.WorldMap; break;
            case "controls": page = Page.Controls; break;
            case "pause": BeginRun(); SetPaused(true); break;
            case "hud": BeginRun(); break;
            case "tutorial": StartTutorial(false); break;
            case "tutorial5": StartTutorial(false); for (int k = 0; k < 9; k++) NextTutStep(); break;
        }
        if (pages != null) pageIndex = Mathf.Clamp(pg, 0, Mathf.Max(0, pages.Length - 1));
        pageTime = 30f;
    }

    void BeginRun()
    {
        phase = Phase.Countdown;
        waveIndex = startWave - 1;
        NextWave();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (!AutoTest.Enabled || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-cine") >= 0) CineRealmIntro();
        else ShowMessage(Map.name, 2.4f);
    }

    void StartStory(StoryPage[] p, System.Action done)
    {
        phase = Phase.Story;
        pages = p; pageIndex = 0; pageTime = 0f;
        onStoryDone = done;
        rollingCredits = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void AdvanceStory(bool skipAll)
    {
        if (rollingCredits) { BackToMenu(); return; }
        // first press finishes the typewriter, second press turns the page
        if (!skipAll && pageIndex < pages.Length && pageTime < pages[pageIndex].body.Length / 55f) { pageTime = 99f; return; }
        pageIndex = skipAll ? pages.Length : pageIndex + 1;
        pageTime = 0f;
        SFX.Play(SFX.S.Throw, 0.25f, 0.7f);
        if (pageIndex >= pages.Length)
        {
            var done = onStoryDone; onStoryDone = null;
            done?.Invoke();
        }
    }

    void Update()
    {
        messageTimer -= Time.unscaledDeltaTime;
        weaponMsgTimer -= Time.unscaledDeltaTime;
        pageTime += Time.unscaledDeltaTime;
        if (helpTimer > 0f && phase != Phase.MainMenu && phase != Phase.Select && !paused) helpTimer -= Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.H) && InputEnabled) helpTimer = helpTimer > 0f ? 0f : 9999f;

        UpdateMusic();

        if (paused) { UpdatePause(); return; }
        if (cineActive)
        {
            cineT += Time.unscaledDeltaTime;
            if (cineSkippable && cineT > 0.4f && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))) EndCine();
            if (phase != Phase.GameOver) return;
        }

        bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool ok = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        bool back = Input.GetKeyDown(KeyCode.Escape);

        switch (phase)
        {
            case Phase.MainMenu:
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (page != Page.Main) { if (back || ok) { page = Page.Main; SFX.Play(SFX.S.Throw, 0.3f, 0.8f); } break; }
                var items = MenuItems();
                if (up) { menuIndex = (menuIndex + items.Length - 1) % items.Length; SFX.Play(SFX.S.Throw, 0.25f, 1.6f); }
                if (down) { menuIndex = (menuIndex + 1) % items.Length; SFX.Play(SFX.S.Throw, 0.25f, 1.6f); }
                if (ok) ChooseMenu(items[Mathf.Clamp(menuIndex, 0, items.Length - 1)]);
                break;
            case Phase.Select:
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                if (back) { phase = Phase.MainMenu; break; }
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PickHero((SelectedHero + HeroDef.All.Length - 1) % HeroDef.All.Length);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) PickHero((SelectedHero + 1) % HeroDef.All.Length);
                if (!storyMode && up) PickMap((SelectedMap + MapDef.All.Length - 1) % MapDef.All.Length);
                if (!storyMode && down) PickMap((SelectedMap + 1) % MapDef.All.Length);
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmSelect();
                break;
            case Phase.Story:
                if (rollingCredits)
                {
                    creditsT += Time.unscaledDeltaTime;
                    if (ok || back || creditsT > Story.Credits.Length * 1.5f + 14f) BackToMenu();
                }
                else if (ok || Input.GetMouseButtonDown(0)) AdvanceStory(false);
                else if (back) AdvanceStory(true);
                break;
            case Phase.Tutorial:
                if (back) { SetPaused(true); break; }
                UpdateTutorial();
                break;
            case Phase.Countdown:
                if (back) { SetPaused(true); break; }
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
                if (back) { SetPaused(true); break; }
                runTime += Time.deltaTime;
                spawnTimer -= Time.deltaTime;
                int cap = waveIndex >= 3 ? 6 : 5;
                if (pending.Count > 0 && spawnTimer <= 0f && Enemy.All.Count < cap)
                {
                    var e = Spawn(pending.Dequeue(), RandomSpawnPoint());
                    if (e.type == EnemyType.Boss) { boss = e; CineBossIntro(e); }
                    spawnTimer = 0.55f;
                }
                if (pending.Count == 0 && Enemy.All.Count == 0)
                {
                    if (waveIndex >= Waves.Length - 1) { phase = Phase.Victory; victoryTime = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                    else { phase = Phase.Countdown; NextWave(); }
                }
                break;
            case Phase.Victory:
                victoryTime += Time.unscaledDeltaTime;
                if (storyMode)
                {
                    if (victoryTime > 3f) StoryAfterVictory();
                    break;
                }
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    if (SelectedMap < MapDef.All.Length - 1) { SelectedMap++; Carry(true); }
                    else { SelectedMap = 0; Carry(false); }
                    Reload();
                }
                if (Input.GetKeyDown(KeyCode.M)) BackToMenu();
                break;
            case Phase.GameOver:
                deathT += Time.unscaledDeltaTime;
                if (deathT < 2.5f) break;
                if (Input.GetKeyDown(KeyCode.Return)) Retry();
                if (Input.GetKeyDown(KeyCode.M)) BackToMenu();
                break;
        }
    }

    // ---------------------------------------------------------------- flow
    string[] MenuItems()
    {
        var l = new List<string>();
        if (SavedChapter > 0 && SavedChapter < MapDef.All.Length) l.Add("CONTINUE STORY");
        l.Add("NEW STORY"); l.Add("TRAINING"); l.Add("FREE BATTLE"); l.Add("WORLD MAP"); l.Add("CONTROLS"); l.Add("CREDITS"); l.Add("QUIT");
        return l.ToArray();
    }

    void ChooseMenu(string item)
    {
        SFX.Play(SFX.S.SwingHeavy, 0.5f, 0.9f);
        switch (item)
        {
            case "CONTINUE STORY":
                storyMode = true;
                SelectedHero = Mathf.Clamp(PlayerPrefs.GetInt(SaveHero, 0), 0, HeroDef.All.Length - 1);
                PlayerController.I?.ApplyHero(SelectedHero);
                PickMap(SavedChapter);
                StartStory(new[] { Story.ChapterIntro(SelectedMap) }, BeginRun);
                break;
            case "NEW STORY": storyMode = true; PickMap(0); phase = Phase.Select; break;
            case "FREE BATTLE": storyMode = false; phase = Phase.Select; break;
            case "TRAINING": storyMode = false; StartTutorial(false); break;
            case "WORLD MAP": page = Page.WorldMap; break;
            case "CONTROLS": page = Page.Controls; break;
            case "CREDITS": page = Page.Credits; break;
            case "QUIT": Application.Quit(); break;
        }
    }

    void ConfirmSelect()
    {
        SFX.Play(SFX.S.Boom, 0.5f, 1.1f);
        if (storyMode)
        {
            StartStory(Story.Prologue(SelectedHero), () => StartTutorial(true));
        }
        else BeginRun();
    }

    void StoryAfterVictory()
    {
        int map = SelectedMap;
        bool last = map >= MapDef.All.Length - 1;
        StartStory(new[] { Story.ChapterOutro(map) }, () =>
        {
            if (last)
            {
                PlayerPrefs.DeleteKey(SaveChapter); PlayerPrefs.Save();
                endingMusic = true;
                StartStory(Story.Ending(SelectedHero), () => { rollingCredits = true; creditsT = 0f; endingMusic = true; phase = Phase.Story; });
            }
            else
            {
                PlayerPrefs.SetInt(SaveChapter, map + 1); PlayerPrefs.SetInt(SaveHero, SelectedHero); PlayerPrefs.Save();
                SelectedMap = map + 1;
                Carry(true);
                pendingChapterIntro = true;
                Reload();
            }
        });
    }

    void Retry()
    {
        Time.timeScale = 1f;
        skipTitle = true; pendingChapterIntro = false;
        carriedScore = 0; carriedKills = 0; carriedTime = 0;
        Reload();
    }

    void BackToMenu()
    {
        Time.timeScale = 1f;
        storyMode = false; pendingChapterIntro = false;
        Carry(false);
        Reload();
    }

    void SetPaused(bool p)
    {
        paused = p;
        pauseIndex = 0; pauseControls = false;
        Time.timeScale = p ? 0f : 1f;
        Cursor.lockState = p ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = p;
        SFX.Play(SFX.S.Throw, 0.3f, p ? 0.7f : 1.2f);
    }

    static readonly string[] PauseItems = { "RESUME", "RESTART REALM", "CONTROLS", "MAIN MENU", "QUIT GAME" };

    void UpdatePause()
    {
        bool back = Input.GetKeyDown(KeyCode.Escape);
        bool ok = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        if (pauseControls) { if (back || ok) pauseControls = false; return; }
        if (back) { SetPaused(false); return; }
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) pauseIndex = (pauseIndex + PauseItems.Length - 1) % PauseItems.Length;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) pauseIndex = (pauseIndex + 1) % PauseItems.Length;
        if (ok) ChoosePause(pauseIndex);
    }

    void ChoosePause(int i)
    {
        switch (i)
        {
            case 0: SetPaused(false); break;
            case 1: Retry(); break;
            case 2: pauseControls = true; break;
            case 3: BackToMenu(); break;
            case 4: Application.Quit(); break;
        }
    }

    void UpdateMusic()
    {
        Music.Track t; float pitch = 1f;
        switch (phase)
        {
            case Phase.Tutorial: t = Music.Track.Battle; pitch = 0.92f; break;
            case Phase.Countdown:
            case Phase.Fighting:
                bool bossWave = waveIndex >= Waves.Length - 1 && (phase == Phase.Fighting || countdown < 2f);
                t = bossWave ? Music.Track.Boss : Music.Track.Battle;
                pitch = SelectedMap == 1 ? 0.95f : SelectedMap == 2 ? 1.04f : 1f;
                break;
            case Phase.Victory: t = storyMode ? Music.Track.Menu : Music.Track.Ending; break;
            case Phase.Story: t = endingMusic ? Music.Track.Ending : Music.Track.Menu; break;
            default: t = Music.Track.Menu; break;
        }
        Music.Play(t, pitch);
        Music.Duck(paused ? 0.35f : phase == Phase.GameOver ? 0.45f : 1f);
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
        if (e.trainingDummy) { dummyRespawn = 1.5f; return; }
        kills++;
        int baseScore = e.type switch
        {
            EnemyType.Grunt => 100, EnemyType.Brute => 350, EnemyType.Caster => 200, EnemyType.Stalker => 250, _ => 5000
        };
        int combo = PlayerController.I != null ? PlayerController.I.Combo : 0;
        score += Mathf.RoundToInt(baseScore * (1f + combo * 0.1f));
        if (e == boss)
        {
            CineBossDeath(e);
            pending.Clear();
            foreach (var other in Enemy.All.ToArray())
                other.health.TakeDamage(new DamageInfo { amount = 9999, source = e.transform.position, knockback = 10, fromPlayer = true });
        }
    }

    public void OnPlayerDied()
    {
        phase = Phase.GameOver;
        deathT = 0f;
        CinePlayerDeath();
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

    // ================================================================ Cinematics
    // Camera paths are functions of normalised time k (0..1). Unscaled time so slow-mo shots still play.
    delegate void CinePath(float k, out Vector3 pos, out Vector3 look);

    void RunCine(float dur, CinePath path, string title, string sub, bool skippable, bool letterbox, float fov = 50f, float smooth = 6f, bool cut = true, System.Action done = null)
    {
        if (cineCo != null) StopCoroutine(cineCo);
        cineCo = StartCoroutine(CineRoutine(dur, path, title, sub, skippable, letterbox, fov, smooth, cut, done));
    }

    System.Action cineDone;

    System.Collections.IEnumerator CineRoutine(float dur, CinePath path, string title, string sub, bool skippable, bool letterbox, float fov, float smooth, bool cut, System.Action done)
    {
        cineActive = true; cineSkippable = skippable; cineLetterbox = letterbox;
        cineTitle = title; cineSub = sub; cineT = 0f; cineDur = dur; cineDone = done;
        ThirdPersonCamera.cineActive = true;
        ThirdPersonCamera.cineFov = fov;
        ThirdPersonCamera.cineSmooth = smooth;
        path(0f, out var p0, out var l0);
        ThirdPersonCamera.cinePos = p0; ThirdPersonCamera.cineLook = l0;
        ThirdPersonCamera.cineCut = cut;
        while (cineT < dur)
        {
            path(Mathf.Clamp01(cineT / dur), out var p, out var l);
            ThirdPersonCamera.cinePos = p; ThirdPersonCamera.cineLook = l;
            yield return null;
        }
        EndCine();
    }

    void EndCine()
    {
        if (cineCo != null) { StopCoroutine(cineCo); cineCo = null; }
        cineActive = false;
        ThirdPersonCamera.cineActive = false;
        var d = cineDone; cineDone = null;
        d?.Invoke();
    }

    static float Ease(float k) => k * k * (3f - 2f * k);

    void CineRealmIntro()
    {
        var p = PlayerController.I; if (p == null) return;
        Vector3 hero = p.transform.position;
        float behind = (p.transform.eulerAngles.y + 180f) * Mathf.Deg2Rad;
        float start = behind + 2.4f;
        string title = Map.name;
        string sub = storyMode ? $"CHAPTER {new[] { "I", "II", "III" }[SelectedMap]}  ·  {Map.subtitle}" : Map.subtitle;
        RunCine(6.5f, (float k, out Vector3 pos, out Vector3 look) =>
        {
            float e = Ease(k);
            float a = Mathf.Lerp(start, behind, e);
            float r = Mathf.Lerp(30f, 5.5f, e);
            float y = Mathf.Lerp(16f, 2.6f, Ease(Mathf.Clamp01(k * 1.15f)));
            var center = Vector3.Lerp(Vector3.zero, hero, e);
            pos = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r + Vector3.up * y;
            look = Vector3.Lerp(Vector3.up * 3f, hero + Vector3.up * 1.7f, Ease(Mathf.Clamp01(k * 1.3f)));
        }, title, sub, true, true, 55f, 5f, true, () => ShowMessage("SURVIVE", 1.4f));
        SFX.Play(SFX.S.Boom, 0.6f, 0.6f);
    }

    void CineBossIntro(Enemy b)
    {
        string[] parts = b.displayName.Split(',');
        Vector3 bp = b.transform.position;
        Vector3 fwd = b.transform.forward; fwd.y = 0; fwd.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        float s = b.Scale;
        RunCine(5f, (float k, out Vector3 pos, out Vector3 look) =>
        {
            float e = Ease(k);
            pos = bp + fwd * Mathf.Lerp(5.5f * s, 3.2f * s, e) + side * Mathf.Lerp(-1.2f, 0.8f, e) * s + Vector3.up * Mathf.Lerp(0.4f, 1.0f, e) * s;
            look = bp + Vector3.up * Mathf.Lerp(1.4f, 2.0f, e) * s;
        }, parts[0].Trim(), parts.Length > 1 ? parts[1].Trim().ToUpper() : "", true, true, 42f, 4f, true);
        StartCoroutine(BossRoar(b));
    }

    System.Collections.IEnumerator BossRoar(Enemy b)
    {
        yield return new WaitForSecondsRealtime(1.6f);
        if (b == null) yield break;
        SFX.Play(SFX.S.Roar, 1f, 0.75f);
        FX.Shake(1.2f);
        var mat = b.bossKind == 1 ? GameAssets.I.frostGlow : b.bossKind == 2 ? GameAssets.I.stormGlow : GameAssets.I.bossGlow;
        FX.Ring(b.transform.position, mat, 8f, 0.8f);
    }

    void CineBossDeath(Enemy b)
    {
        Vector3 bp = b.transform.position; float s = b.Scale;
        float a0 = Random.Range(0f, Mathf.PI * 2f);
        RunCine(3.6f, (float k, out Vector3 pos, out Vector3 look) =>
        {
            float a = a0 + k * 1.6f;
            pos = bp + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (4.5f * s) + Vector3.up * (1.8f + k) * s;
            look = bp + Vector3.up * 1.2f * s;
        }, BossDefs.Fallen[b.bossKind], "", false, true, 45f, 5f, true);
    }

    public void CineExecution(PlayerController p, Enemy e)
    {
        Vector3 mid = (p.transform.position + e.transform.position) * 0.5f;
        Vector3 dir = e.transform.position - p.transform.position; dir.y = 0; dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? -1f : 1f);
        float s = Mathf.Max(1f, e.Scale);
        RunCine(1.75f, (float k, out Vector3 pos, out Vector3 look) =>
        {
            pos = mid + side * (3.4f + k * 0.8f) * s - dir * (1.2f - k) + Vector3.up * (1.1f + k * 0.5f) * s;
            look = mid + Vector3.up * 1.3f * s;
        }, null, null, false, true, 45f, 9f, true);
    }

    public void CineUltimate(PlayerController p)
    {
        Vector3 hp = p.transform.position; Vector3 f = p.transform.forward;
        Vector3 r = p.transform.right;
        RunCine(1.1f, (float k, out Vector3 pos, out Vector3 look) =>
        {
            pos = hp + r * (2.6f - k * 0.6f) + f * (1.4f + k * 0.6f) + Vector3.up * (1.0f + k * 1.6f);
            look = hp + Vector3.up * (1.5f + k * 0.3f);
        }, p.Hero.ultName, null, false, true, 55f, 7f, false);
    }

    void CinePlayerDeath()
    {
        var p = PlayerController.I; if (p == null) return;
        Vector3 hp = p.transform.position;
        float a0 = (p.transform.eulerAngles.y + 150f) * Mathf.Deg2Rad;
        RunCine(2.5f, (float k, out Vector3 pos, out Vector3 look) =>
        {
            float a = a0 + k * 0.6f;
            pos = hp + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (3f + k * 4f) + Vector3.up * (1.5f + k * 6f);
            look = hp + Vector3.up * 0.6f;
        }, null, null, false, true, 50f, 3f, false);
        FX.SlowMo(1.5f, 0.3f);
    }

    void DrawCineOverlay(float w, float h)
    {
        float k = Mathf.Clamp01(cineT * 3f) * Mathf.Clamp01((cineDur - cineT) * 3f + (cineDur - cineT > 0.2f ? 1f : 0f));
        if (cineLetterbox) Letterbox(w, h, Mathf.Clamp01(cineT * 3f));
        if (!string.IsNullOrEmpty(cineTitle))
        {
            float a = Mathf.Clamp01((cineT - 0.6f) * 1.5f) * Mathf.Clamp01((cineDur - cineT) * 1.5f);
            float y = h * 0.66f;
            Glow(new Rect(0, y, w, 90), cineTitle, new GUIStyle(big) { fontSize = 66 }, new Color(1f, 0.6f, 0.25f, a), a);
            if (!string.IsNullOrEmpty(cineSub))
            {
                Box(new Rect(w * 0.5f - 220 * a, y + 88, 440 * a, 2), new Color(1f, 0.55f, 0.2f, 0.8f * a));
                Shadowed(new Rect(0, y + 96, w, 34), cineSub, new GUIStyle(center) { fontSize = 22, fontStyle = FontStyle.Bold }, new Color(1f, 0.92f, 0.8f, a));
            }
        }
        if (cineSkippable) Shadowed(new Rect(w - 360, h - h * 0.11f + 14, 320, 30), "SPACE  skip", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontSize = 15 }, new Color(1, 1, 1, 0.5f * k));
    }

    // ================================================================ Training tutorial
    struct TutStep { public string title, text; public string[] keys; }

    static readonly TutStep[] TutSteps =
    {
        new TutStep { title = "MOVE", text = "Walk around the arena", keys = new[] { "W", "A", "S", "D" } },
        new TutStep { title = "LOOK", text = "Move the mouse to turn the camera", keys = new[] { "MOUSE" } },
        new TutStep { title = "SPRINT", text = "Hold SHIFT while moving to sprint", keys = new[] { "SHIFT", "+", "W" } },
        new TutStep { title = "LIGHT ATTACK", text = "Left-click to strike the training dummy. Keep clicking for a combo (4 hits)", keys = new[] { "LEFT CLICK" } },
        new TutStep { title = "HEAVY ATTACK", text = "Right-click for a heavy attack. Every weapon has its own (slam / cyclone / skyfall)", keys = new[] { "RIGHT CLICK" } },
        new TutStep { title = "DODGE ROLL", text = "Press SPACE to roll (twice). Roll right before a hit to trigger slow-motion", keys = new[] { "SPACE" } },
        new TutStep { title = "BLOCK & PARRY", text = "The dummy now fights back! HOLD F to block its swing. Tap F just as it hits = PARRY", keys = new[] { "HOLD F" } },
        new TutStep { title = "JAVELIN", text = "Press E to throw your Galeshard javelin, then E again to call it back to your hand", keys = new[] { "E", "E" } },
        new TutStep { title = "SWITCH WEAPON", text = "Press Q (or 1 / 2 / 3) to swap between greatsword, twin blades and war maul", keys = new[] { "Q", "1", "2", "3" } },
        new TutStep { title = "EXECUTE", text = "Hit the dummy until its YELLOW stun bar is full, get close, then press V to EXECUTE", keys = new[] { "V", "or", "MIDDLE MOUSE" } },
        new TutStep { title = "ULTIMATE", text = "Your power bar is full. Press R to unleash your ULTIMATE", keys = new[] { "R" } },
    };

    void StartTutorial(bool thenStory)
    {
        tutorialThenStory = thenStory;
        phase = Phase.Tutorial;
        tutStep = -1;
        tutCompleteT = -1f;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        ShowMessage("TRAINING GROUNDS", 2f);
        NextTutStep();
    }

    void NextTutStep()
    {
        tutStep++;
        tutDoneT = -1f;
        var p = PlayerController.I;
        if (p == null) return;
        tutMoved = 0f; tutLooked = 0f; tutLastPos = p.transform.position;
        tutLastYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
        tutBase = new[] { p.statLight, p.statHeavies, p.statDodges, p.statBlocks, p.statThrows, p.statCatches, p.statSwitches, p.statExecutions, p.statUlts, Mathf.RoundToInt(p.statSprint * 100f) };
        if (tutStep == 3 && dummy == null) SpawnDummy();
        if (dummy != null) dummy.dummyAggro = tutStep == 6;
        if (tutStep == 10) p.rage = p.maxRage;
        if (tutStep >= TutSteps.Length)
        {
            tutCompleteT = 0f;
            ShowMessage("TRAINING COMPLETE", 2.5f);
            SFX.Play(SFX.S.Boom, 0.7f, 1.2f);
        }
        else SFX.Play(SFX.S.Pickup, 0.5f);
    }

    void SpawnDummy()
    {
        var p = PlayerController.I;
        var pos = p.transform.position + p.transform.forward * 5f; pos.y = 0f;
        dummy = Spawn(EnemyType.Grunt, pos);
        dummy.MakeDummy();
        dummy.dummyAggro = tutStep == 6;
    }

    public void OnDummyExecuted(Enemy e) { }

    bool TutCheck(PlayerController p)
    {
        switch (tutStep)
        {
            case 0: return tutMoved > 8f;
            case 1: return tutLooked > 140f;
            case 2: return p.statSprint * 100f - tutBase[9] > 120f;
            case 3: return p.statLight - tutBase[0] >= 4;
            case 4: return p.statHeavies - tutBase[1] >= 1;
            case 5: return p.statDodges - tutBase[2] >= 2;
            case 6: return p.statBlocks - tutBase[3] >= 1;
            case 7: return p.statThrows - tutBase[4] >= 1 && p.statCatches - tutBase[5] >= 1;
            case 8: return p.statSwitches - tutBase[6] >= 2;
            case 9: return p.statExecutions - tutBase[7] >= 1;
            case 10: return p.statUlts - tutBase[8] >= 1;
        }
        return false;
    }

    void UpdateTutorial()
    {
        var p = PlayerController.I;
        if (p == null) return;
        if (dummyRespawn > 0f) { dummyRespawn -= Time.deltaTime; if (dummyRespawn <= 0f && tutCompleteT < 0f) SpawnDummy(); }
        if (tutCompleteT >= 0f)
        {
            tutCompleteT += Time.unscaledDeltaTime;
            if (tutCompleteT > 3f)
            {
                if (dummy != null) Destroy(dummy.gameObject);
                foreach (var e in Enemy.All.ToArray()) if (e != null) Destroy(e.gameObject);
                Enemy.All.Clear();
                if (tutorialThenStory) StartStory(new[] { Story.ChapterIntro(0) }, () => { p.health.Heal(9999f); p.rage = 0f; BeginRun(); });
                else BackToMenu();
            }
            return;
        }
        var pos = p.transform.position; pos.y = 0; var lp = tutLastPos; lp.y = 0;
        tutMoved += Vector3.Distance(pos, lp); tutLastPos = p.transform.position;
        if (Camera.main != null)
        {
            float yaw = Camera.main.transform.eulerAngles.y;
            tutLooked += Mathf.Abs(Mathf.DeltaAngle(tutLastYaw, yaw)); tutLastYaw = yaw;
        }
        if (p.health.Fraction < 0.5f) p.health.Heal(5f);
        if (tutStep == 10 && p.rage < p.maxRage && !p.UltActive && p.statUlts == tutBase[8]) p.rage = p.maxRage;
        if (tutDoneT < 0f && TutCheck(p)) { tutDoneT = 0f; SFX.Play(SFX.S.Parry, 0.5f, 1.5f); }
        if (tutDoneT >= 0f) { tutDoneT += Time.unscaledDeltaTime; if (tutDoneT > 1.1f) NextTutStep(); }
    }

    void KeyChip(ref float x, float y, string key, Color accent, float alpha = 1f)
    {
        var st = new GUIStyle(center) { fontSize = 16, fontStyle = FontStyle.Bold };
        float kw = Mathf.Max(38f, st.CalcSize(new GUIContent(key)).x + 20f);
        if (key == "or" || key == "+") { Shadowed(new Rect(x, y, 30, 34), key, st, new Color(1, 1, 1, 0.7f * alpha)); x += 34; return; }
        Box(new Rect(x, y, kw, 34), new Color(0.05f, 0.04f, 0.04f, 0.85f * alpha));
        Frame(new Rect(x, y, kw, 34), new Color(accent.r, accent.g, accent.b, alpha), 2);
        Box(new Rect(x + 2, y + 29, kw - 4, 3), new Color(accent.r, accent.g, accent.b, 0.5f * alpha));
        Shadowed(new Rect(x, y, kw, 34), key, st, new Color(1, 1, 1, alpha));
        x += kw + 8;
    }

    void DrawTutorial(float w, float h)
    {
        if (tutStep < 0 || tutStep >= TutSteps.Length)
        {
            if (tutCompleteT >= 0f)
                Shadowed(new Rect(0, h * 0.4f, w, 40), tutorialThenStory ? "Your journey begins..." : "Returning to the main menu...", mid, new Color(1, 1, 1, 0.8f));
            return;
        }
        var st = TutSteps[tutStep];
        bool done = tutDoneT >= 0f;
        float pw = 760, ph = 150, px = (w - pw) * 0.5f, py = h - ph - 110;
        Box(new Rect(px, py, pw, ph), new Color(0.03f, 0.02f, 0.02f, 0.78f));
        var acc = done ? new Color(0.4f, 1f, 0.45f) : new Color(1f, 0.6f, 0.2f);
        Frame(new Rect(px, py, pw, ph), new Color(acc.r, acc.g, acc.b, 0.8f), 2);
        Box(new Rect(px, py, pw * (tutStep + (done ? 1f : 0f)) / TutSteps.Length, 4), acc);
        Shadowed(new Rect(px + 20, py + 12, 300, 22), $"TRAINING  {tutStep + 1} / {TutSteps.Length}", new GUIStyle(small) { fontSize = 14, fontStyle = FontStyle.Bold }, new Color(1, 1, 1, 0.6f));
        Shadowed(new Rect(px + 20, py + 30, pw - 40, 40), done ? st.title + "  —  DONE!" : st.title, new GUIStyle(mid) { alignment = TextAnchor.MiddleLeft, fontSize = 30 }, acc);
        Shadowed(new Rect(px + 20, py + 72, pw - 40, 26), st.text, new GUIStyle(small) { fontSize = 18 }, Color.white);
        float x = px + 20;
        foreach (var k in st.keys) KeyChip(ref x, py + 104, k, acc, done ? 0.5f : 1f);
        if (tutStep == 0) Shadowed(new Rect(px + pw - 240, py + 108, 220, 26), $"{Mathf.Min(100, Mathf.RoundToInt(tutMoved / 8f * 100))}%", new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, acc);
        Shadowed(new Rect(px, py + ph + 6, pw, 22), "ESC  pause / quit training", new GUIStyle(center) { fontSize = 13 }, new Color(1, 1, 1, 0.45f));
    }

    // Always-visible control strip at the bottom of the screen.
    void DrawControlBar(float w, float h)
    {
        if (phase != Phase.Fighting && phase != Phase.Countdown) return;
        var items = new (string key, string label)[] { ("LMB", "Attack"), ("RMB", "Heavy"), ("SPACE", "Dodge"), ("F", "Block/Parry"), ("E", "Javelin"), ("V", "Execute"), ("R", "Ultimate"), ("Q", "Weapon") };
        var ks = new GUIStyle(center) { fontSize = 13, fontStyle = FontStyle.Bold };
        var ls = new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
        float total = 0f;
        foreach (var it in items) total += Mathf.Max(30f, ks.CalcSize(new GUIContent(it.key)).x + 12f) + ls.CalcSize(new GUIContent(it.label)).x + 22f;
        float x = (w - total) * 0.5f, y = h - 34;
        var p = PlayerController.I;
        foreach (var it in items)
        {
            float kw = Mathf.Max(30f, ks.CalcSize(new GUIContent(it.key)).x + 12f);
            bool hot = (it.key == "V" && p != null && p.ExecutableTarget() != null) || (it.key == "R" && p != null && p.rage >= p.maxRage && !p.UltActive);
            Color c = hot ? Color.Lerp(new Color(1f, 0.8f, 0.2f), Color.white, Mathf.PingPong(Time.unscaledTime * 4f, 1f)) : new Color(1f, 0.6f, 0.25f, 0.85f);
            Box(new Rect(x, y, kw, 24), new Color(0, 0, 0, 0.7f));
            Frame(new Rect(x, y, kw, 24), c, 1);
            Shadowed(new Rect(x, y, kw, 24), it.key, ks, hot ? c : Color.white);
            x += kw + 5;
            float lw = ls.CalcSize(new GUIContent(it.label)).x;
            Shadowed(new Rect(x, y, lw + 4, 24), it.label, ls, new Color(1, 1, 1, hot ? 1f : 0.7f));
            x += lw + 17;
        }
    }

    // Floating health + stun bars over enemies, and the EXECUTE prompt.
    void DrawEnemyBars(float w, float h)
    {
        var cam = Camera.main; var p = PlayerController.I;
        if (cam == null || p == null) return;
        float ui = Screen.height / h;
        var exec = p.ExecutableTarget();
        foreach (var e in Enemy.All)
        {
            if (e == null || e.health.IsDead) continue;
            var sp = cam.WorldToScreenPoint(e.transform.position + Vector3.up * (2.45f * e.Scale + 0.2f));
            if (sp.z < 0.5f || sp.z > 40f) continue;
            float x = sp.x / ui, y = (Screen.height - sp.y) / ui;
            if (e.IsStunned)
            {
                bool can = e == exec;
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                var col = can ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 0.85f, 0.2f, 0.6f);
                Tex(new Rect(x - 20 * pulse, y - 46 - 20 * pulse, 40 * pulse, 40 * pulse), ring, col);
                Glow(new Rect(x - 120, y - 40, 240, 30), can ? "[V]  EXECUTE" : "STUNNED", new GUIStyle(mid) { fontSize = can ? 22 : 16 }, col, can ? pulse : 0.3f);
            }
            if (e.type == EnemyType.Boss) continue; // boss has the big bar at the top
            bool show = e.health.Fraction < 0.999f || e.StunFraction > 0.01f || e.trainingDummy;
            if (!show) continue;
            float bw = 70f;
            Box(new Rect(x - bw * 0.5f - 2, y - 2, bw + 4, 13), new Color(0, 0, 0, 0.7f));
            Box(new Rect(x - bw * 0.5f, y, bw * e.health.Fraction, 5), new Color(0.9f, 0.15f, 0.1f));
            Box(new Rect(x - bw * 0.5f, y + 6, bw * e.StunFraction, 3), e.IsStunned ? Color.Lerp(new Color(1f, 0.8f, 0.2f), Color.white, Mathf.PingPong(Time.unscaledTime * 5f, 1f)) : new Color(1f, 0.8f, 0.2f));
            if (e.trainingDummy) Shadowed(new Rect(x - 80, y - 22, 160, 20), "TRAINING DUMMY", new GUIStyle(center) { fontSize = 12 }, new Color(1, 1, 1, 0.7f));
        }
        // boss stun bar under the big health bar
        if (boss != null && !boss.health.IsDead)
        {
            float bw = Mathf.Min(760f, w * 0.6f);
            Box(new Rect((w - bw) * 0.5f, 80, bw * boss.StunFraction, 5), new Color(1f, 0.8f, 0.2f, 0.9f));
        }
    }

    // ================================================================ GUI helpers
    void Styles()
    {
        if (big != null) return;
        big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        mid = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperLeft, richText = true };
        center = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        tiny = new GUIStyle(small) { fontSize = 14, wordWrap = true, alignment = TextAnchor.UpperCenter };
        cardTitle = new GUIStyle(mid) { fontSize = 22 };
        menuItem = new GUIStyle(mid) { fontSize = 28, alignment = TextAnchor.MiddleLeft };
        storyHead = new GUIStyle(mid) { fontSize = 34 };
        storyBody = new GUIStyle(small) { fontSize = 22, alignment = TextAnchor.UpperCenter, wordWrap = true };

        disc = MakeDisc(64, false);
        ring = MakeDisc(64, true);
        arrow = MakeArrow(32);
        gradL = MakeGrad(256, 1, true);
        gradV = MakeGrad(1, 256, false);
        worldMap = MakeWorldMap(640, 360);
    }

    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        return t;
    }

    static Texture2D MakeDisc(int n, bool ringOnly)
    {
        var t = NewTex(n, n); var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = ringOnly ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * 22f) : Mathf.Clamp01((1f - d) * n * 0.5f);
                px[y * n + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    static Texture2D MakeArrow(int n)
    {
        var t = NewTex(n, n); var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n; // v: 0 bottom .. 1 top
                bool inside = v > 0.08f && v < 0.95f && Mathf.Abs(u) < (0.95f - v) * 0.5f && !(v < 0.35f && Mathf.Abs(u) < (0.35f - v) * 0.9f);
                px[y * n + x] = new Color(1, 1, 1, inside ? 1f : 0f);
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    static Texture2D MakeGrad(int w, int h, bool horizontal)
    {
        var t = NewTex(w, h); var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float k = horizontal ? x / (float)(w - 1) : y / (float)(h - 1);
                px[y * w + x] = new Color(0, 0, 0, Mathf.Pow(1f - k, horizontal ? 1.4f : 1.6f));
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    // Locations on the world map (u = left..right, v = top..bottom)
    static readonly Vector2[] MapNodes = { new Vector2(0.2f, 0.58f), new Vector2(0.48f, 0.26f), new Vector2(0.8f, 0.42f) };
    static readonly Vector2 Emberhold = new Vector2(0.52f, 0.74f);
    static readonly Color[] RealmColor = { new Color(1f, 0.5f, 0.2f), new Color(0.5f, 0.85f, 1f), new Color(0.72f, 0.6f, 1f) };

    // Procedural world map of Ashenreach: volcanic west, frozen north, storm peaks east, Emberhold in the south.
    static Texture2D MakeWorldMap(int W, int H)
    {
        var t = NewTex(W, H); var px = new Color[W * H];
        var parchment = new Color(0.62f, 0.53f, 0.4f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = 1f - y / (float)H; // v from top
                float dx = (u - 0.5f) * 1.25f, dy = (v - 0.5f) * 1.6f;
                float land = 0.62f - Mathf.Sqrt(dx * dx + dy * dy) * 0.75f
                             + (Mathf.PerlinNoise(u * 4f + 3.1f, v * 2.6f + 7.7f) - 0.5f) * 0.55f
                             + (Mathf.PerlinNoise(u * 11f + 1.3f, v * 7f + 2.2f) - 0.5f) * 0.22f;
                foreach (var n in MapNodes) land += 0.35f * Mathf.Exp(-((u - n.x) * (u - n.x) * 3f + (v - n.y) * (v - n.y)) / 0.02f);
                land += 0.35f * Mathf.Exp(-((u - Emberhold.x) * (u - Emberhold.x) * 3f + (v - Emberhold.y) * (v - Emberhold.y)) / 0.02f);

                Color c;
                if (land < 0.3f)
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin((land * 90f) + u * 6f);
                    c = Color.Lerp(new Color(0.07f, 0.09f, 0.13f), new Color(0.12f, 0.15f, 0.2f), Mathf.Clamp01(land / 0.3f)) * (0.92f + wave * 0.08f);
                }
                else
                {
                    Color fire = new Color(0.42f, 0.2f, 0.12f), frost = new Color(0.62f, 0.7f, 0.78f), storm = new Color(0.33f, 0.29f, 0.45f), home = new Color(0.55f, 0.48f, 0.3f);
                    float wf = Mathf.Clamp01(1f - Mathf.Abs(u - 0.18f) * 3.2f);
                    float wi = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v * 0.8f), new Vector2(0.48f, 0.2f)) * 3.2f);
                    float ws = Mathf.Clamp01(1f - Mathf.Abs(u - 0.82f) * 3.2f);
                    float wh = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), Emberhold) * 4.5f) * 1.4f;
                    float sum = wf + wi + ws + wh + 0.001f;
                    c = (fire * wf + frost * wi + storm * ws + home * wh) / sum;
                    if (sum < 0.05f) c = parchment * 0.6f;
                    float elev = land + (Mathf.PerlinNoise(u * 20f, v * 20f) - 0.5f) * 0.08f;
                    float band = Mathf.Repeat(elev * 14f, 1f);
                    if (band < 0.07f) c *= 0.82f;                              // contour lines
                    if (land < 0.34f) c = Color.Lerp(c * 0.5f, c, (land - 0.3f) / 0.04f); // dark coast
                    c = Color.Lerp(c, parchment, 0.18f);
                }
                float vig = Mathf.Clamp01(1.25f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.3f);
                c *= Mathf.Lerp(0.45f, 1f, vig);
                c.a = 1f;
                px[y * W + x] = c;
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    static void Box(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Tex(Rect r, Texture t, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, t);
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
        Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height * 0.35f), new Color(1, 1, 1, 0.18f));
    }

    static void Shadowed(Rect r, string text, GUIStyle s, Color c)
    {
        GUI.color = new Color(0, 0, 0, c.a * 0.85f);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
        GUI.color = c;
        GUI.Label(r, text, s);
        GUI.color = Color.white;
    }

    // Big text with a soft coloured glow behind it.
    static void Glow(Rect r, string text, GUIStyle s, Color c, float glow = 1f)
    {
        var g = new Color(c.r, c.g, c.b, 0.12f * glow);
        GUI.color = g;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * 5f, r.y + Mathf.Sin(a) * 5f, r.width, r.height), text, s);
        }
        Shadowed(r, text, s, c);
    }

    static bool Clicked(Rect r)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { e.Use(); return true; }
        return false;
    }

    static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);
    Vector3 lastMouse;
    bool mouseMovedFrame;
    bool MouseMoved => Event.current.type == EventType.Repaint && mouseMovedFrame;

    // Ember particles drifting up the screen (menus / cutscenes).
    static void Embers(float w, float h, int count, float alpha)
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < count; i++)
        {
            float h1 = Mathf.Repeat(i * 0.6180339f, 1f), h2 = Mathf.Repeat(i * 0.4142135f + 0.3f, 1f), h3 = Mathf.Repeat(i * 0.7320508f, 1f);
            float k = Mathf.Repeat(t * (0.04f + h2 * 0.06f) + h3, 1f);
            float x = h1 * w + Mathf.Sin(t * (0.6f + h3) + i) * 30f;
            float y = h * (1.05f - k * 1.1f);
            float s = 2f + h2 * 4f;
            float a = Mathf.Sin(k * Mathf.PI) * alpha * (0.4f + 0.6f * h3);
            Box(new Rect(x, y, s, s), new Color(1f, 0.45f + h2 * 0.3f, 0.12f, a));
        }
    }

    static void Letterbox(float w, float h, float k)
    {
        float bar = h * 0.11f * k;
        Box(new Rect(0, 0, w, bar), Color.black);
        Box(new Rect(0, h - bar, w, bar), Color.black);
    }

    // ================================================================ GUI
    void OnGUI()
    {
        Styles();
        if (Event.current.type == EventType.Repaint)
        {
            mouseMovedFrame = (Input.mousePosition - lastMouse).sqrMagnitude > 1f;
            lastMouse = Input.mousePosition;
        }
        float w = Screen.width, h = Screen.height;
        float ui = Mathf.Clamp(Mathf.Min(h / 900f, w / 1500f), 0.6f, 1.6f);
        var baseM = Matrix4x4.Scale(new Vector3(ui, ui, 1));
        GUI.matrix = baseM;
        w /= ui; h /= ui;

        switch (phase)
        {
            case Phase.MainMenu: DrawMainMenu(w, h); return;
            case Phase.Select: DrawSelect(w, h); return;
            case Phase.Story: DrawStory(w, h); return;
        }

        if (!cineActive) DrawHUD(w, h, baseM);

        if ((phase == Phase.GameOver && deathT >= 2.5f) || (phase == Phase.Victory && !storyMode))
        {
            Box(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.6f));
            Letterbox(w, h, 1f);
            bool win = phase == Phase.Victory;
            bool last = SelectedMap >= MapDef.All.Length - 1;
            string head = win ? (last ? "ALL REALMS CONQUERED" : "REALM CONQUERED") : "YOU HAVE FALLEN";
            Glow(new Rect(0, h * 0.3f, w, 90), head, big, win ? new Color(1f, 0.8f, 0.3f) : new Color(0.9f, 0.15f, 0.1f));
            int mins = Mathf.FloorToInt(runTime / 60f), secs = Mathf.FloorToInt(runTime % 60f);
            Shadowed(new Rect(0, h * 0.3f + 90, w, 40), $"Score {score:N0}   ·   Kills {kills}   ·   Time {mins}:{secs:00}", mid, Color.white);
            string next = win ? (last ? "ENTER  play again from the first realm" : $"ENTER  journey on to {MapDef.All[SelectedMap + 1].name}") : "ENTER  rise again and retry this realm";
            Shadowed(new Rect(0, h * 0.3f + 150, w, 40), next, mid, new Color(1, 1, 1, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f)));
            Shadowed(new Rect(0, h * 0.3f + 195, w, 30), "M  main menu", center, new Color(1, 1, 1, 0.6f));
        }
        else if (phase == Phase.Victory && storyMode)
        {
            float k = Mathf.Clamp01(victoryTime / 2f);
            Letterbox(w, h, k);
        }

        if (cineActive) DrawCineOverlay(w, h);
        if (paused) DrawPause(w, h);
    }

    // ---------------------------------------------------------------- main menu
    void DrawMainMenu(float w, float h)
    {
        Tex(new Rect(0, 0, w * 0.62f, h), gradL, new Color(1, 1, 1, 0.92f));
        Tex(new Rect(0, h * 0.55f, w, h * 0.45f), gradV, new Color(1, 1, 1, 0.75f));
        Embers(w, h, 70, 0.8f);

        float x = 90;
        var titleS = new GUIStyle(big) { fontSize = 88, alignment = TextAnchor.MiddleLeft };
        Glow(new Rect(x, h * 0.09f, w, 110), "BLADE OF ASH", titleS, new Color(1f, 0.55f, 0.2f), 1.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.7f));
        Shadowed(new Rect(x + 6, h * 0.09f + 100, w, 30), "THE  EMBERS  OF  ASHENREACH", new GUIStyle(center) { alignment = TextAnchor.MiddleLeft, fontSize = 20, fontStyle = FontStyle.Bold }, new Color(1f, 0.85f, 0.65f, 0.85f));
        Box(new Rect(x + 6, h * 0.09f + 136, 420, 2), new Color(1f, 0.55f, 0.2f, 0.6f));

        if (page == Page.Controls) { DrawControlsPanel(x, h * 0.36f); Shadowed(new Rect(x, h - 70, 600, 30), "ESC / ENTER  back", small, new Color(1, 1, 1, 0.6f)); return; }
        if (page == Page.Credits) { DrawCreditsPanel(x, h * 0.34f, h); Shadowed(new Rect(x, h - 70, 600, 30), "ESC / ENTER  back", small, new Color(1, 1, 1, 0.6f)); return; }
        if (page == Page.WorldMap)
        {
            Box(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.55f));
            float mw = Mathf.Min(w - 120f, 1100f), mh = mw * 0.5625f;
            DrawWorldMap(new Rect((w - mw) * 0.5f, (h - mh) * 0.5f + 20f, mw, mh), SavedChapter, -1);
            Glow(new Rect(0, (h - mh) * 0.5f - 50f, w, 60), "THE REALM OF ASHENREACH", new GUIStyle(mid) { fontSize = 34 }, new Color(1f, 0.8f, 0.5f));
            Shadowed(new Rect(0, h - 50, w, 30), "ESC / ENTER  back", center, new Color(1, 1, 1, 0.6f));
            return;
        }

        var items = MenuItems();
        float y = h * 0.38f;
        for (int i = 0; i < items.Length; i++)
        {
            var r = new Rect(x, y + i * 58, 440, 50);
            if (MouseMoved && Hover(r)) menuIndex = i;
            bool sel = i == menuIndex;
            if (sel)
            {
                Tex(new Rect(r.x - 30, r.y, 520, r.height), gradL, new Color(0.9f, 0.35f, 0.08f, 0.55f));
                Box(new Rect(r.x - 30, r.y + 6, 5, r.height - 12), new Color(1f, 0.6f, 0.2f));
            }
            Shadowed(new Rect(r.x + (sel ? 14 : 0), r.y, r.width, r.height), items[i], menuItem, sel ? Color.white : new Color(1, 1, 1, 0.55f));
            if (Clicked(r)) { menuIndex = i; ChooseMenu(items[i]); }
        }
        if (SavedChapter > 0 && SavedChapter < MapDef.All.Length)
            Shadowed(new Rect(x, y + items.Length * 58 + 6, 600, 24), $"Saved: Chapter {SavedChapter + 1} — {MapDef.All[SavedChapter].name}  ·  {HeroDef.All[Mathf.Clamp(PlayerPrefs.GetInt(SaveHero, 0), 0, 2)].title}", small, new Color(1f, 0.8f, 0.5f, 0.75f));

        var hero = HeroDef.All[SelectedHero];
        Shadowed(new Rect(w - 520, h - 110, 480, 34), hero.title.ToUpper(), new GUIStyle(mid) { alignment = TextAnchor.MiddleRight, fontSize = 28 }, hero.accent);
        Shadowed(new Rect(w - 520, h - 78, 480, 24), "Choose your champion in NEW STORY or FREE BATTLE", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontSize = 15 }, new Color(1, 1, 1, 0.6f));
        Shadowed(new Rect(x, h - 56, 700, 26), "W / S  or  ARROWS  select     ENTER  confirm     ·     mouse works too", small, new Color(1, 1, 1, 0.5f));
    }

    void DrawControlsPanel(float x, float y)
    {
        Shadowed(new Rect(x, y - 44, 500, 36), "CONTROLS", new GUIStyle(mid) { alignment = TextAnchor.MiddleLeft, fontSize = 30 }, new Color(1f, 0.8f, 0.5f));
        DrawHelp(x + 12, y + 10, false);
    }

    void DrawCreditsPanel(float x, float y, float h)
    {
        var s = new GUIStyle(small) { fontSize = 19 };
        for (int i = 0; i < Story.Credits.Length && y + i * 26 < h - 90; i++)
        {
            var line = Story.Credits[i];
            bool head = line.StartsWith("—") || i == 0;
            Shadowed(new Rect(x, y + i * 26, 700, 26), line, s, head ? new Color(1f, 0.7f, 0.35f) : new Color(1, 1, 1, 0.85f));
        }
    }

    // ---------------------------------------------------------------- hero / realm select
    void DrawSelect(float w, float h)
    {
        Tex(new Rect(0, h * 0.4f, w, h * 0.6f), gradV, new Color(1, 1, 1, 0.95f));
        Box(new Rect(0, 0, w, h * 0.17f), new Color(0, 0, 0, 0.45f));
        Embers(w, h, 40, 0.5f);
        Glow(new Rect(0, h * 0.025f, w, 80), storyMode ? "NEW STORY" : "FREE BATTLE", new GUIStyle(big) { fontSize = 60 }, new Color(1f, 0.55f, 0.2f));
        Shadowed(new Rect(0, h * 0.025f + 70, w, 30), storyMode ? "Your journey begins in the Cinder Colosseum" : "Three heroes  ·  Three weapons  ·  Three realms  ·  Three tyrants", center, new Color(1, 1, 1, 0.8f));

        float cw = Mathf.Min(330f, (w - 120f) / 3f), ch = 150f, gap = 18f;
        float cx = (w - (cw * 3 + gap * 2)) * 0.5f, cy = h * (storyMode ? 0.56f : 0.47f);
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

        float wy;
        if (!storyMode)
        {
            float my = cy + ch + 52;
            Shadowed(new Rect(0, my - 34, w, 30), "CHOOSE YOUR REALM   ( W / S )", center, Color.white);
            for (int i = 0; i < MapDef.All.Length; i++)
            {
                var m = MapDef.All[i];
                var r = new Rect(cx + i * (cw + gap), my, cw, 96);
                bool sel = i == SelectedMap;
                Color mc = RealmColor[i];
                Box(r, new Color(0.05f, 0.04f, 0.04f, sel ? 0.9f : 0.6f));
                if (sel) Frame(r, mc, 3);
                Shadowed(new Rect(r.x, r.y + 6, r.width, 26), $"{i + 1}. {m.name}", cardTitle, sel ? mc : new Color(1, 1, 1, 0.7f));
                GUI.color = new Color(1, 1, 1, sel ? 0.9f : 0.55f);
                GUI.Label(new Rect(r.x + 10, r.y + 34, r.width - 20, 36), m.subtitle, tiny);
                GUI.color = Color.white;
                Shadowed(new Rect(r.x, r.y + 70, r.width, 22), "Boss: " + BossDefs.Names[m.bossKind].Split(',')[0], new GUIStyle(center) { fontSize = 14 }, sel ? mc : new Color(1, 1, 1, 0.5f));
                if (Clicked(r)) PickMap(i);
            }
            wy = my + 112;
        }
        else wy = cy + ch + 22;

        Shadowed(new Rect(0, wy, w, 24), "ARSENAL:  1 ASHBRAND greatsword   ·   2 EMBERFANG TWINS   ·   3 THUNDERJAW MAUL   ·   E  Galeshard Javelin", new GUIStyle(center) { fontSize = 15 }, new Color(1f, 0.85f, 0.6f));
        var btn = new Rect(w * 0.5f - 170, wy + 34, 340, 50);
        Box(btn, new Color(0.6f, 0.2f, 0.05f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f)));
        Frame(btn, new Color(1f, 0.6f, 0.2f), 2);
        Shadowed(btn, storyMode ? "BEGIN THE JOURNEY  ( ENTER )" : "FIGHT  ( ENTER )", new GUIStyle(mid) { fontSize = storyMode ? 21 : 26 }, Color.white);
        if (Clicked(btn)) ConfirmSelect();
        Shadowed(new Rect(30, h - 40, 300, 24), "ESC  back", small, new Color(1, 1, 1, 0.5f));
    }

    // ---------------------------------------------------------------- cutscenes
    void DrawStory(float w, float h)
    {
        Box(new Rect(0, 0, w, h), new Color(0, 0, 0, rollingCredits ? 0.82f : 0.5f));
        Embers(w, h, 50, 0.6f);
        Letterbox(w, h, 1f);

        if (rollingCredits) { DrawRollingCredits(w, h); return; }
        if (pages == null || pageIndex >= pages.Length) return;

        var p = pages[pageIndex];
        float fade = Mathf.Clamp01(pageTime * 1.5f);
        float top = h * 0.14f;
        if (p.showMap)
        {
            float mh = Mathf.Min(h * 0.44f, (w - 200f) * 0.5625f), mw = mh / 0.5625f;
            int conquered = endingMusic ? 3 : SelectedMap + (p.header.StartsWith("THE ") && !p.header.StartsWith("THE OATH") && !p.header.StartsWith("THE RETURN") ? 1 : 0);
            DrawWorldMap(new Rect((w - mw) * 0.5f, top, mw, mh), conquered, endingMusic ? -1 : SelectedMap, fade);
            top += mh + 18f;
        }
        else top = h * 0.3f;

        Glow(new Rect(0, top, w, 50), p.header, storyHead, new Color(1f, 0.7f, 0.35f, fade), fade);
        Box(new Rect(w * 0.5f - 160 * fade, top + 54, 320 * fade, 2), new Color(1f, 0.55f, 0.2f, 0.7f * fade));
        int chars = Mathf.Clamp(Mathf.FloorToInt(pageTime * 55f), 0, p.body.Length);
        Shadowed(new Rect(w * 0.12f, top + 72, w * 0.76f, h * 0.4f), p.body.Substring(0, chars), storyBody, new Color(1f, 0.95f, 0.88f, fade));

        Shadowed(new Rect(w - 520, h - h * 0.11f + 14, 480, 30), $"{pageIndex + 1} / {pages.Length}      ENTER  continue      ESC  skip", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontSize = 15 }, new Color(1, 1, 1, 0.55f));
    }

    void DrawRollingCredits(float w, float h)
    {
        var s = new GUIStyle(center) { fontSize = 24 };
        float y0 = h - creditsT * 42f;
        for (int i = 0; i < Story.Credits.Length; i++)
        {
            float y = y0 + i * 44f;
            if (y < -50 || y > h) continue;
            var line = Story.Credits[i];
            bool head = i == 0 || line.StartsWith("—") || line == "THANK YOU FOR PLAYING";
            float edge = Mathf.Clamp01(Mathf.Min(y - h * 0.11f, h * 0.89f - y) / 80f);
            if (i == 0) Glow(new Rect(0, y - 20, w, 70), line, new GUIStyle(big) { fontSize = 56 }, new Color(1f, 0.55f, 0.2f, edge));
            else Shadowed(new Rect(0, y, w, 40), line, s, head ? new Color(1f, 0.7f, 0.35f, edge) : new Color(1, 1, 1, 0.9f * edge));
        }
        Shadowed(new Rect(w - 420, h - h * 0.11f + 14, 380, 30), "ENTER  main menu", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontSize = 15 }, new Color(1, 1, 1, 0.5f));
    }

    // ---------------------------------------------------------------- world map
    void DrawWorldMap(Rect r, int conquered, int current, float alpha = 1f)
    {
        Box(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16), new Color(0.06f, 0.04f, 0.03f, 0.9f * alpha));
        Frame(new Rect(r.x - 8, r.y - 8, r.width + 16, r.height + 16), new Color(0.75f, 0.55f, 0.3f, 0.8f * alpha), 2);
        Tex(r, worldMap, new Color(1, 1, 1, alpha));
        Vector2 P(Vector2 n) => new Vector2(r.x + n.x * r.width, r.y + n.y * r.height);

        // journey path: Emberhold -> I -> II -> III -> back home
        var route = new[] { Emberhold, MapNodes[0], MapNodes[1], MapNodes[2], Emberhold };
        for (int s = 0; s < route.Length - 1; s++)
        {
            bool done = s < conquered || (s == 3 && conquered >= 3);
            bool homeLeg = s == 3;
            var a = P(route[s]); var b = P(route[s + 1]);
            int dots = Mathf.CeilToInt(Vector2.Distance(a, b) / 11f);
            for (int i = 1; i < dots; i++)
            {
                float k = i / (float)dots;
                var q = Vector2.Lerp(a, b, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * -18f;
                Color c = done ? new Color(1f, 0.6f, 0.2f, alpha) : new Color(0.9f, 0.85f, 0.75f, (homeLeg ? 0.25f : 0.5f) * alpha);
                Box(new Rect(q.x - 2.5f, q.y - 2.5f, 5, 5), c);
            }
        }

        // region labels
        var lab = new GUIStyle(small) { fontSize = Mathf.RoundToInt(r.width / 60f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        string[] regions = { "THE CINDERLANDS", "FROSTVEIL", "THE STORMSPIRE" };
        Vector2[] regionAt = { new Vector2(0.17f, 0.33f), new Vector2(0.3f, 0.12f), new Vector2(0.84f, 0.18f) };
        for (int i = 0; i < 3; i++)
        {
            var q = P(regionAt[i]);
            Shadowed(new Rect(q.x - 150, q.y - 12, 300, 24), regions[i], lab, new Color(1f, 0.95f, 0.85f, 0.55f * alpha));
        }

        // Emberhold
        {
            var q = P(Emberhold); float s = r.width / 34f;
            bool lit = conquered >= 3;
            Tex(new Rect(q.x - s, q.y - s, s * 2, s * 2), disc, new Color(lit ? 1f : 0.4f, lit ? 0.75f : 0.35f, lit ? 0.3f : 0.3f, alpha));
            Tex(new Rect(q.x - s * 1.4f, q.y - s * 1.4f, s * 2.8f, s * 2.8f), ring, new Color(1f, 0.8f, 0.5f, 0.8f * alpha));
            Shadowed(new Rect(q.x - 160, q.y + s * 1.4f, 320, 24), lit ? "EMBERHOLD — THE FLAME BURNS" : "EMBERHOLD — THE COLD ALTAR", new GUIStyle(lab) { fontSize = lab.fontSize + 1 }, new Color(1f, 0.85f, 0.55f, alpha));
        }

        for (int i = 0; i < 3; i++)
        {
            var q = P(MapNodes[i]); float s = r.width / 40f;
            bool done = i < conquered, cur = i == current;
            var c = RealmColor[i];
            if (cur)
            {
                float pulse = 1.4f + 0.5f * Mathf.Repeat(Time.unscaledTime * 0.8f, 1f);
                Tex(new Rect(q.x - s * pulse, q.y - s * pulse, s * 2 * pulse, s * 2 * pulse), ring, new Color(c.r, c.g, c.b, (1f - Mathf.Repeat(Time.unscaledTime * 0.8f, 1f)) * alpha));
            }
            Tex(new Rect(q.x - s, q.y - s, s * 2, s * 2), disc, new Color(0, 0, 0, 0.7f * alpha));
            Tex(new Rect(q.x - s * 0.75f, q.y - s * 0.75f, s * 1.5f, s * 1.5f), disc, new Color(c.r, c.g, c.b, (done || cur ? 1f : 0.45f) * alpha));
            Tex(new Rect(q.x - s * 0.3f, q.y - s * 0.3f, s * 0.6f, s * 0.6f), disc, new Color(1f, 1f, 1f, (done ? 0.9f : 0.35f) * alpha));
            Shadowed(new Rect(q.x - 160, q.y + s + 2, 320, 22), MapDef.All[i].name, lab, new Color(c.r, c.g, c.b, alpha));
            string st = done ? "EMBER RECLAIMED" : cur ? "YOU ARE HERE" : "TYRANT: " + BossDefs.Names[i].Split(',')[0];
            Shadowed(new Rect(q.x - 160, q.y + s + 22, 320, 20), st, new GUIStyle(lab) { fontStyle = FontStyle.Normal, fontSize = lab.fontSize - 2 }, new Color(1, 1, 1, (done ? 0.9f : 0.6f) * alpha));
        }
    }

    // ---------------------------------------------------------------- in-game HUD
    void DrawHUD(float w, float h, Matrix4x4 baseM)
    {
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

            float sw = 172, sx = w - 3 * (sw + 8) - 22, sy = h - 112;
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

            DrawMinimap(24, 24, 200, baseM);
        }

        Shadowed(new Rect(w - 330, 22, 300, 30), $"SCORE  {score:N0}", new GUIStyle(mid) { alignment = TextAnchor.MiddleRight }, Color.white);
        Shadowed(new Rect(w - 430, 54, 400, 26), $"{Map.name}  ·  Wave {Mathf.Max(1, waveIndex + 1)} / {Waves.Length}  ·  Kills {kills}", new GUIStyle(small) { alignment = TextAnchor.MiddleRight }, new Color(1, 1, 1, 0.8f));
        if (storyMode) Shadowed(new Rect(w - 430, 78, 400, 24), $"STORY  ·  Chapter {SelectedMap + 1} of {MapDef.All.Length}", new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontSize = 14 }, new Color(1f, 0.75f, 0.4f, 0.8f));

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
            Glow(new Rect(0, h * 0.28f, w, 90), message, big, new Color(1f, 0.6f, 0.25f, al), al);
        }
        if (weaponMsgTimer > 0f && p != null)
        {
            var wc = WeaponDef.All[(int)p.Weapon].trailColor;
            Shadowed(new Rect(0, h * 0.62f, w, 40), weaponMsg, mid, new Color(wc.r, wc.g, wc.b, Mathf.Clamp01(weaponMsgTimer * 2f)));
        }

        if (!cineActive && phase != Phase.GameOver)
        {
            DrawEnemyBars(w, h);
            if (helpTimer > 0f && !paused) DrawHelp(30, 300, true);
            else if (!paused) Shadowed(new Rect(30, 272, 400, 24), "[H] full controls    [ESC] pause", small, new Color(1, 1, 1, 0.5f));
            if (phase == Phase.Tutorial) DrawTutorial(w, h);
            else DrawControlBar(w, h);
        }
    }

    // GTA-style radar: rotates with the camera, enemies as red blips (clamped to the rim when far).
    void DrawMinimap(float x, float y, float size, Matrix4x4 baseM)
    {
        var p = PlayerController.I; var camT = Camera.main != null ? Camera.main.transform : null;
        if (p == null || camT == null) return;
        float R = size * 0.5f; var c = new Vector2(x + R, y + R);
        const float worldRange = 30f;
        float scale = R / worldRange;
        float yaw = camT.eulerAngles.y * Mathf.Deg2Rad;
        float cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
        var pp = p.transform.position;
        Vector2 ToMap(Vector3 world, bool clamp, out bool inside)
        {
            var d = world - pp;
            float right = d.x * cs - d.z * sn, fwd = d.x * sn + d.z * cs;
            var v = new Vector2(right, -fwd) * scale;
            inside = v.magnitude <= R * 0.9f;
            if (!inside && clamp) v = v.normalized * R * 0.88f;
            return c + v;
        }

        Tex(new Rect(x - 4, y - 4, size + 8, size + 8), disc, new Color(0, 0, 0, 0.55f));
        Color tint = RealmColor[SelectedMap];
        Tex(new Rect(x, y, size, size), disc, new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.25f, 0.7f));
        Tex(new Rect(x + R * 0.5f, y + R * 0.5f, R, R), ring, new Color(1, 1, 1, 0.12f));

        // arena wall
        for (int i = 0; i < 72; i++)
        {
            float a = i / 72f * Mathf.PI * 2f;
            var q = ToMap(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 23f, false, out bool inside);
            if (inside) Box(new Rect(q.x - 1.5f, q.y - 1.5f, 3, 3), new Color(1f, 0.85f, 0.6f, 0.55f));
        }
        // health orbs
        foreach (var o in HealthOrb.All)
        {
            if (o == null) continue;
            var q = ToMap(o.transform.position, true, out _);
            Tex(new Rect(q.x - 4, q.y - 4, 8, 8), disc, new Color(0.3f, 1f, 0.4f));
        }
        // enemies
        foreach (var e in Enemy.All)
        {
            if (e == null) continue;
            var q = ToMap(e.transform.position, true, out bool inside);
            float s = e.type == EnemyType.Boss ? 16f : e.type == EnemyType.Brute ? 11f : 8f;
            Color col = e.type == EnemyType.Boss ? new Color(0.8f, 0.4f, 1f) : e.type == EnemyType.Caster ? new Color(1f, 0.4f, 0.8f) : new Color(1f, 0.2f, 0.15f);
            if (!inside) col.a = 0.7f;
            Tex(new Rect(q.x - s * 0.5f - 1, q.y - s * 0.5f - 1, s + 2, s + 2), disc, new Color(0, 0, 0, 0.8f));
            Tex(new Rect(q.x - s * 0.5f, q.y - s * 0.5f, s, s), disc, col);
        }
        // north marker
        {
            var n = ToMap(pp + Vector3.forward * 100f, true, out _);
            Shadowed(new Rect(n.x - 10, n.y - 11, 20, 22), "N", new GUIStyle(center) { fontSize = 14, fontStyle = FontStyle.Bold }, new Color(1f, 0.85f, 0.5f));
        }
        // player arrow (rotates with the hero relative to the camera)
        float rel = p.transform.eulerAngles.y - camT.eulerAngles.y;
        GUI.matrix = baseM * Matrix4x4.TRS(new Vector3(c.x, c.y, 0), Quaternion.Euler(0, 0, rel), Vector3.one);
        Tex(new Rect(-9, -10, 18, 20), arrow, new Color(0, 0, 0, 0.8f));
        Tex(new Rect(-7, -8, 14, 16), arrow, p.Hero.accent);
        GUI.matrix = baseM;

        Tex(new Rect(x - 2, y - 2, size + 4, size + 4), ring, new Color(tint.r, tint.g, tint.b, 0.9f));
        Shadowed(new Rect(x - 20, y + size + 4, size + 40, 22), Map.name, new GUIStyle(center) { fontSize = 13, fontStyle = FontStyle.Bold }, tint);
        string obj = phase == Phase.Tutorial ? "OBJECTIVE: Complete your training"
            : phase == Phase.Fighting || phase == Phase.Countdown
            ? (waveIndex >= Waves.Length - 1 ? "OBJECTIVE: Slay " + BossDefs.Names[Map.bossKind].Split(',')[0] : $"OBJECTIVE: Survive wave {Mathf.Max(1, waveIndex + 1)} of {Waves.Length}")
            : "";
        Shadowed(new Rect(x - 20, y + size + 22, 360, 22), obj, new GUIStyle(small) { fontSize = 13 }, new Color(1, 1, 1, 0.8f));
    }

    // ---------------------------------------------------------------- pause
    void DrawPause(float w, float h)
    {
        Box(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.8f));
        Tex(new Rect(0, 0, w * 0.55f, h), gradL, new Color(1, 1, 1, 0.8f));
        float x = 90;
        Glow(new Rect(x, h * 0.12f, 600, 90), "PAUSED", new GUIStyle(big) { alignment = TextAnchor.MiddleLeft, fontSize = 72 }, new Color(1f, 0.55f, 0.2f));
        Shadowed(new Rect(x + 4, h * 0.12f + 84, 700, 26), $"{Map.name}  ·  Wave {Mathf.Max(1, waveIndex + 1)} / {Waves.Length}  ·  Score {score:N0}", small, new Color(1, 1, 1, 0.7f));

        if (pauseControls) { DrawControlsPanel(x, h * 0.36f); Shadowed(new Rect(x, h - 60, 600, 30), "ESC / ENTER  back", small, new Color(1, 1, 1, 0.6f)); return; }

        float y = h * 0.36f;
        for (int i = 0; i < PauseItems.Length; i++)
        {
            var r = new Rect(x, y + i * 58, 440, 50);
            if (MouseMoved && Hover(r)) pauseIndex = i;
            bool sel = i == pauseIndex;
            if (sel)
            {
                Tex(new Rect(r.x - 30, r.y, 520, r.height), gradL, new Color(0.9f, 0.35f, 0.08f, 0.55f));
                Box(new Rect(r.x - 30, r.y + 6, 5, r.height - 12), new Color(1f, 0.6f, 0.2f));
            }
            Shadowed(new Rect(r.x + (sel ? 14 : 0), r.y, r.width, r.height), PauseItems[i], menuItem, sel ? Color.white : new Color(1, 1, 1, 0.55f));
            if (Clicked(r)) { pauseIndex = i; ChoosePause(i); }
        }
        // mini world map on the right shows where you are in the journey
        float mw = Mathf.Min(w * 0.4f, 620f), mh = mw * 0.5625f;
        DrawWorldMap(new Rect(w - mw - 70, h * 0.3f, mw, mh), storyMode ? SelectedMap : SavedChapter, SelectedMap);
        Shadowed(new Rect(x, h - 60, 700, 30), "ESC  resume     W / S  select     ENTER  confirm", small, new Color(1, 1, 1, 0.5f));
    }

    void DrawHelp(float x, float y, bool backdrop)
    {
        if (backdrop) Box(new Rect(x - 12, y - 10, 470, 300), new Color(0, 0, 0, 0.55f));
        GUI.Label(new Rect(x, y, 460, 300),
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
            "Esc  pause menu    H  toggle this help", small);
    }
}
