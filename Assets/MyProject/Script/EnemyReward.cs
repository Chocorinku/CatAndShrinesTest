using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyReward : MonoBehaviour
{
    [Header("Reward Settings")]
    [Tooltip("この敵が死亡したときにドロップする合計金額")]
    [SerializeField] private int rewardGold = 15;

    /// <summary>
    /// Invectorの死亡イベント（vOnDead）から呼び出すための関数
    /// </summary>
    public void DropRewardGold() {
        // 先ほど作った CoinSpawner に、自分の位置と金額を伝えるだけ（最速処理）
        // 敵の足元（transform.position）から少し浮かせたい場合は + Vector3.up などを足してください
        CoinSpawner.Instance.SpawnTotalGold(transform.position + Vector3.up, rewardGold);
    }
}
