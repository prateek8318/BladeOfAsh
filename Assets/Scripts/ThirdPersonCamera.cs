using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5.2f;
    public float height = 2.15f;
    public float shoulder = 0.8f;
    public float sensitivity = 3f;
    public float minPitch = -25f, maxPitch = 60f;

    public float yaw, pitch = 15f;
    float currentDistance;
    float titleBlend = 1f, currentShoulder;
    Vector3 shakeOffset;

    Light keyLight;
    Camera camComp;

    // Cinematic override: GameManager drives these during cutscenes.
    public static bool cineActive, cineCut;
    public static Vector3 cinePos, cineLook;
    public static float cineFov = 50f, cineSmooth = 6f;
    bool wasCine;

    void Start()
    {
        // soft key light that lights the hero's face on menus and cutscenes
        var lg = new GameObject("ShowcaseLight");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0.8f, 1.2f, 0.5f);
        keyLight = lg.AddComponent<Light>();
        keyLight.type = LightType.Point; keyLight.range = 9f; keyLight.color = new Color(1f, 0.88f, 0.75f); keyLight.intensity = 0f;
        currentDistance = distance;
        if (target != null) yaw = target.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (camComp == null) camComp = GetComponent<Camera>();
        if (cineActive)
        {
            float dt = Time.unscaledDeltaTime;
            if (cineCut) { transform.position = cinePos; cineCut = false; }
            else transform.position = Vector3.Lerp(transform.position, cinePos, 1f - Mathf.Exp(-cineSmooth * dt));
            var want = Quaternion.LookRotation((cineLook - transform.position).normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-cineSmooth * 1.4f * dt));
            transform.position += Random.insideUnitSphere * FX.shake * 0.25f;
            if (camComp != null) camComp.fieldOfView = Mathf.Lerp(camComp.fieldOfView, cineFov, 1f - Mathf.Exp(-4f * dt));
            if (keyLight != null) keyLight.intensity = 1.6f;
            wasCine = true;
            return;
        }
        if (wasCine)
        {
            // hand control back smoothly from wherever the cutscene left the camera
            wasCine = false;
            var e = transform.eulerAngles;
            yaw = e.y; pitch = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, minPitch, maxPitch);
            currentDistance = Vector3.Distance(transform.position, target.position + Vector3.up * height);
        }
        if (camComp != null) camComp.fieldOfView = Mathf.Lerp(camComp.fieldOfView, 60f, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));

        var gm = GameManager.I;
        if (gm != null && gm.IsTitle)
        {
            // showcase: face the hero from the front and sway slowly around them
            float front = target.eulerAngles.y + 180f + 18f + Mathf.Sin(Time.unscaledTime * 0.22f) * 14f;
            yaw = Mathf.LerpAngle(yaw, front, Time.unscaledDeltaTime * 2.5f);
            pitch = Mathf.Lerp(pitch, 6f, Time.unscaledDeltaTime * 2f);
        }
        else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0) && Time.timeScale > 0f && (gm == null || gm.InputEnabled))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        var rot = Quaternion.Euler(pitch, yaw, 0f);
        bool title = gm != null && gm.IsTitle;
        titleBlend = Mathf.MoveTowards(titleBlend, title ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
        currentShoulder = Mathf.Lerp(currentShoulder, title ? gm.TitleShoulder : shoulder, Time.unscaledDeltaTime * 3f);
        if (keyLight != null) keyLight.intensity = 2.2f * titleBlend;
        var pivot = target.position + Vector3.up * Mathf.Lerp(height, 1.85f, titleBlend) + rot * Vector3.right * currentShoulder;
        float wanted = Mathf.Lerp(distance, 4.4f, titleBlend);
        if (Physics.SphereCast(pivot, 0.3f, -(rot * Vector3.forward), out var hit, wanted, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerController>() == null
            && hit.collider.GetComponentInParent<Enemy>() == null)
        {
            wanted = Mathf.Max(1.2f, hit.distance - 0.1f);
        }
        currentDistance = Mathf.Lerp(currentDistance, wanted, Time.unscaledDeltaTime * (wanted < currentDistance ? 20f : 4f));

        shakeOffset = Random.insideUnitSphere * FX.shake * 0.35f;
        transform.position = pivot - rot * Vector3.forward * currentDistance + shakeOffset;
        transform.rotation = rot;
    }

    public Vector3 FlatForward
    {
        get { var f = transform.forward; f.y = 0; return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward; }
    }
    public Vector3 FlatRight
    {
        get { var r = transform.right; r.y = 0; return r.sqrMagnitude > 0.001f ? r.normalized : Vector3.right; }
    }
}
