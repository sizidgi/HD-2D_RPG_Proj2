using UnityEngine;

/// <summary>
/// 状態異常付与の成功率計算
/// 公式: (使用者INT - 対象INT) × 5 + 使用者Lv %
/// </summary>
public static class StatusEffectCalculator
{
    public static float CalculateSuccessPercent(Character caster, Character target)
    {
        if (caster == null || target == null)
        {
            return 0f;
        }

        int casterLevel = Mathf.Max(1, caster.level);
        return Mathf.Clamp((caster.Int - target.Int) * 5f + casterLevel, 0f, 100f);
    }

    public static bool RollStatusEffect(Character caster, Character target)
    {
        float percent = CalculateSuccessPercent(caster, target);
        return Random.value < percent / 100f;
    }

    public static bool IsOnHitStatusDebuff(BuffBase buff)
    {
        if (buff == null)
        {
            return false;
        }

        switch (buff.statusEffect)
        {
            case StatusEffect.Poison:
            case StatusEffect.Stun:
            case StatusEffect.Burn:
            case StatusEffect.Freeze:
            case StatusEffect.Sleep:
            case StatusEffect.Silent:
            case StatusEffect.SpdDown:
            case StatusEffect.MagicDamageDown:
            case StatusEffect.Makituki:
            case StatusEffect.LockIn:
                return true;
            default:
                return false;
        }
    }

    public static bool IsActionBlockingDebuff(BuffBase buff)
    {
        if (buff == null)
        {
            return false;
        }

        switch (buff.statusEffect)
        {
            case StatusEffect.Freeze:
            case StatusEffect.Stun:
            case StatusEffect.Sleep:
                return true;
            default:
                return false;
        }
    }
}
