using UnityEngine;
using Cinemachine;
using System;
using Invector.vCharacterController;

public class Custom2DLockOn : MonoBehaviour {
    
    [Header("Cinemachine References")]
    public CinemachineVirtualCamera normalLockOnCamera; // ザコ敵用の固定視点カメラ 
    //public CinemachineVirtualCamera bossLockOnCamera; // ボス戦用の引き・低視点カメラ 
    public CinemachineTargetGroup targetGroup;          // プレイヤーと敵を登録するグループ 

    [Header("Settings")]
    public string enemyTag = "Enemy";
    public string bossTag = "Boss";                     // ★ボス識別用のタグ 
    public float lockOnRange = 30f;                     // ボス用に少し長めに設定 
    public float lockOnRangeNormal = 15f;

    [Header("LockOn Inputs")]
    public GenericInput lockOnInput = new GenericInput("Tab", "RightStickClick", "RightStickClick");
    private Transform currentTarget;
    private bool isLockedOn = false;
    private bool isBossBattle = false;

    private Transform playerTransform;
    private vThirdPersonInput tpInput;

    private void Start() {
        playerTransform = this.transform; 

        // 最初はロックオンカメラをオフにしておく(通常カメラの優先度より低くする) 
        if (normalLockOnCamera != null) normalLockOnCamera.Priority = 0;
        //if (bossLockOnCamera != null) bossLockOnCamera.Priority = 0;
        
        // プレイヤー自身に付いているInvectorの入力スクリプトを取得 
        tpInput = GetComponent<vThirdPersonInput>();

        if (tpInput == null) { Debug.LogError("プレイヤーに vThirdPersonInput が見つかりません!"); }
    }

    private void Update() {
        HandleInput();      // ターゲットが離れすぎたら自動解除 ロックオン中の距離・死亡チェック 

        if (isLockedOn && currentTarget != null) {
            float distane = Vector3.Distance(playerTransform.position, currentTarget.position);
            float maxRange = isBossBattle ? lockOnRange : lockOnRangeNormal;
            if (distane > lockOnRangeNormal) {
                RemoveLockOn();
                return;
            }

            // --- Invector連動:毎フレーム、敵の方向を強制的に向かせる --- 
            if (tpInput != null && tpInput.cc != null) {
                tpInput.cc.lockRotation = true;     //インベクターの標準回転を禁止するフラグ
                // プレイヤーから敵への方向(ベクトル)を計算する
                // 上下の傾き(高低差)でプレイヤーが傾かないようにY軸をゼロにする 
                Vector3 directionToEnemy = currentTarget.position - playerTransform.position;
                directionToEnemy.y = 0;

                if (directionToEnemy != Vector3.zero) {
                    // Invectorの移動スクリプトに対して、敵の方向を向くように指示する
                    tpInput.cc.RotateToDirection(directionToEnemy);
                }
            }
        }
    }

    void HandleInput() {
        if (lockOnInput.GetButtonDown()) // 変更されたインプット処理 
            { 
            if (isLockedOn) RemoveLockOn();
            else TryLockOn();
        }
    }
    private void TryLockOn() { // 一番近い敵を探索する自作ロジック(省略) 
        Transform nearestEnemy = FindNearestEnemy();
        if (nearestEnemy != null) {
            currentTarget = nearestEnemy;
            isLockedOn = true;
            // タグでボスかどうかを判定 
            isBossBattle = currentTarget.CompareTag(bossTag);
            if (isBossBattle) {
                // --- 【ボス戦の場合】 ---
                // TargetGroupにプレイヤーとボスを登録 
                targetGroup.m_Targets = new CinemachineTargetGroup.Target[0];
                // 初期化 
                targetGroup.AddMember(playerTransform, 1f, 1f);
                targetGroup.AddMember(currentTarget, 1f, 1.5f);

                // ボスは少しウエイト(重要度)を高めに 
                // ボス戦専用カメラをONにする 
                //bossLockOnCamera.Priority = 20;
                normalLockOnCamera.Priority = 0;
            } else {
                // --- 【ザコ敵の場合(今までの仕様)】 --- 
                // 通常の固定視点ロックオンカメラをONにする
                normalLockOnCamera.Priority = 20;
                //bossLockOnCamera.Priority = 0;
            }
        }
    }

    private void RemoveLockOn() {
        isLockedOn = false;

        // 敵だけをカメラの追従対象から外す(プレイヤーは残る) 
        //もし将来的に、「プレイヤーは常にカメラが映し続けたまま、倒した敵だけをリストから除外したい」となった場合に 
        //targetGroup.RemoveMember(currentTarget); 
        currentTarget = null;
        isBossBattle = false;

        // 全てのロックオンカメラをオフにして通常視線に戻す 
        if (normalLockOnCamera != null) normalLockOnCamera.Priority = 0;
        //if (bossLockOnCamera != null) bossLockOnCamera.Priority = 0;

        // TargetGroupを空にする
        if (targetGroup != null) targetGroup.m_Targets = new CinemachineTargetGroup.Target[0];
        // ---Invector連動:移動モードを通常(自由方向)に戻す-- - 
        if (tpInput != null && tpInput.cc != null) {
            // ★解除時はInvectorのターゲットも空にする
            tpInput.cc.rotateTarget = null;
        }
    }

    private Transform FindNearestEnemy() {

        // 1. まず周辺にボスがいるか探す(ボス最優先)
        GameObject bossObj = GameObject.FindWithTag(bossTag);
        if (bossObj != null) return bossObj.transform;

        // 2. ボスがいなければザコ敵を探す 
        GameObject enemyObj = GameObject.FindWithTag(enemyTag);
        return enemyObj != null ? enemyObj.transform : null;
    }
}
