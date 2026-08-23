using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//コイン生成マネージャー（オブジェクトプール付き）
public class CoinSpawner : MonoBehaviour
{
    // どこからでも一瞬で呼び出せるようにシングルトン化
    public static CoinSpawner Instance { get; private set; }

    [Header("Coin Prefabs (3種類)")]
    [SerializeField] private CoinItem coinPrefab100; // 100円用Prefab
    [SerializeField] private CoinItem coinPrefab10;  // 10円用Prefab
    [SerializeField] private CoinItem coinPrefab1;   // 1円用Prefab

    [Header("Pool Size per Type")]
    [SerializeField] private int poolSizeEach = 50;   // 同時に画面に出る可能性のある最大コイン数

    // 3つのプールを別々に管理して、適切なコインを取り出す
    private List<CoinItem> pool100 = new List<CoinItem>();
    private List<CoinItem> pool10 = new List<CoinItem>();
    private List<CoinItem> pool1 = new List<CoinItem>();

    private Transform playerTransform;

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
   
    void Start()
    {
        // プレイヤーのTransformを探してキャッシュ（Invectorのタグ等に合わせて適宜変更してください）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        // ゲーム開始時に指定された数だけコインを生成して、非アクティブ状態でプールに眠らせておく
        // 3種類すべてのプールを事前に生成（GC対策）
        GeneratePool(coinPrefab100, pool100, 100);
        GeneratePool(coinPrefab10, pool10, 10);
        GeneratePool(coinPrefab1, pool1, 1);
    }

    private void GeneratePool(CoinItem prefab, List<CoinItem> pool, int val) {
        if (prefab == null) return;
        for (int i = 0; i < poolSizeEach; i++) {
            CoinItem coin = Instantiate(prefab, transform);
            coin.goldAmount = val; // 金額を固定化
            coin.gameObject.SetActive(false);
            pool.Add(coin);
        }
    }

    /// <summary>
    /// 敵が死亡した時などに外部から呼び出すメイン関数
    /// </summary>
    /// <param name="spawnPosition">敵がいた死に場所</param>
    /// <param name="totalRewardGold">ドロップしたい合計金額（例：235）</param>
    public void SpawnTotalGold(Vector3 spawnPosition, int totalRewardGold) {
        if (playerTransform == null || totalRewardGold <= 0) return;

        // 1. 100円コインの枚数を計算
        int count100 = totalRewardGold / 100;
        int remainder = totalRewardGold % 100;

        // 2. 10円コインの枚数を計算
        int count10 = remainder / 10;
        remainder = remainder % 10;

        // 3. 残りが1円コインの枚数
        int count1 = remainder;

        // 各プールから必要な枚数だけドロップさせる
        SpawnFromPool(pool100, count100, coinPrefab100, 100, spawnPosition);
        SpawnFromPool(pool10, count10, coinPrefab10, 10, spawnPosition);
        SpawnFromPool(pool1, count1, coinPrefab1, 1, spawnPosition);
    }

    private void SpawnFromPool(List<CoinItem> pool, int count, CoinItem prefab, int val, Vector3 pos) {
        if (count <= 0) return;

        int activated = 0;
        for (int i = 0; i < pool.Count; i++) {
            if (!pool[i].gameObject.activeInHierarchy) {
                pool[i].Launch(pos, playerTransform);
                activated++;
                if (activated >= count) return;
            }
        }

        // 万が一プールが足りなくなった場合の自動拡張（安全策）
        if (activated < count) {
            int extraNeeded = count - activated;
            for (int i = 0; i < extraNeeded; i++) {
                CoinItem coin = Instantiate(prefab, transform);
                coin.goldAmount = val;
                coin.Launch(pos, playerTransform);
                pool.Add(coin);
            }
        }
    }
}
