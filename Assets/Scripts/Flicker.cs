using UnityEngine;

public class Flicker : MonoBehaviour
{
    public float baseIntensity = 2.5f;
    public float amount = 0.6f;
    Light l;
    float seed;

    void Awake() { l = GetComponent<Light>(); seed = Random.value * 100f; }

    void Update()
    {
        if (l == null) return;
        l.intensity = baseIntensity + (Mathf.PerlinNoise(seed, Time.time * 6f) - 0.5f) * 2f * amount;
    }
}
