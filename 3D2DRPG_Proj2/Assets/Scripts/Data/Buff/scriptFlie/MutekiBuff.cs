using UnityEngine;

/// <summary>
/// 無敵バフ（無の創造など）。被ダメージは Character.ResolveIncomingDamage / TakeDamage で0になる。
/// </summary>
[CreateAssetMenu(menuName = "RPG/Buffs/Muteki")]
public class MutekiBuff : BuffBase
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(buffId))
        {
            buffId = System.Guid.NewGuid().ToString();
        }
        if (buffType == BuffType.StatusEnhancement && string.IsNullOrEmpty(buffName))
        {
            buffName = "無敵";
        }
    }

    public override void Apply(Character target)
    {
        if (target == null)
        {
            Debug.LogWarning("MutekiBuff適用失敗: ターゲットがnullです");
            return;
        }

        sourceCharacter = target;
        Debug.Log($"{target.charactername} に無敵バフを適用しました");
    }

    public override void Remove()
    {
        if (sourceCharacter != null)
        {
            Debug.Log($"{sourceCharacter.charactername} の無敵バフを解除しました");
        }
    }
}
