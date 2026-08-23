using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinItem : MonoBehaviour
{
    [Header("Physics Settings (弾け具合)")]
    [Tooltip("コインが飛び出す基本の力。大きくするほど遠くへ跳ねます")]
    public float explodeForce = 3f;

    public float xwardBias = 0.06f;
    public float upwardBias = 2f;
    public float zwardBias = 2f;

    [Header("Settings")]
    public int goldAmount = 10;          // このコイン1枚の価値
    public float magnetRadius = 5f;      // 吸い込みが始まる距離（インスペクターで調整可能）
    public float attractSpeed = 12f;     // 吸い込まれるスピード
    public float dropLockDuration = 0.5f;// 【新設】生まれてから吸い込みを禁止する時間（秒）

    [Header("Physics (飛び散り用)")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider coinCollider; // 自分のコライダーへの参照

    [Header("どれほど近付いたら取得扱いするか")]
    [SerializeField] private float degreOfContact = 0.35f;

    private Transform playerTransform;
    private bool isFlyingToPlayer = false;
    private bool isInitialized = false;
    private float lockTimer = 0f; // 猶予時間カウンター

    private void Awake() {
        if (rb == null) rb = GetComponent<Rigidbody>();
        // もしインスペクターで空欄なら自動取得
        if (coinCollider == null) coinCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// コインが敵などからドロップした瞬間に呼び出す初期化関数（放物線で飛ばす）
    /// </summary>
    public void Launch(Vector3 spawnPosition, Transform player) {
        playerTransform = player;
        transform.position = spawnPosition;
        gameObject.SetActive(true); // プールから取り出して出現
        isFlyingToPlayer = false;
        lockTimer = 0f; // タイマーリセット

        if (coinCollider != null) coinCollider.enabled = true; // 【重要】プールから出た時は当たり判定を復活させる
        
        if (rb != null) {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;

            // 地面を滑り続けないように空気抵抗を少し高めに設定（ブレーキをかける）
            rb.drag = 1.5f;

            // 【2.5D最適化】Z軸（奥行き）への移動や、変な回転を物理的にロックして滑り散らばりを防ぐ
            //rb.constraints = RigidbodyConstraints.FreezePositionZ;

            // インスペクターの upwardBias を使って上方向の力を調整
            Vector3 forceDirection = new Vector3(
                Random.Range(-1.2f, 1.2f + xwardBias),              //6
                Random.Range(1.5f, 1.5f + upwardBias),              //8 ここを可変に
                Random.Range(-0.2f, 0.2f + zwardBias)               //5
            ).normalized;

            // インスペクターの explodeForce を使って全体の威力を調整
            rb.AddForce(forceDirection * Random.Range(explodeForce - 1f, explodeForce + 2f), ForceMode.Impulse);    //10

            // 【計算修正】横への広がりと、上への高さを別々に計算して合算
            // これにより、normalizedによる意図しない大出力化を防ぎます
            //float randomX = Random.Range(-xwardBias, xwardBias);    //2
            //float jumpY = Random.Range(1.8f, 1.8f + upwardBias);    //3
            //float randomZ = Random.Range(-zwardBias, zwardBias);    //1.5

            //Vector3 launchVelocity = new Vector3(randomX, jumpY, randomZ);

            //// AddForceではなく、初期速度（velocity）を直接書き換えることで
            //// 重なりによる物理の誤差を100%排除し、毎回狙った軌道で綺麗に飛ばします
            //rb.velocity = launchVelocity * explodeForce;            //3
        }

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || playerTransform == null) return;

        // クルクル自転処理（吸い込み開始前はいつでも、何があっても絶対に回り続ける）
        if (!isFlyingToPlayer) {
            transform.Rotate(Vector3.up, 120f * Time.deltaTime);
        }

        // 猶予時間をカウントダウン
        if (lockTimer < dropLockDuration) {
            lockTimer += Time.deltaTime;
            return; // 0.5秒経つまでは、以下の吸い込み処理を完全にスキップする
        }

        // プレイヤーの腰のあたり（少し上）を目指して滑らかに移動
        Vector3 targetPosition = playerTransform.position + new Vector3(0, 1f, 0);

        // プレイヤーとの距離の2乗を計算（Vector3.Distance は内部で平方根を計算して重いため、sqrMagnitude で超高速化）
        float distanceSqr = (playerTransform.position - transform.position).sqrMagnitude;
        float magnetRadiusSqr = magnetRadius * magnetRadius;

        // 設定された距離より近づいたら吸い込みモード開始
        if (distanceSqr <= magnetRadiusSqr) {
            isFlyingToPlayer = true;

            // 吸い込み開始と同時に当たり判定を消す！
            // これによりプレイヤーに激突しなくなり、反動が100%消滅します
            if (coinCollider != null) coinCollider.enabled = false;

            if (rb != null) rb.isKinematic = true; // 物理演算（重力など）を切って、綺麗な直線移動にする
        }

        // 吸い込み移動処理
        if (isFlyingToPlayer) {
            
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, attractSpeed * Time.deltaTime);

            // 吸い込まれながらも少し回転させておくと綺麗に見えます
            transform.Rotate(Vector3.up, 360f * Time.deltaTime);

            // ほぼ密着したら回収
            if (Vector3.Distance(transform.position, targetPosition) < degreOfContact) {
                Collect();
            }
        }
    }

    /// <summary>
    /// プレイヤーに回収された時の処理
    /// </summary>
    private void Collect() {

        if (CurrencyManager.Instance != null) {
            // 世界に1つのマネージャーにお金を加算
            CurrencyManager.Instance.AddGold(goldAmount);
            // コンソールウィンドウにログを出して、データが届いているか確認する
            Debug.Log($"コイン回収！所持金は現在: {CurrencyManager.Instance.CurrentGold} GOLD です。");
        } else {
            Debug.LogError("CurrencyManagerがヒエラルキーに見つかりません！");
        }
        
        // 【最重要】Destroyせず、非アクティブにしてプールに戻す準備をする
        if (rb != null) rb.isKinematic = false;
        rb.drag = 0f; // 次回使用のために初期化
        rb.constraints = RigidbodyConstraints.None;
        gameObject.SetActive(false);
    }
}
