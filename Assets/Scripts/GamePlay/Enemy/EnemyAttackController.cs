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
        controller = GetComponent<EnemyController>();
        atkDetect = AtkObj != null ? AtkObj.GetComponent<EnemyAtkDetect>() : null;

        if (atkDetect != null && controller != null)
            atkDetect.SetAtk(controller.state.baseAtk);

        if (AtkObj != null) AtkObj.SetActive(false);
    }

    public void OnAttack()
    {
        if (AtkObj != null) AtkObj.SetActive(true);
        print("怪物攻击");
        // 兜底：AtkObj 上的碰撞体是 21×1 的细带，玩家站在地面上时基本碰不到，
        // 所以出手这一帧按距离直接结算一次伤害（EnemyAtkDetect 会避开同一帧的重复结算）
        controller?.TryDealDirectHit();
    }
    public void OnAttackEnd()
    {
        AtkObj.SetActive(false);
    }
}
