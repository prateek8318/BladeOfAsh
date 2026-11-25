using System;
using System.IO;
using UnityEngine;

// Launch the game with:  -autotest -shotdir <folder>
// Skips the title, aims the camera at the nearest enemy, fights automatically,
// saves screenshots and quits after ~45 seconds. Used to smoke-test builds.
public class AutoTest : MonoBehaviour
{
    public static bool Enabled
    {
        get { foreach (var a in Environment.GetCommandLineArgs()) if (a == "-autotest") return true; return false; }
    }

    string shotDir;
    float t;
    float nextAction;
    int shot;
    float duration = 45f;
    float nextShot = 4f;

    void Start()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) { if (args[i] == "-shotdir") shotDir = args[i + 1]; if (args[i] == "-testseconds") float.TryParse(args[i + 1], out duration); }
        if (string.IsNullOrEmpty(shotDir)) shotDir = Application.persistentDataPath;
        Directory.CreateDirectory(shotDir);
        Debug.Log("[AutoTest] running, screenshots -> " + shotDir);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        var p = PlayerController.I;
        var cam = FindAnyObjectByType<ThirdPersonCamera>();
        if (p != null && cam != null && !p.health.IsDead && GameManager.I != null && GameManager.I.InputEnabled)
        {
            Enemy nearest = null; float best = float.MaxValue;
            foreach (var e in Enemy.All)
            {
                float d = Vector3.Distance(e.transform.position, p.transform.position);
                if (d < best) { best = d; nearest = e; }
            }
            if (nearest != null)
            {
                var to = nearest.transform.position - p.transform.position; to.y = 0;
                float yaw = Quaternion.LookRotation(to).eulerAngles.y;
                cam.yaw = Mathf.LerpAngle(cam.yaw, yaw, Time.unscaledDeltaTime * 4f);
                cam.pitch = 18f;
                if (Time.time >= nextAction)
                {
                    int kind = best > 7f ? 2 : (UnityEngine.Random.value < 0.15f ? 1 : 0);
                    if (UnityEngine.Random.value < 0.03f) kind = 4; // switch weapon now and then
                    if (p.rage >= p.maxRage) kind = 3;
                    if (p.ExecutableTarget() != null) kind = 5;
                    p.AutoAction(kind);
                    nextAction = Time.time + 0.3f;
                }
            }
        }

        if (t >= nextShot)
        {
            var path = Path.Combine(shotDir, $"shot_{shot}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[AutoTest] screenshot {path}  enemies={Enemy.All.Count} hp={(p != null ? p.health.current : -1)} combo={(p != null ? p.Combo : 0)} weapon={(p != null ? p.Weapon.ToString() : "-")}");
            shot++;
            nextShot += duration > 60f ? 12f : 6f;
        }
        if (t > duration)
        {
            Debug.Log("[AutoTest] done");
            Application.Quit();
        }
    }
}
