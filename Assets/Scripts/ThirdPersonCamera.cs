using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 6.5f;
    public float height = 1.9f;
    public float shoulder = 0.7f;
    public float sensitivity = 3f;
    public float minPitch = -25f, maxPitch = 60f;

    public float yaw, pitch = 15f;
    float currentDistance;
    Vector3 shakeOffset;

    void Start()
    {
        currentDistance = distance;
        if (target != null) yaw = target.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetKeyDown(KeyCode.Escape) && (GameManager.I == null || !GameManager.I.IsTitle))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
        var gm = GameManager.I;
        if (gm != null && gm.IsTitle)
        {
            // slow showcase orbit around the hero on the menu
            yaw += Time.unscaledDeltaTime * 9f;
            pitch = Mathf.Lerp(pitch, 8f, Time.unscaledDeltaTime * 2f);
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
        var pivot = target.position + Vector3.up * height + rot * Vector3.right * shoulder;
        float wanted = distance;
        if (Physics.SphereCast(pivot, 0.3f, -(rot * Vector3.forward), out var hit, distance, ~0, QueryTriggerInteraction.Ignore)
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
