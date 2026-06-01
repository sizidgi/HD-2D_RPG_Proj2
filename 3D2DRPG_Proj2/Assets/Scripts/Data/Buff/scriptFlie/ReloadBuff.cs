using UnityEngine;

/// <summary>
/// リロードバフ
/// 次の通常攻撃が全体攻撃になり、最終ダメージが上昇する（攻撃後に消費）
/// </summary>
[CreateAssetMenu(menuName = "RPG/Buffs/Reload")]
public class ReloadBuff : BuffBase
{
    public const string NormalAttackSkillName = "通常攻撃";

    [Header("最終ダメージ倍率")]
    [Tooltip("最終ダメージを何倍にするか（例: 1.5 = 50%上昇）")]
    public float damageMultiplier = 1.5f;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(buffId))
        {
            buffId = System.Guid.NewGuid().ToString();
        }
        if (string.IsNullOrEmpty(buffName))
        {
            buffName = "リロード";
        }
    }

    public override void Apply(Character target)
    {
        if (target == null)
        {
            Debug.LogWarning("ReloadBuff適用失敗: ターゲットがnullです");
            return;
        }

        sourceCharacter = target;

        var buffManager = target.GetBuffManager();
        if (buffManager != null)
        {
            buffManager.OnReloadBuffApplied();
        }

        Debug.Log($"{target.charactername} にリロードバフを適用しました");
    }

    public override void Remove()
    {
        if (sourceCharacter != null)
        {
            Debug.Log($"{sourceCharacter.charactername} からリロードバフを解除しました");
        }
    }
}
