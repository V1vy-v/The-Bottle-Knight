using GamePlay.Enemy;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackController : MonoBehaviour
{
    [SerializeField]private GameObject AtkObj;
    private EnemyController controller;
    private EnemyAtkDetect atkDetect;
    private void Start()
    {
        controller = this.GetComponent<EnemyController>();
        atkDetect = AtkObj.GetComponent<EnemyAtkDetect>();
        if (atkDetect != null)
            atkDetect.SetAtk(controller.state.baseAtk);
        AtkObj.SetActive(false);
    }

    public void OnAttack()
    {
        AtkObj.SetActive(true);
    }
    public void OnAttackEnd()
    {
        AtkObj.SetActive(false);
    }
}
