using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UITest : MonoBehaviour
{
    private List<Character> character = new List<Character>();
    private int index = 0;
    private int maxIndex = 0;
    private bool inputFlag = false;
    [SerializeField, Header("敵の頭上に出すクリスタルUI")]
    private GameObject EnemyAttakPointUI;
    public void Inputs(UnityEvent<int> unityEvent, int i, List<Character> Enemys)
    {
        index = 0;
        maxIndex = i;
        character.Clear();
        character.AddRange(Enemys);
        EnemyAttakPointUI.SetActive(true);
        EnemyAttakPointUI.transform.position = character[index].CharacterObj.transform.position + new Vector3(0, 2, 0);
        Debug.Log(i);
        inputFlag = true;
        StartCoroutine(EventCoroutines(unityEvent, i));
    }

    /// <summary>
    /// 座標リストから対象選択（蘇生対象など Character が存在しない場合）
    /// </summary>
    public void InputsAtPositions(UnityEvent<int> unityEvent, List<Vector3> positions)
    {
        if (positions == null || positions.Count == 0) return;

        index = 0;
        maxIndex = positions.Count - 1;
        character.Clear();
        EnemyAttakPointUI.SetActive(true);
        EnemyAttakPointUI.transform.position = positions[index] + new Vector3(0, 2, 0);
        inputFlag = true;
        StartCoroutine(EventCoroutinesAtPositions(unityEvent, positions));
    }

    private IEnumerator EventCoroutinesAtPositions(UnityEvent<int> unityEvent, List<Vector3> positions)
    {
        while (true)
        {
            yield return null;

            if (Input.GetKeyDown(KeyCode.W) && inputFlag || Input.GetKeyDown(KeyCode.UpArrow) && inputFlag)
            {
                inputFlag = false;
                index += 1;
                if (index > maxIndex) index = 0;
                if (index < 0) index = maxIndex;
                EnemyAttakPointUI.transform.position = positions[index] + new Vector3(0, 2, 0);
                inputFlag = true;
            }
            if (Input.GetKeyDown(KeyCode.S) && inputFlag || Input.GetKeyDown(KeyCode.DownArrow) && inputFlag)
            {
                inputFlag = false;
                index -= 1;
                if (index < 0) index = maxIndex;
                if (index > maxIndex) index = 0;
                EnemyAttakPointUI.transform.position = positions[index] + new Vector3(0, 2, 0);
                inputFlag = true;
            }
            if (Input.GetKeyDown(KeyCode.Space) && inputFlag || Input.GetKeyDown(KeyCode.Return) && inputFlag)
            {
                inputFlag = false;
                unityEvent.Invoke(index);
                EnemyAttakPointUI.SetActive(false);
                break;
            }
        }
    }

    private IEnumerator EventCoroutines(UnityEvent<int> unityEvent, int i)
    {
        while (true)
        {
            yield return null;

            
            if (Input.GetKeyDown(KeyCode.W) && inputFlag || Input.GetKeyDown(KeyCode.UpArrow) && inputFlag)
            {
                inputFlag = false;
                ChengePoint(1);
            }
            if (Input.GetKeyDown(KeyCode.S) && inputFlag || Input.GetKeyDown(KeyCode.DownArrow) && inputFlag)
            {
                inputFlag = false;
                ChengePoint(-1);
            }
            if (Input.GetKeyDown(KeyCode.Space) && inputFlag || Input.GetKeyDown(KeyCode.Return) && inputFlag)
            {
                inputFlag = false;
                unityEvent.Invoke(index);
                EnemyAttakPointUI.SetActive(false);
                break;
            }
        }
    }
    /// <summary>
    /// 対象選択UIを閉じてコルーチンを停止する（キャンセル用）
    /// </summary>
    public void ClosePanel()
    {
        inputFlag = false;
        StopAllCoroutines();
        if (EnemyAttakPointUI != null)
        {
            EnemyAttakPointUI.SetActive(false);
        }
    }

    void ChengePoint(int movepoint)
    {
        index += movepoint;
        if (index == -1)
        {
            index = maxIndex;
        }
        else if (index == maxIndex+1)
        {
            index = 0;
        }
        EnemyAttakPointUI.transform.position = character[index].CharacterObj.transform.position + new Vector3(0, 2, 0);
        inputFlag = true;
    }
}
