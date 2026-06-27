using UnityEngine;

/// <summary>
/// フリーズ（凍結）デバフ
/// 付与された対象は次の自身のターン開始時に1回行動をスキップする
/// </summary>
[CreateAssetMenu(menuName = "RPG/Buffs/Freeze")]
public class Freeze : BuffBase
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(buffId))
        {
            buffId = System.Guid.NewGuid().ToString();
        }
        if (string.IsNullOrEmpty(buffName))
        {
            buffName = "フリーズ";
        }

        statusEffect = StatusEffect.Freeze;
    }

    public override void Apply(Character target)
    {
        if (target == null)
        {
            Debug.LogWarning("Freeze適用失敗: ターゲットがnullです");
            return;
        }

        Debug.Log($"{target.charactername} にフリーズを付与しました");
    }

    public override void Remove()
    {
        if (sourceCharacter != null)
        {
            Debug.Log($"{sourceCharacter.charactername} からフリーズを解除しました");
        }
    }
}
