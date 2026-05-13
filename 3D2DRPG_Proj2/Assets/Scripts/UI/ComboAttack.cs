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

    private void Update()
    {
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

    // 次の攻撃へ(コルーチンなしの場合)
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
        //animator.SetTrigger($"Attack{comboStep}");
        timer = 0f;
        canInput = true;
    }

    /// <summary>
    /// 攻撃アニメーション待機用のコルーチン
    /// </summary>
    [SerializeField, Header("攻撃アニメーション待機時間")]
    private float attackAnimationDuration = 0.8f;

    private IEnumerator NextAttackCoroutine()
    {
        comboStep++;

        Debug.Log($"[ComboAttack] NextAttack: comboStep={comboStep}, maxComboStep={maxComboStep}");

        canInput = false; // 攻撃中は入力を無効化

        // 攻撃を実行
        bool EnemySurvival = onComboAttack.Invoke(0);

        // アニメーションが終わるまで待機
        yield return new WaitForSeconds(attackAnimationDuration);

        if (!EnemySurvival)
        {
            EndCombo();
            yield break;
        }

        // 最大コンボ数に達したらコンボ終了（攻撃実行後にチェック）
        if (comboStep >= maxComboStep)
        {
            EndCombo();
            yield break;
        }

        // 次のタイミングUIを表示
        timingUI.Show(timingTime, timingWindowEnd);
        timer = 0f;
        canInput = true;
    }


    private void EndCombo()
    {
        timingUI.Hide();
        comboStep = 1;
        onComboEnd.Invoke(0);
        canInput = false;
        timer = 0f;
    }

    // コンボ攻撃の入力設定
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
        
        StartAttack(0);
    }
}
