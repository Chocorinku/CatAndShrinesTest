using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using Invector.vCharacterController;
using Invector;
using System;

public class CustomLockOn_2 : MonoBehaviour {
    // 敵ごとの離脱タイマーを管理するための構造体（クラスではないのでGCが発生しない）
    private struct TrackedEnemy {
        public Collider collider;
        public float outOfRangeTimer; // 範囲外に出てからの経過時間
    }
    #region
    [Header("Cinemachine References")]
    public CinemachineVirtualCamera normalLockOnCamera; // ザコ敵用の固定視点カメラ
    public CinemachineVirtualCamera bossLockOnCamera;   // ボス戦用の引き・低視点カメラ
    public CinemachineTargetGroup targetGroup;          // プレイヤーと敵を登録するグループ

    [Header("Settings")]
    public string enemyTag = "Enemy";
    public string bossTag = "Boss";
    public float searchRadiusNormal = 15f;              // ザコ敵の探索半径
    public float searchRadiusBoss = 30f;                // ボスの探索半径
    public LayerMask enemyLayer;                        // 敵が所属するレイヤー

    [Header("New Settings (リアルタイムリスト用)")]
    [Tooltip("範囲外に出てからリストから完全に除外されるまでの猶予秒数")]
    public float outOfRangeDuration = 2.0f;

    [Header("LockOn Inputs")]
    public GenericInput lockOnInput = new GenericInput("Tab", "RightStickClick", "RightStickClick");

    // 内部管理用変数
    private bool isLockedOn = false;
    private bool isBossBattle = false;
    private Transform playerTransform;
    private vThirdPersonInput tpInput;
    private Rigidbody rb;
    private int normalCamPriority;
    private int bossCamPriority;

    // 事前に配列を確保（最大検知数分）
    [SerializeField] private Collider[] enemyBuffer = new Collider[100];
    // 【変更】単純なColliderのリストから、タイマー付きの構造体リストに変更
    private List<TrackedEnemy> trackedEnemyList = new List<TrackedEnemy>();
    // 結果を受け取るリスト // インスペクター確認用、あるいは他スクリプト連携用にColliderだけのリストも一応残す（中身は自動更新）
    [SerializeField] private List<Collider> enemyList = new List<Collider>();
    [Header("ロック中の敵")]
    [SerializeField] private Transform currentTarget;
    private vIHealthController targetHealthController; // 負荷軽減のためにキャッシュ用変数を追加
    bool isStickHeld;
    [Header("現在ロックオンしている敵のインデックス")]
    [SerializeField] int lockedEnemyIndex;
    [Header("プレイヤーの向きでロック対象を優先するフラグ")]
    [SerializeField] bool pForwrdLock;

    public RaycastHit groundHit;
    [Range(0, 30)]
    public float slideDownVelocity = 7f;
    [Tooltip("Smooth to slide down the controller")]
    public float slideDownSmooth = 2f;
    [Tooltip("Smooth to rotate the controller")]
    public float rotateDownSlopeSmooth = 8f;


    [Header("LockOn UI Settings")]
    [SerializeField] private GameObject lockOnMarker; // インスペクターからマークのPrefabまたはシーン上のオブジェクトをアサイン
    [SerializeField] private GameObject lockOnModeMarker;
    [SerializeField] private Vector3 markerOffset = new Vector3(0, 2f, 0); // 敵の頭上に表示するためのオフセット値

    [Header("ギズモの表示設定")]
    [SerializeField] bool showGizmos;   //ギズモをオンオフフラグ
    // インスペクターから色と透明度（Alpha値）を変更できるようにします
    [SerializeField] Color domeColor = new Color(1f, 0.92f, 0.016f, 0.2f); // 初期値：薄い黄色
    [SerializeField] Color wireColor = Color.cyan; // 初期値

    #endregion

    private void Start() {
        playerTransform = this.transform;
        rb = GetComponent<Rigidbody>();

        // 初期状態ではロックオンカメラをオフ
        if (normalLockOnCamera != null) {
            normalCamPriority = normalLockOnCamera.Priority;
            if (isBossBattle) normalLockOnCamera.Priority = 0;
        }
        if (bossLockOnCamera != null) {
            bossCamPriority = bossLockOnCamera.Priority;
            bossLockOnCamera.Priority = 0;
        }
        // Invectorのコンポーネントを取得
        tpInput = GetComponent<vThirdPersonInput>();
        if (tpInput == null) {
            Debug.LogError("プレイヤーに vThirdPersonInput が見つかりません！");
        }
    }

    private void Update() {
        HandleInput();

        // 毎フレームのターゲット生存・距離チェック
        if (isLockedOn && currentTarget != null) {
            UpdateTrackedEnemyList();
            CheckTargetStatus();       // 距離チェックの仕様をタイマー連動に変更
        }
        UpdateMarkerPosition();     //敵が移動中も常にマークも一緒に移動するように
    }

    private void FixedUpdate() {
        // tpInput.cc.isSprinting はInvectorがダッシュ中かどうかを判定するフラグです
        bool isSprinting = tpInput != null && tpInput.cc != null && tpInput.cc.isSprinting;
        // 【最重要・ガクガク解消】
        // 物理演算フレーム（FixedUpdate）のタイミングでInvectorの内部変数へターゲットを通知
        if (isLockedOn && currentTarget != null && tpInput != null && tpInput.cc != null) {

            if (isSprinting) {
                tpInput.cc.rotateTarget = null; // ターゲットへ向くのをやめる
                tpInput.cc.lockInStrafe = false;
                tpInput.cc.isStrafing = false;   // カニ歩きアニメーションを止める
                tpInput.cc.lockRotation = false; // 回転ロックを解除（進行方向を向く）
                tpInput.cc.locomotionType = vThirdPersonMotor.LocomotionType.FreeWithStrafe; // 自由移動化
            } else {
                // インベクターの自動回転ロジックに敵のTransformを直接委ねる（競合の完全防止）
                tpInput.cc.rotateTarget = currentTarget;
                // カニ歩きフラグを毎フレーム強制維持し、Animatorのブレを防ぐ（vLockOnから抽出）
                tpInput.cc.lockInStrafe = true;
                tpInput.cc.isStrafing = true;
                tpInput.cc.lockRotation = true;     //インベクターの標準回転を禁止するフラグ

                // 【ガクガク＆向き迷子の完全解消】
                // インベクターの物理移動が計算された直後に、Rigidbodyに対して直接「敵の方向への回転」を適用
                Vector3 directionToEnemy = currentTarget.position - playerTransform.position;
                directionToEnemy.y = 0f; // 上下の傾きによるキャラクターの傾きを防止

                if (directionToEnemy != Vector3.zero) {

                    var normal = currentTarget.position - playerTransform.position;
                    normal.y = 0f;

                    var dir = Vector3.ProjectOnPlane(normal.normalized, groundHit.normal).normalized;
                    dir.y = 0f;

                    Vector3 desiredForward = Vector3.RotateTowards(transform.forward, dir, rotateDownSlopeSmooth * Time.fixedDeltaTime, 0f);
                    Quaternion _newRotation = Quaternion.LookRotation(desiredForward);
                    rb.MoveRotation(_newRotation);

                    //transform.LookAt(directionToEnemy);     //相手の方を常に向く
                }


                if (directionToEnemy != Vector3.zero) {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);
                    //rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 15f * Time.fixedDeltaTime));

                    //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);

                    //rb.MoveRotation(targetRotation);    //vThirdPersonMotorからの

                }

                if (directionToEnemy.sqrMagnitude > 0.001f) {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);

                    // インベクターの移動速度や回転速度に負けないよう、物理演算で滑らかに（かつ確実に）向きを固定
                    // 回転速度（15fなど）は必要に応じて調整してください
                    //rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 15f * Time.fixedDeltaTime));


                    // Invectorの移動スクリプトに対して、敵の方向を向くように指示する
                    //tpInput.cc.RotateToDirection(directionToEnemy);

                    //tpInput.cc.RotateToPosition(currentTarget.position);

                    //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);

                }

            }
            // ==========================================
            // 【共通物理処理】傾斜滑り降り（ダッシュ中・通常時どちらも適用）
            // ==========================================
            // もしダッシュ中にこの物理挙動が不要（通常移動に任せたい）な場合は、
            // この3行を丸ごと上記の「else { ... }」の中に引っ越してください。
            var slopeNormal = currentTarget.position - playerTransform.position;
            slopeNormal.y = 0f;
            var slopeDir = Vector3.ProjectOnPlane(slopeNormal.normalized, groundHit.normal).normalized;
            rb.velocity = Vector3.Lerp(rb.velocity, slopeDir * slideDownVelocity, slideDownSmooth * Time.fixedDeltaTime);

        } else {
            if (tpInput != null && tpInput.cc != null) {
                tpInput.cc.lockRotation = false;
            }
        }
    }

    private void HandleInput() {
        if (lockOnInput.GetButtonDown()) {
            if (isLockedOn) RemoveLockOn();
            else TryLockOn();
        }
        SwitchTarget();
    }

    private void SwitchTarget() {       //ターゲット相手を変える
        float horizontalInput = Input.GetAxis("RightAnalogHorizontal");

        // ロックオン中で、自分以外に切り替え候補（敵）が2匹以上いる場合のみ処理
        if (Mathf.Abs(horizontalInput) > 0.1f && isLockedOn && enemyList.Count > 1) {
            if (isStickHeld) return;
            isStickHeld = true;

            Transform bestNextTarget = null;
            float minAngleDifference = float.MaxValue; // 一番角度が近い敵を探す

            // 基準となる「プレイヤーから現在の敵への方向ベクトル」（水平面にするためyは0）
            Vector3 dirToCurrent = (currentTarget.position - playerTransform.position);
            dirToCurrent.y = 0f;
            dirToCurrent.Normalize();

            // スティックが右（>0.1）なら時計回り、左（<-0.1）なら反時計回り
            bool wantClockwise = horizontalInput > 0.1f;

            // 周囲の敵リストを全スキャンして、次に最適な敵を探す
            for (int i = 0; i < enemyList.Count; i++) {
                Transform candidate = enemyList[i].transform;

                // 今ロックしている敵自身はスキップ
                if (candidate == currentTarget) continue;

                // プレイヤーから候補の敵への方向ベクトル
                Vector3 dirToCandidate = (candidate.position - playerTransform.position);
                dirToCandidate.y = 0f;
                dirToCandidate.Normalize();

                // 現在の敵の方向と、候補の敵の方向の「角度差（0〜180度）」を計算
                float angle = Vector3.Angle(dirToCurrent, dirToCandidate);

                // 外積（Cross）を使って、候補の敵が「右」か「左」のどちらにいるかを特定する
                // プレイヤーの上方向ベクトル（Vector3.up）を軸として外積を計算
                Vector3 cross = Vector3.Cross(dirToCurrent, dirToCandidate);
                bool isClockwise = cross.y > 0f; // yがプラスなら現在の敵より右側（時計回り）

                // 【判定条件】
                // スティックを右に倒したとき：候補の敵が「右側（時計回り）」にいる場合のみ対象
                // スティックを左に倒したとき：候補の敵が「左側（反時計回り）」にいる場合のみ対象
                if (wantClockwise == isClockwise) {
                    // 条件に合う敵の中で、一番「現在の敵からの角度が近い（隣の）敵」を選ぶ
                    if (angle < minAngleDifference) {
                        minAngleDifference = angle;
                        bestNextTarget = candidate;
                    }
                }
            }
            // もし行きたい方向（右/左）に敵が見つからなかった場合の保険処理
            // （例：一番右端の敵をロック中に、さらに右に倒したときは、一番左端の敵にループさせる）
            if (bestNextTarget == null) {
                float maxAngle = -1f;
                for (int i = 0; i < enemyList.Count; i++) {
                    Transform candidate = enemyList[i].transform;
                    if (candidate == currentTarget) continue;

                    Vector3 dirToCandidate = (candidate.position - playerTransform.position);
                    dirToCandidate.y = 0f;
                    float angle = Vector3.Angle(dirToCurrent, dirToCandidate.normalized);

                    // 一番角度が離れている（＝反対側の端っこにいる）敵をセットする
                    if (angle > maxAngle) {
                        maxAngle = angle;
                        bestNextTarget = candidate;
                    }
                }
            }
            // 見つかった次のターゲットを適用
            if (bestNextTarget != null) {
                currentTarget = bestNextTarget;
                // enemyList内での現在のインデックスも同期させておく
                lockedEnemyIndex = enemyList.FindIndex(c => c.transform == currentTarget);
                ApplyTarget();
            }


            //Listバージョン
            //if (horizontalInput > 0.1f) {
            //    // 右に傾けた場合：インデックスを進める（末尾なら0に戻る）
            //    lockedEnemyIndex = (lockedEnemyIndex + 1) % enemyList.Count;

            //} else if (horizontalInput < -0.1f)
            //    lockedEnemyIndex = (lockedEnemyIndex - 1 + enemyList.Count) % enemyList.Count;

            //currentTarget = enemyList[lockedEnemyIndex].transform;
            //ApplyTarget();


        } else if (Mathf.Abs(horizontalInput) <= 0.1f) {
            isStickHeld = false;
        }
    }

    private void ApplyTarget() {
        if (currentTarget != null) {
            isLockedOn = true;

            // タグでボスかどうかを判定
            isBossBattle = currentTarget.CompareTag(bossTag);
            // ロックした瞬間にコンポーネントを一度だけキャッシュ（毎フレームGetComponentを防ぐ）
            targetHealthController = currentTarget.GetComponent<vIHealthController>();

            if (isBossBattle) {
                // --- 【ボス戦の場合のCinemachine制御】 ---
                if (targetGroup != null) {
                    targetGroup.m_Targets = new CinemachineTargetGroup.Target[0]; // 初期化
                    targetGroup.AddMember(playerTransform, 1f, 1f);
                    targetGroup.AddMember(currentTarget, 1f, 1.5f); // ボスは重み高め
                }

                if (bossLockOnCamera != null) bossLockOnCamera.Priority = bossCamPriority;
                if (normalLockOnCamera != null) normalLockOnCamera.Priority = 0;
            } else {
                // --- 【ザコ敵の場合のCinemachine制御】 ---
                if (normalLockOnCamera != null) normalLockOnCamera.Priority = normalCamPriority;
                if (bossLockOnCamera != null) bossLockOnCamera.Priority = 0;
            }

            // ターゲットが決定したらマークを表示する
            if (currentTarget != null) {
                UpdateMarkerPosition();
            }
        }
    }

    private void TryLockOn() {
        // 2.5D画面内から最適な敵を探索（画面中央優先アルゴリズム）
        currentTarget = FindcurrentTargetIn2DView();
        if (currentTarget != null) {
            // ロックオン成功時、現在のリストをベースにリアルタイム追跡リストを初期化
            trackedEnemyList.Clear();
            foreach (var col in enemyList) {
                TrackedEnemy te;
                te.collider = col;
                te.outOfRangeTimer = 0f;
                trackedEnemyList.Add(te);
            }
            ApplyTarget();
        }
    }

    public void RemoveLockOn() {

        enemyList.Clear(); // 前回のデータをクリア

        isLockedOn = false;
        currentTarget = null;
        targetHealthController = null; // キャッシュもクリア
        isBossBattle = false;
        if (lockOnMarker != null) lockOnMarker.SetActive(false); // 解除時は非表示
        if (lockOnModeMarker != null) lockOnModeMarker.SetActive(false); // 解除時は非表示
        lockedEnemyIndex = 0;

        // 全てのロックオンカメラをオフにして通常視点に戻す
        if (normalLockOnCamera != null) normalLockOnCamera.Priority = normalCamPriority;
        if (bossLockOnCamera != null) bossLockOnCamera.Priority = bossCamPriority;

        // TargetGroupを初期化
        if (targetGroup != null) {
            targetGroup.m_Targets = new CinemachineTargetGroup.Target[0];
        }

        // --- Invector連動：移動モードとターゲットを安全に完全クリア ---
        if (tpInput != null && tpInput.cc != null) {
            tpInput.cc.rotateTarget = null;
            tpInput.cc.lockInStrafe = false;
            tpInput.cc.isStrafing = false;
        }
    }

    // ロックオン中に毎フレーム走り、周囲の敵リストとタイマーを更新するメソッド
    private void UpdateTrackedEnemyList() {
        // 現在の戦闘モードに合わせたスキャン半径を設定
        float scanRadius = isBossBattle ? searchRadiusBoss : searchRadiusNormal;
        int hitCount = Physics.OverlapSphereNonAlloc(playerTransform.position, scanRadius, enemyBuffer, enemyLayer);

        // 1. 新しく範囲内に入った敵をリストに追加、または既存の敵のタイマーをリセット
        for (int i = 0; i < hitCount; i++) {
            Collider col = enemyBuffer[i];

            // 生存チェック
            var hc = col.GetComponent<vIHealthController>();
            bool isAlive = (hc != null) ? hc.currentHealth > 0 : col.gameObject.activeInHierarchy;
            if (!isAlive) continue;

            // すでに追跡リストにいるか確認
            int existingIndex = trackedEnemyList.FindIndex(e => e.collider == col);
            if (existingIndex >= 0) {
                // 範囲内にいるので、範囲外タイマーを0にリセット
                TrackedEnemy te = trackedEnemyList[existingIndex];
                te.outOfRangeTimer = 0f;
                trackedEnemyList[existingIndex] = te;
            } else {
                // 新しい敵を発見したのでリストに追加
                TrackedEnemy newEnemy;
                newEnemy.collider = col;
                newEnemy.outOfRangeTimer = 0f;
                trackedEnemyList.Add(newEnemy);
            }
        }
        // 2. 範囲外に出た敵のタイマーを進め、制限時間を超えたら削除する
        for (int i = trackedEnemyList.Count - 1; i >= 0; i--) {
            TrackedEnemy te = trackedEnemyList[i];

            // 敵が死亡、またはオブジェクト自体が消滅した場合は即座にリストから削除
            if (te.collider == null || !te.collider.gameObject.activeInHierarchy) {
                trackedEnemyList.RemoveAt(i);
                continue;
            }
            var hc = te.collider.GetComponent<vIHealthController>();
            if (hc != null && hc.currentHealth <= 0) {
                trackedEnemyList.RemoveAt(i);
                continue;
            }
            // 今回のスキャン結果（enemyBuffer）に含まれているかチェック
            bool isStillInRange = false;
            //このfor文でイメージ的にドーム型で索敵をしてプレイヤーと距離が離れ過ぎたらListから外す候補を出している
            for (int j = 0; j < hitCount; j++) {
                if (enemyBuffer[j] == te.collider) {
                    isStillInRange = true;
                    break;
                }
            }
            // 範囲外にいる場合、タイマーを進める
            if (!isStillInRange) {
                te.outOfRangeTimer += Time.deltaTime;
                if (te.outOfRangeTimer >= outOfRangeDuration) {
                    // 猶予時間を超えたので追跡リストから削除
                    trackedEnemyList.RemoveAt(i);
                    continue;
                }
                // タイマーの更新をリストに反映
                trackedEnemyList[i] = te;
            }
        }
        // 3. 最後に、インスペクター確認用＆ターゲット切り替え用の通常List（enemyList）を同期更新
        enemyList.Clear();
        for (int i = 0; i < trackedEnemyList.Count; i++) {
            enemyList.Add(trackedEnemyList[i].collider);
        }
        // 4. リストの数が変わった際、現在ロック中の敵のインデックス（lockedEnemyIndex）がズレないよう再計算
        if (currentTarget != null) {
            lockedEnemyIndex = enemyList.FindIndex(c => c.transform == currentTarget);
        }
    }
    
    /// <summary>
    /// 2.5D視点の画面内において、最も中央（プレイヤー付近）に近い最適な敵を割り出す
    /// </summary>
    private Transform FindcurrentTargetIn2DView() {
        enemyList.Clear();

        // 1. ボス探索（enemyBufferを使い回す）
        int bossCount = Physics.OverlapSphereNonAlloc(playerTransform.position, searchRadiusBoss, enemyBuffer, enemyLayer);
        for (int i = 0; i < bossCount; i++) {
            Transform t = enemyBuffer[i].transform;
            // 一時的なチェックはGetComponentを使わざるを得ないが、ロック確定時以外はキャッシュしない
            var hc = t.GetComponent<vIHealthController>();
            bool isAlive = (hc != null) ? hc.currentHealth > 0 : t.gameObject.activeInHierarchy;

            if (enemyBuffer[i].CompareTag(bossTag) && isAlive) {
                enemyList.Add(enemyBuffer[i]);
                lockedEnemyIndex = 0;
                return t;
            }
        }

        //Listバージョン
        int enemyCount = Physics.OverlapSphereNonAlloc(playerTransform.position, searchRadiusNormal, enemyBuffer, enemyLayer);
        currentTarget = null;

        // それぞれのモードで「最悪の初期値」をセットする
        float closestScreenDistance = float.MaxValue;   // 距離用（小さいほど良いので、初期値は最大値）
        float bestDot = -1f;        // 向き用（大きいほど良いので、初期値は最小値）

        // ヒットした数だけ List に追加
        for (int i = 0; i < enemyCount; i++) {
            Transform t = enemyBuffer[i].transform;
            var hc = t.GetComponent<vIHealthController>();
            bool isAlive = (hc != null) ? hc.currentHealth > 0 : t.gameObject.activeInHierarchy;

            if (!isAlive || enemyBuffer[i].CompareTag(bossTag)) continue;
            enemyList.Add(enemyBuffer[i]);
            int currentListIndex = enemyList.Count - 1;
            
            if (pForwrdLock) {
                // 【カメラを使わない判定：プレイヤーの正面度を計算】
                Vector3 directionToEnemy = (t.position - playerTransform.position).normalized;
                directionToEnemy.y = 0f; // 高低差を無視して水平面の向きだけで計算

                // プレイヤーの正面（transform.forward）と、敵への方向の「内積（Dot）」を計算
                float dot = Vector3.Dot(playerTransform.forward, directionToEnemy);

                // 向きのときは「値が大きい（正面に近い）敵」を最優先にする
                if (dot > bestDot) {
                    bestDot = dot;
                    currentTarget = t;
                    lockedEnemyIndex = currentListIndex;
                }
            } else {
                // プレイヤーと敵の直線距離を計算
                float distanceToCenter = Vector3.Distance(playerTransform.position, t.position);

                // 距離のときは「値が小さい（近い）敵」を最優先にする
                if (distanceToCenter < closestScreenDistance) {
                    closestScreenDistance = distanceToCenter;
                    currentTarget = t;
                    lockedEnemyIndex = currentListIndex;
                }
            }
        }
        return currentTarget;
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
        if (currentTarget != null) {
            bool isStillTracked = enemyList.Exists(c => c.transform == currentTarget);
            if (!isStillTracked) {
                RemoveLockOn();
            }
        }
    }

    /// <summary>
    /// 対象のキャラクターが生存しているか判定（Invectorの体力コンポーネントに対応）
    /// </summary>
    private bool IsTargetAlive(Transform targetTransform) {
        if (targetTransform == null) return false;

        // キャッシュされたコンポーネントがあればそれを使う（毎フレームのGetComponentを完全回避）
        if (targetHealthController != null) {
            return targetHealthController.currentHealth > 0;
        }
        // コンポーネントが無ければオブジェクトのアクティブ状態で判定（安全弁）
        return targetTransform.gameObject.activeInHierarchy;
    }

    // マークの位置を更新するメソッド
    private void UpdateMarkerPosition() {
        if (isLockedOn && currentTarget != null) {
            // マークをアクティブにする
            if (!lockOnMarker.activeSelf) lockOnMarker.SetActive(true);
            if (!lockOnModeMarker.activeSelf) lockOnModeMarker.SetActive(true);

            // 敵の座標 + オフセットの位置にマークを移動
            lockOnMarker.transform.position = currentTarget.position + markerOffset;
            lockOnModeMarker.transform.position = this.transform.position + markerOffset;

            // 2.5Dカメラ（横スクロール等）でマークの向きをカメラに固定したい場合
            lockOnMarker.transform.rotation = Camera.main.transform.rotation;
            lockOnModeMarker.transform.rotation = Camera.main.transform.rotation;
        } else {
            // ロックオンしていない、またはターゲットがいない時は非表示
            if (lockOnMarker != null && lockOnMarker.activeSelf && lockOnModeMarker != null && lockOnModeMarker.activeSelf) {
                lockOnMarker.SetActive(false);
                lockOnModeMarker.SetActive(false);
            }
        }
    }


    // --- 範囲を描画するための処理 ---
    private void OnDrawGizmos() {
        if (!showGizmos) return;
        // 指定された位置と半径でワイヤーフレームの球を描画
        if (playerTransform != null) {
            float scanRadius = isBossBattle ? searchRadiusBoss : searchRadiusNormal;
            // 1. 中のドーム（塗りつぶし）を描画
            Gizmos.color = domeColor;
            Gizmos.DrawSphere(playerTransform.position, scanRadius);
            // 塗りつぶしだけだと境界が見づらいため、クッキリした線も重ねます
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(playerTransform.position, scanRadius);
        }
    }
}

