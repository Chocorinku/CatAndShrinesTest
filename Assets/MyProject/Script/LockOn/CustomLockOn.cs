using UnityEngine;
using Cinemachine;
using System;
using Invector.vCharacterController;
using Invector;
using System.Collections.Generic;

public class CustomLockOn : MonoBehaviour
{
    /*
    [Header("Cinemachine References")]
    public CinemachineVirtualCamera normalLockOnCamera; // ザコ敵用の固定視点カメラ
    public CinemachineVirtualCamera bossLockOnCamera;   // ボス戦用の引き・低視点カメラ
    public CinemachineTargetGroup targetGroup;      // プレイヤーと敵を登録するグループ
    */
    [Header("Settings")]
    public string enemyTag = "Enemy";
    //public string bossTag = "Boss";         // ★ボス識別用のタグ
    //public float searchRadiusBoss = 30f;         // ボス用に少し長めに設定
    public float searchRadiusNormal = 15f;   //雑魚敵の探索半径

    [Header("LockOn Inputs")]
    public GenericInput lockOnInput = new GenericInput("Tab", "RightStickClick", "RightStickClick");

    // 内部管理用変数
    private Transform currentTarget;
    private bool isLockedOn = false;
    //private bool isBossBattle = false;
    private Transform playerTransform;
    private vThirdPersonInput tpInput;
    private Rigidbody rb;

    private void Start() {
        playerTransform = this.transform;
        rb = GetComponent<Rigidbody>();

        // 最初はロックオンカメラをオフにしておく（通常カメラの優先度より低くする）
        //if (normalLockOnCamera != null) normalLockOnCamera.Priority = 0;
        //if (bossLockOnCamera != null) bossLockOnCamera.Priority = 0;

        // プレイヤー自身に付いているInvectorの入力スクリプトを取得
        tpInput = GetComponent<vThirdPersonInput>();
        if (tpInput == null) {
            Debug.LogError("プレイヤーに vThirdPersonInput が見つかりません！");
        }
    }
    private void Update() {
        HandleInput();

        //毎フレームのターゲット生存・距離チェック
        if (isLockedOn && currentTarget != null) {
            CheckTargetStatus();
        }
    }

    private void FixedUpdate() {

        // 【最重要・ガクガク解消】
        // 物理演算フレーム（FixedUpdate）のタイミングでInvectorの内部変数へターゲットを通知
        if (isLockedOn && currentTarget != null && tpInput != null && tpInput.cc != null) {
            // 【重要】インベクターのカメラ連動回転ロジックを眠らせるため、あえて null にする
            tpInput.cc.rotateTarget = null;
            //tpInput.cc.rotateTarget = currentTarget;

            // カニ歩きフラグを毎フレーム強制維持し、Animatorのブレを防ぐ（vLockOnから抽出）
            //tpInput.cc.locomotionType = vThirdPersonMotor.LocomotionType.OnlyStrafe;
            tpInput.cc.lockInStrafe = true;
            tpInput.cc.isStrafing = true;
            tpInput.cc.lockRotation = true;     //インベクターの標準回転を禁止するフラグ

            // 【ガクガク＆向き迷子の完全解消】
            // インベクターの物理移動が計算された直後に、Rigidbodyに対して直接「敵の方向への回転」を適用
            // カメラの向き（Camera.main）を一切参照しないため、カメラがフリーズするのを100%防ぎます
            Vector3 directionToEnemy = currentTarget.position - playerTransform.position;

            directionToEnemy.y = 0f; // 上下の傾きによるキャラクターの傾きを防止
            if (directionToEnemy.sqrMagnitude > 0.001f) {
                
                // インベクターの移動速度や回転速度に負けないよう、物理演算で滑らかに（かつ確実に）向きを固定
                // 回転速度（15fなど）は必要に応じて調整してください
                // Rigidbodyでの滑らかな敵への自動回転
                Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 15f * Time.fixedDeltaTime));
            }
        }
    }
    /*
        // ターゲットが離れすぎたら自動解除 ロックオン中の距離・死亡チェック
        if (isLockedOn && currentTarget != null) {
            float distane = Vector3.Distance(playerTransform.position, currentTarget.position);
            float maxRange = isBossBattle ? lockOnRange : lockOnRangeNormal;

            if (distane > lockOnRangeNormal) {
                RemoveLockOn();
                return;
            }

            // --- Invector連動：毎フレーム、敵の方向を強制的に向かせる ---
            if (tpInput != null && tpInput.cc != null) {

                // プレイヤーから敵への方向（ベクトル）を計算する
                Vector3 directionToEnemy = currentTarget.position - playerTransform.position;
                directionToEnemy.y = 0; // 上下の傾き（高低差）でプレイヤーが傾かないようにY軸をゼロにする

                if (directionToEnemy != Vector3.zero) {
                    // Invectorの移動スクリプトに対して、敵の方向を向くように指示する
                    tpInput.cc.RotateToDirection(directionToEnemy);
                }
            }
        }
        */

    void HandleInput() {
        if (lockOnInput.GetButtonDown()) 
        {
            if (isLockedOn) RemoveLockOn();
            else TryLockOn();
        }
    }

    private void TryLockOn() {
        
        // 一番近い敵を探索する自作ロジック（省略）
        Transform bestTarget = FindNearestEnemy();

        // 【改善】敵が1体もいない（null）の場合は、ロックオンを有効にせず処理を抜ける
        if (bestTarget == null) {
            return;
        }
        currentTarget = bestTarget;
        isLockedOn = true;
        /*
         // ★【ボス識別】ターゲットがボスかどうかをタグで判定
        isBossBattle = currentTarget.CompareTag(bossTag);

        if (isBossBattle)
        {
            // ★【将来用】ボス戦時の特殊な処理（ステージ側のボス専用カメラの起動イベントなど）をここに記述
            Debug.Log("ボス戦ロックオン開始");
        }
         */
    }

    private void RemoveLockOn() {
        isLockedOn = false;
        currentTarget = null;
        //isBossBattle = false;

        // --- Invector連動：カニ歩きモードを安全に完全クリア ---
        if (tpInput != null && tpInput.cc != null) {
            tpInput.cc.rotateTarget = null;
            tpInput.cc.lockInStrafe = false;
            tpInput.cc.isStrafing = false;
            // ロックオン解除時、インベクター標準の「自由移動（ Free Direction ）」モードに戻す
            tpInput.cc.locomotionType = vThirdPersonMotor.LocomotionType.OnlyFree;
        }
    }

    /// <summary>
    /// 2.5D視点の画面内において、最も中央（プレイヤー付近）に近い最適な敵を割り出す
    /// </summary>
    private Transform FindNearestEnemy() {
        // シーン内のすべての敵を取得
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        //GameObject[] bosses = GameObject.FindGameObjectsWithTag(bossTag); // ★ボス一括取得

        Transform nearest = null;
        float closestDistance = float.MaxValue;
        /*
        // 1. 【ボス最優先探索】周辺にボスがいれば、距離に関わらず最優先でターゲットにする
        foreach (var boss in bosses) {
            if (boss == null || !IsTargetAlive(boss.transform)) continue;

            float distance = Vector3.Distance(playerTransform.position, boss.transform.position);
            if (distance < searchRadiusBoss && distance < closestDistance) {
                closestDistance = distance;
                nearest = boss.transform;
            }
        }
        */

        if (nearest == null) {
            foreach (var enemy in enemies) {
                // タグが一致しない、または死亡している敵はスキップ
                if (enemy == null || !IsTargetAlive(enemy.transform)) continue;

                // プレイヤーと敵の直線距離を計算
                float distance = Vector3.Distance(playerTransform.position, enemy.transform.position);

                if (distance < searchRadiusNormal && distance < closestDistance) {
                    closestDistance = distance;
                    nearest = enemy.transform;
                }
            }
        }
        return nearest;
    }

    /// <summary>
    /// ターゲットが離れすぎたか、または死亡したかをチェックする
    /// </summary>
    private void CheckTargetStatus() {
        // 死亡チェック（インベクターのvIHealthController、または通常の生存確認）
        if (!IsTargetAlive(currentTarget)) {
            RemoveLockOn();
            return;
        }

        // 距離チェック
        float distance = Vector3.Distance(playerTransform.position, currentTarget.position);
        //float maxRange = isBossBattle ? searchRadiusBoss : searchRadiusNormal;

        //if (distance > maxRange)
        if (distance > searchRadiusNormal) {
            RemoveLockOn();
        }
    }

    /// <summary>
    /// 対象のキャラクターが生存しているか判定（Invectorの体力コンポーネントに対応）
    /// </summary>
    private bool IsTargetAlive(Transform targetTransform) {
        if (targetTransform == null) return false;

        // Invector公式の体力インターフェースを取得して確認
        var healthController = targetTransform.GetComponent<vIHealthController>();
        if (healthController != null) {
            return healthController.currentHealth > 0;
        }

        // コンポーネントが無ければオブジェクトのアクティブ状態で判定（安全弁）
        return targetTransform.gameObject.activeInHierarchy;
    }
}
