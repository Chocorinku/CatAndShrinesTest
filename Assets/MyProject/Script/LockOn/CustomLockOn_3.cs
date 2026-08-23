using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Invector.vCharacterController;
using Invector;
using System;

public class CustomLockOn_3 : MonoBehaviour {
    // 敵ごとの離脱タイマーを管理するための構造体（クラスではないのでGCが発生しない）
    private struct TrackedEnemy {
        public Collider collider;
        public float outOfRangeTimer; // 範囲外に出てからの経過時間
    }

    #region Variables
    [Header("Cinemachine References")]
    public CinemachineVirtualCamera normalLockOnCamera;
    public CinemachineVirtualCamera bossLockOnCamera;
    public CinemachineTargetGroup targetGroup;

    [Header("Settings")]
    public string enemyTag = "Enemy";
    public string bossTag = "Boss";
    public float searchRadiusNormal = 15f;
    public float searchRadiusBoss = 30f;
    public LayerMask enemyLayer;

    [Header("New Settings (リアルタイムリスト用)")]
    [Tooltip("範囲外に出てからリストから完全に除外されるまでの猶予秒数")]
    public float outOfRangeDuration = 2.0f;

    [Header("LockOn Inputs")]
    public GenericInput lockOnInput = new GenericInput("Tab", "RightStickClick", "RightStickClick");

    // 内部管理用変数
    [Header("ロック中の敵")]
    [SerializeField] private Transform currentTarget; // bestTargetと統合し、これ一本に絞る
    private vIHealthController targetHealthController; // 負荷軽減のためにキャッシュ用変数を追加

    private bool isLockedOn = false;
    private bool isBossBattle = false;
    private Transform playerTransform;
    private vThirdPersonInput tpInput;
    private Rigidbody rb;
    private int normalCamPriority;
    private int bossCamPriority;

    [SerializeField] private Collider[] enemyBuffer = new Collider[100];
    // 【変更】単純なColliderのリストから、タイマー付きの構造体リストに変更
    private List<TrackedEnemy> trackedEnemyList = new List<TrackedEnemy>();
    // インスペクター確認用、あるいは他スクリプト連携用にColliderだけのリストも一応残す（中身は自動更新）
    [SerializeField] private List<Collider> enemyList = new List<Collider>();

    private bool isStickHeld;

    [Header("現在ロックオンしている敵のインデックス")]
    [SerializeField] private int lockedEnemyIndex;

    public RaycastHit groundHit;
    [Range(0, 30)] public float slideDownVelocity = 7f;
    public float slideDownSmooth = 2f;
    public float rotateDownSlopeSmooth = 8f;

    [Header("LockOn UI Settings")]
    [SerializeField] private GameObject lockOnMarker;
    [SerializeField] private GameObject lockOnModeMarker;
    [SerializeField] private Vector3 markerOffset = new Vector3(0, 2f, 0);

    [Header("ギズモの表示設定")]
    [SerializeField] bool showGizmos;
    [SerializeField] Color domeColor = new Color(1f, 0.92f, 0.016f, 0.2f);
    [SerializeField] Color wireColor = Color.cyan;
    #endregion

    void Start() {
        playerTransform = this.transform;
        rb = GetComponent<Rigidbody>();
        if (normalLockOnCamera != null) {
            normalCamPriority = normalLockOnCamera.Priority;
            if (isBossBattle) normalLockOnCamera.Priority = 0;
        }
        if (bossLockOnCamera != null) {
            bossCamPriority = bossLockOnCamera.Priority;
            bossLockOnCamera.Priority = 0;
        }
        tpInput = GetComponent<vThirdPersonInput>();
        if (tpInput == null) {
            Debug.LogError("プレイヤーに vThirdPersonInput が見つかりません！");
        }
    }

    void Update() {
        HandleInput();

        // ロックオン状態の時、周囲の敵リストをリアルタイムに更新・維持する
        if (isLockedOn) {
            UpdateTrackedEnemyList();
            CheckTargetStatus(); // 距離チェックの仕様をタイマー連動に変更
        }

        UpdateMarkerPosition();
    }

    private void FixedUpdate() {
        if (isLockedOn && currentTarget != null && tpInput != null && tpInput.cc != null) {
            tpInput.cc.rotateTarget = currentTarget;
            tpInput.cc.lockInStrafe = true;
            tpInput.cc.isStrafing = true;
            tpInput.cc.lockRotation = true;

            Vector3 directionToEnemy = currentTarget.position - playerTransform.position;
            directionToEnemy.y = 0f;

            var normal = currentTarget.position - playerTransform.position;
            normal.y = 0f;
            var dir = Vector3.ProjectOnPlane(normal.normalized, groundHit.normal).normalized;
            rb.velocity = Vector3.Lerp(rb.velocity, dir * slideDownVelocity, slideDownSmooth * Time.fixedDeltaTime);

            dir.y = 0f;
            if (directionToEnemy != Vector3.zero) {
                Vector3 desiredForward = Vector3.RotateTowards(transform.forward, dir, rotateDownSlopeSmooth * Time.fixedDeltaTime, 0f);
                Quaternion _newRotation = Quaternion.LookRotation(desiredForward);
                rb.MoveRotation(_newRotation);
            }
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
    private void SwitchTarget() {
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


            //if (horizontalInput > 0.1f) {
            //    lockedEnemyIndex = (lockedEnemyIndex + 1) % enemyList.Count;
            //} else if (horizontalInput < -0.1f) {
            //    lockedEnemyIndex = (lockedEnemyIndex - 1 + enemyList.Count) % enemyList.Count;
            //}

            // ターゲットを切り替えて適用
            //currentTarget = enemyList[lockedEnemyIndex].transform;
            //ApplyTarget();


        } else if (Mathf.Abs(horizontalInput) <= 0.1f) {
            isStickHeld = false;
        }
    }

    private void ApplyTarget() {
        if (currentTarget != null) {
            isLockedOn = true;
            isBossBattle = currentTarget.CompareTag(bossTag);

            // ロックした瞬間にコンポーネントを一度だけキャッシュ（毎フレームGetComponentを防ぐ）
            targetHealthController = currentTarget.GetComponent<vIHealthController>();

            if (isBossBattle) {
                if (targetGroup != null) {
                    targetGroup.m_Targets = new CinemachineTargetGroup.Target[0];
                    targetGroup.AddMember(playerTransform, 1f, 1f);
                    targetGroup.AddMember(currentTarget, 1f, 1.5f);
                }
                if (bossLockOnCamera != null) bossLockOnCamera.Priority = bossCamPriority;
                if (normalLockOnCamera != null) normalLockOnCamera.Priority = 0;
            } else {
                if (normalLockOnCamera != null) normalLockOnCamera.Priority = normalCamPriority;
                if (bossLockOnCamera != null) bossLockOnCamera.Priority = 0;
            }

            UpdateMarkerPosition();
        }
    }

    private void TryLockOn() {
        // 初回ロックオン時は、周囲を一度スキャンしてターゲットを決める
        currentTarget = FindBestTargetIn2DView();
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
        enemyList.Clear();
        isLockedOn = false;
        currentTarget = null;
        targetHealthController = null; // キャッシュもクリア
        isBossBattle = false;

        if (lockOnMarker != null) lockOnMarker.SetActive(false);
        if (lockOnModeMarker != null) lockOnModeMarker.SetActive(false);
        lockedEnemyIndex = 0;

        if (normalLockOnCamera != null) normalLockOnCamera.Priority = normalCamPriority;
        if (bossLockOnCamera != null) bossLockOnCamera.Priority = bossCamPriority;
        if (targetGroup != null) {
            targetGroup.m_Targets = new CinemachineTargetGroup.Target[0];
        }
        if (tpInput != null && tpInput.cc != null) {
            tpInput.cc.rotateTarget = null;
            tpInput.cc.lockInStrafe = false;
            tpInput.cc.isStrafing = false;
        }
    }

    // ★新規：ロックオン中に毎フレーム走り、周囲の敵リストとタイマーを更新するメソッド
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

    private Transform FindBestTargetIn2DView() {
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
        // 2. ザコ敵探索
        int enemyCount = Physics.OverlapSphereNonAlloc(playerTransform.position, searchRadiusNormal, enemyBuffer, enemyLayer);

        Transform bestFound = null; // メソッド内の一時変数に変更
        float closestScreenDistance = float.MaxValue;
        Camera mainCam = Camera.main;

        for (int i = 0; i < enemyCount; i++) {
            Transform t = enemyBuffer[i].transform;
            var hc = t.GetComponent<vIHealthController>();
            bool isAlive = (hc != null) ? hc.currentHealth > 0 : t.gameObject.activeInHierarchy;

            if (!isAlive || enemyBuffer[i].CompareTag(bossTag)) continue;

            enemyList.Add(enemyBuffer[i]);
            int currentListIndex = enemyList.Count - 1;
            if (mainCam != null) {
                Vector3 screenPos = mainCam.WorldToScreenPoint(t.position);
                if (screenPos.z < 0) continue;

                Vector2 enemyScreenPos2D = new Vector2(screenPos.x, screenPos.y);
                Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                float distanceToCenter = Vector2.Distance(screenCenter, enemyScreenPos2D);

                if (distanceToCenter < closestScreenDistance) {
                    closestScreenDistance = distanceToCenter;
                    bestFound = t;
                    lockedEnemyIndex = currentListIndex;
                }
            }
        }
        return bestFound;
    }

    private void CheckTargetStatus() {
        // 死亡チェック
        if (!IsTargetAlive()) {
            RemoveLockOn();
            return;
        }

        // 【変更】現在ロック中の敵が、追跡リスト（猶予タイマー内）から完全に消え去ったか確認
        // これにより、単純な即時距離判定ではなく、設定した「秒数（outOfRangeDuration）」が経ったら外れるようになります
        if (currentTarget != null) {
            bool isStillTracked = enemyList.Exists(c => c.transform == currentTarget);
            if (!isStillTracked) {
                RemoveLockOn();
            }
        }
    }
    
    // 引数を無くし、すでにロックしている敵（currentTarget）の生存を「キャッシュ」を使って超高速に判定
    private bool IsTargetAlive() {
        if (currentTarget == null) return false;
        // キャッシュされたコンポーネントがあればそれを使う（毎フレームのGetComponentを完全回避）
        if (targetHealthController != null) {
            return targetHealthController.currentHealth > 0;
        }
        return currentTarget.gameObject.activeInHierarchy;
    }

    // ※提示されていなかったマーカー位置更新メソッドのガワだけエラー防止で配置
    private void UpdateMarkerPosition() {
        if (isLockedOn && currentTarget != null) { 
            if (!isLockedOn || currentTarget == null || lockOnMarker == null) return;

            if (!lockOnMarker.activeSelf) lockOnMarker.SetActive(true);
            if (!lockOnModeMarker.activeSelf) lockOnModeMarker.SetActive(true);
            lockOnMarker.transform.position = currentTarget.position + markerOffset;
            lockOnModeMarker.transform.position = this.transform.position + markerOffset;
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
