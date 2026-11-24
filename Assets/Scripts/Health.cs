using System;
using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public Vector3 source;
    public float knockback;
    public bool heavy;
    public bool fromPlayer;
}

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float current;
    public bool isPlayer;
    public bool invulnerable;
    public float damageTakenMultiplier = 1f;

    public event Action<DamageInfo> Damaged;
    public event Action Died;

    public bool IsDead => current <= 0f;
    public float Fraction => maxHealth > 0 ? current / maxHealth : 0f;

    void Awake() { current = maxHealth; }

    public void SetMax(float value)
    {
        maxHealth = value;
        current = value;
    }

    public bool TakeDamage(DamageInfo info)
    {
        if (IsDead || invulnerable) return false;
        info.amount *= damageTakenMultiplier;
        if (info.amount <= 0f) return false;
        current = Mathf.Max(0f, current - info.amount);
        FX.DamageNumber(transform.position + Vector3.up * (2.2f * transform.lossyScale.y), info.amount, isPlayer);
        Damaged?.Invoke(info);
        if (current <= 0f) Died?.Invoke();
        return true;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        current = Mathf.Min(maxHealth, current + amount);
    }
}
