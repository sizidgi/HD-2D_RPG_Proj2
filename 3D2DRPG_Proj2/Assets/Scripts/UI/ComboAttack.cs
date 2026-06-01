using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
public class ComboAttack : MonoBehaviour
{
    //public Animator animator;
    [SerializeField,Header("ComboUI")]
    private TimingUI timingUI;
    [SerializeField] private float timingTime = 0.4f; // タイミング差分
    [SerializeField] private float timingWindowEnd = 0.6f;   // 攻撃中のタイミング受付終了

    private UnityEvent<int> onComboEnd;
    private Func<int, bool> onComboAttack;
    private Character enemy;
    private int comboStep = 0;
    private int maxComboStep = 3; // 最大コンボ数
    private bool canInput = false;
    private float timer = 0f;
    private bool isComboActive = false;

    private void Update()
    {
        // 撃破済み・Destroy 済みのターゲットならコンボを打ち切る
        if (isComboActive && (enemy == null || enemy.hp <= 0))
        {
            EndCombo();
            return;
        }

        // 攻撃中の時間経過を管理
        if (canInput)
        {
            var testing = timingUI.IsTimingSuccess();
            timingUI.SetMarker(timingUI.CheckTimingSuccess());
            // 攻撃入力
            if (testing)
            {
                TryAttack();
            }
            timer += Time.deltaTime;
            //Debug.Log(timer);
            if (timer > timingWindowEnd+timingTime)
            {
                EndCombo(); // タイミングを逃したら終了
            }
        }
    }
    // 攻撃入力を試みる
    private void TryAttack()
    {
        canInput = false;
        //if (!canInput && comboStep == 0)
        //{
        //    // 最初の攻撃
        //    StartAttack(1);
        //}  else 
        //if (canInput)
        //{
        Debug.Log("TryAttack");
        // タイミングよくクリックしたら次の攻撃へ
        if (timingUI.isActive)
        {
            Debug.Log(timingUI.isActive);
            StartCoroutine(NextAttackCoroutine());
        }
        else
        {
            EndCombo();
        }
        //}
    }
    // コンボ攻撃開始
    private void StartAttack(int step)
    {
        Debug.Log($"=== StartAttack() 呼ばれました。Step: {step} ===");
        
        comboStep = step;
        //animator.SetTrigger($"Attack{step}");
        canInput = true;
        timer = 0f;
        
        Debug.Log($"timingUI.Show() を呼び出します。timingTime: {timingTime}, timingWindowEnd: {timingWindowEnd}");
        timingUI.Show(timingTime,timingWindowEnd);
    }
    // 次の攻撃へ
    private void NextAttack()
    {
        comboStep++;
        
        Debug.Log(comboStep);
        if (comboStep >= maxComboStep) // 3段コンボ上限など
        {
            EndCombo();
            return;
        }
        timingUI.Show(timingTime, timingWindowEnd);
        bool EnemySurvival = onComboAttack.Invoke(0);
        if (!EnemySurvival)
        {
            EndCombo();
            return;
        }
        timer = 0f;
        canInput = true;
    }

    [SerializeField, Header("アニメーション間隔")]
    private float attackAnimationDuration = 0.8f;

    private IEnumerator NextAttackCoroutine()
    {
        comboStep++;

        Debug.Log($"[ComboAttack] NextAttack: comboStep={comboStep}, maxComboStep={maxComboStep}");

        canInput = false; // 攻撃入力を無効化

        // 攻撃開始
        bool EnemySurvival = onComboAttack.Invoke(0);

        if (!EnemySurvival)
        {
            EndCombo();
            yield break;
        }

        // アニメーション完了待ち
        yield return new WaitForSeconds(attackAnimationDuration);

        // 待機中に撃破された場合
        if (!isComboActive || enemy == null || enemy.hp <= 0)
        {
            EndCombo();
            yield break;
        }

        // 最大コンボ数に達したら終了
        if (comboStep >= maxComboStep)
        {
            EndCombo();
            yield break;
        }

        // 次の攻撃UIを表示
        timingUI.Show(timingTime, timingWindowEnd);
        timer = 0f;
        canInput = true;
    }


    /// <summary>
    /// 撃破など外部要因でコンボを終了（コールバック中の StopAllCoroutines 回避のため次フレーム実行）
    /// </summary>
    public void RequestEndCombo()
    {
        if (!isComboActive) return;
        StartCoroutine(EndComboDeferred());
    }

    private IEnumerator EndComboDeferred()
    {
        yield return null;
        EndCombo();
    }

    private void EndCombo()
    {
        if (!isComboActive) return;
        isComboActive = false;
        StopAllCoroutines();
        timingUI.Hide();
        comboStep = 1;
        onComboEnd.Invoke(0);
        canInput = false;
        timer = 0f;
    }

    // タイミングの設定
    public void AttackTiming(float _timing , float _AttackEnd)
    {
          timingTime = _timing;
       timingWindowEnd = _AttackEnd;

    }
    public void Inputs(Func<int,bool> _attackEvent, UnityEvent<int> _attackEnd, int _maxcombo, Character enemies)
    {
        Debug.Log($"=== ComboAttack.Inputs() 呼ばれました。MaxCombo: {_maxcombo} ===");
        
        onComboEnd = _attackEnd;
        onComboAttack = _attackEvent;
        enemy = enemies;
        maxComboStep = _maxcombo;
        
        if (timingUI == null)
        {
            Debug.LogError("ComboAttack: timingUI が null です！Inspectorで設定してください。");
            return;
        }

        isComboActive = true;
        StartAttack(0);
    }
}
