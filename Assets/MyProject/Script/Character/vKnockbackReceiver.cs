using UnityEngine;
using Invector.vCharacterController;
using Invector;
using Invector.vCharacterController.AI;
using System.Collections;
using System.Collections.Generic;
using System;

//======ノックバックスクリプト=======
public class vKnockbackReceiver : MonoBehaviour {

    public enum ShakeMode {
        TransformOffset, // 1. スクリプトで座標を揺らす（要・親子関係調整）
        VertexShader     // 2. シェーダーで頂点を揺らす（構造変更なし・安全）
    }
    [Header("Knockback Settings")]
    public float knockbackForce = 15f;    // 押し出す強さ
    public float knockbackDuration = 0.2f; // ノックバックが続く時間（秒）

    // 内部管理用変数（GCは発生しません）
    private float knockbackTimer = 0f;
    private Vector3 currentPushDirection;
    private float currentKnockbackForce = 0f; // 今回のフレームで加える力を保持

    // 🚨 確実に対象を捉えるための隠し変数
    private Rigidbody cachedHipsRb;
    private Rigidbody rb;
    private Animator animator;

    [Header("オンだとノックバック数値がreaction_idよって変わる")]
    [SerializeField] private bool bbbb;

    // ラグドール化を後から発動するためのフラグ
    private float savedRagdollForceMagnitude = 0f; // ラグドール化の瞬間に加える追撃の力

    [Header("--- Shake Mode Settings ---")]
    [Tooltip("1（座標揺らし）か2（シェーダー揺らし）かを選択")]
    [SerializeField] private ShakeMode shakeMode;

    [Header("Model Shake Settings")]
    [Tooltip("インスペクターで先ほど作ったMeshContainerをここにアサインしてください")]
    [SerializeField] private Transform shakeTargetTransform;
    [Tooltip("震える時間の長さ")]
    [SerializeField] private float shakeDuration = 0.2f;
    [Tooltip("震える激しさ（振幅）")]
    [SerializeField] private float shakeMagnitude = 0.1f;
    [Tooltip("震える速さ（周期ピッチ）")]
    [SerializeField] private float shakeSpeed = 50f;    // シェーダー版は少し速め（100前後）が心地よく揺れます

    private Coroutine currentShakeCoroutine;
    private Vector3 originalLocalPosition;

    // GCを出さないためのマテリアルキャッシュ用配列
    private Material[] cachedMaterials;

    private List<Material> targetMaterials = new List<Material>();
    private static readonly int ShakeProgressProp = Shader.PropertyToID("_ShakeProgress");
    private static readonly int ShakeMagnitudeProp = Shader.PropertyToID("_ShakeMagnitude");
    private static readonly int ShakeSpeedProp = Shader.PropertyToID("_ShakeSpeed");

    void Start() {
        animator = GetComponent<Animator>();

        // シェイク対象の初期ローカル座標を記憶
        if (shakeTargetTransform != null) {
            originalLocalPosition = shakeTargetTransform.localPosition;
        } else {
            Debug.LogWarning($"[{gameObject.name}] shakeTargetMesh がアサインされていません。描画パーツをまとめた空オブジェクトをセットしてください。");
        }

        // モード2用：子階層の全メッシュからマテリアルを自動回収（マテリアルのインスタンス化を防ぐため共有マテリアルを取得）
        // 【プロの軽量化対策】
        // rend.materials ではなく rend.sharedMaterials を使うことで、マテリアルのクローン（メモリ消費・GCのゴミ）を完全に防ぎます。
        // 重複を排除して、このキャラクターが持っている「ModelShakeShader」のオリジナル実体だけをリストアップします。
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<Material> validMaterials = new List<Material>();

        foreach (var rend in renderers) {
            // sharedMaterials は元のプロジェクトのアセットを直接参照するため GC が発生しません
            Material[] sharedMats = rend.sharedMaterials;
            foreach (var mat in sharedMats) {
                if (mat != null && mat.HasProperty(ShakeProgressProp)) {
                    if (!validMaterials.Contains(mat)) {
                        validMaterials.Add(mat);
                    }
                }
            }
        }

        cachedMaterials = validMaterials.ToArray();

        // 起動時に初期化（念のため）
        ResetShakeStates();

        // 【最重要】ゲーム開始時に、子オブジェクトから「Hips」のRigidbodyをあらかじめ見つけて記憶しておく。ラグドール用の処理
        Rigidbody[] allRbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rbInChild in allRbs) {
            if (rbInChild.gameObject == this.gameObject) continue;

            if (rbInChild.gameObject.name.ToLower().Contains("hips")) {
                cachedHipsRb = rbInChild;
                Debug.Log($"[Knockback Setup] {cachedHipsRb.gameObject.name} を事前に補足しました。");
                rb = GetComponent<Rigidbody>();     //人型で腰以外にもキックバックする用に格納
                break;
            }
        }

        // もし名前にHipsが含まれていない場合の保険（自分以外の2番目のRigidbody）
        if (cachedHipsRb == null && allRbs.Length > 1) {
            foreach (var rbInChild in allRbs) {
                if (rbInChild.gameObject != this.gameObject) {
                    cachedHipsRb = rbInChild;
                    Debug.Log($"[Knockback Setup] {cachedHipsRb.gameObject.name} を補足できませんでした。");
                    break;
                }
            }
        }
        if (cachedHipsRb == null) {
            cachedHipsRb = GetComponent<Rigidbody>();
            Debug.Log($"[Knockback Setup] {cachedHipsRb.gameObject.name} のリジッドボディをGetComponentしました。");
        }
    }

    // ダメージイベント（OnReceiveDamage）から呼び出す
    public void TakeKnockback(vDamage damage) {
        if (cachedHipsRb == null || damage == null || damage.sender == null) return;

        // 1. 攻撃元からの水平方向のベクトルを計算
        currentPushDirection = transform.position - damage.sender.position;
        currentPushDirection.y = 0f;
        currentPushDirection.Normalize();

        savedRagdollForceMagnitude = 0f;
        
        // 🚨 攻撃を受けたら、Animatorの加算レイヤーに設定した「ビクつくトリガー」を即座に引く
        if (animator != null) {
            animator.SetTrigger("TriggerShake");
        }

        // --- ★モデルシェイクの実行 ---
        // ラグドール（Id >= 5）化しない時だけシェイクさせる（ラグドール化するとバラバラに吹っ飛ぶため）
        bool isRagdoll = (bbbb && damage.reaction_id == 5) || (!bbbb && damage.reaction_id >= 5);
        if (!isRagdoll && shakeTargetTransform != null) {
            if (currentShakeCoroutine != null) StopCoroutine(currentShakeCoroutine);

            if (shakeMode == ShakeMode.TransformOffset && shakeTargetTransform != null) {
                currentShakeCoroutine = StartCoroutine(DoTransformShake());
            } 
            else if (shakeMode == ShakeMode.VertexShader && targetMaterials.Count > 0) {
                currentShakeCoroutine = StartCoroutine(DoShaderShake());
            }
        }
        // ----------------------------

        if (bbbb)
            // TakeKnockback内での分岐例
            switch (damage.reaction_id) {
                case 0: // 弱攻撃
                    knockbackTimer = 0.1f;
                    currentKnockbackForce = 8f; // インベクターの制御に勝つため少し強めに設定
                    break;

                case 1: // 中攻撃
                    knockbackTimer = 0.2f;
                    currentKnockbackForce = 18f;
                    break;

                case 2: // 強攻撃
                    knockbackTimer = 0.4f;
                    currentKnockbackForce = 28f;
                    break;

                case 5: // 吹っ飛んで倒れる
                    knockbackTimer = 0.35f;       // 吹っ飛ぶ時間（少し長め）
                    currentKnockbackForce = 35f;  // 通常の体として後ろにズラす強い力
                    
                    // ふにゃふにゃになった瞬間に、ダメ押しでさらに後ろに加える力（調整用）
                    savedRagdollForceMagnitude = 20f;
                    TriggerRagdollImmediate(currentPushDirection * savedRagdollForceMagnitude);
                    break;

                default: // 想定外のID用セーフティ
                    knockbackTimer = knockbackDuration;
                    currentKnockbackForce = 15f;
                    break;
            }
        
        if (!bbbb)
            //大ダメージ（例: reaction_id が 5 以上）ならラグドールへ
            if (damage.reaction_id >= 5) {
                TriggerRagdollImmediate(currentPushDirection * 20f);
                return;
            } else {
                // 2. タイマーをセットしてノックバック状態を開始する
                knockbackTimer = knockbackDuration;
                currentKnockbackForce = knockbackForce;
            }
        //ForceMode.VelocityChangeだと質量（Mass）の影響をうけない。ForceMode.Impulseだと質量（Mass）の影響をうける。
        //cachedHipsRb.AddForce(currentPushDirection * ((currentKnockbackForce + knockbackForce) * cachedHipsRb.mass), ForceMode.Impulse);

        Debug.Log("[Knockback] 吹っ飛び！");
    }

    void FixedUpdate() {
       // タイマーが動いている間は、物理フレームごとに連続して後ろへ力を加える
       if (knockbackTimer > 0f) {
           if (cachedHipsRb != null && rb == null) {
               // Invectorの毎フレームの速度リセットに競り勝つため、
               // Velocityへの直接加算、または強い力（VelocityChange / Impulse）を加え続けます
               //cachedHipsRb.AddForce(currentPushDirection * currentKnockbackForce, ForceMode.VelocityChange);
                    cachedHipsRb.AddForce(currentPushDirection * ((currentKnockbackForce + knockbackForce) * cachedHipsRb.mass), ForceMode.Impulse);

                }else if(rb != null) {
                    rb.AddForce(currentPushDirection * ((currentKnockbackForce + knockbackForce) * cachedHipsRb.mass), ForceMode.Impulse);
                }
           // タイマーを減らす（FixedUpdateなので fixedDeltaTime を使用）
           knockbackTimer -= Time.fixedDeltaTime;

           // タイマーが終了したらロックを解除して制御をInvectorに戻す
           if (knockbackTimer <= 0f) {
               currentKnockbackForce = 0f;
           }
       }
    }

    // 即座にラグドール化させるメソッド
    private void TriggerRagdollImmediate(Vector3 additionalForce) {
        var ragdoll = GetComponent<vRagdoll>();
        if (ragdoll != null) {
            
            // ラグドール化する時はシェイクを強制停止して位置を戻す
            if (currentShakeCoroutine != null) {
                StopCoroutine(currentShakeCoroutine);
                ResetShakeStates();
            }
            
            // ラグドールをONにする
            ragdoll.ActivateRagdoll();
            cachedHipsRb.AddForce(additionalForce * knockbackForce, ForceMode.Impulse);
            Debug.Log("[Knockback] 吹っ飛び移動が完了したため、ラグドール化しました！");
        }
    }

    // すべての揺れ状態を安全に初期化する関数
    private void ResetShakeStates() {
        if (shakeTargetTransform != null) shakeTargetTransform.localPosition = originalLocalPosition;

        int count = cachedMaterials.Length;
        for (int i = 0; i < count; i++) {
            if (cachedMaterials[i] != null) cachedMaterials[i].SetFloat(ShakeProgressProp, 0f);
        }
    }

    // --- ★シェイク処理のコルーチン ---
    private IEnumerator DoTransformShake() {
        float elapsed = 0f;
        Vector3 nextPos = originalLocalPosition;

        while (elapsed < shakeDuration) {
            elapsed += Time.deltaTime;

            // 時間の経過とともに揺れを滑らかに減衰させる (1.0 -> 0.0)
            float damper = 1f - (elapsed / shakeDuration);

            // 正弦波（Sin）とノイズを使って、左右（X軸）に高速に往復させるブレを計算
            // 必要に応じて Y 軸も足すと上下にも揺れます
            nextPos.x = originalLocalPosition.x + (Mathf.Sin(elapsed * shakeSpeed) * shakeMagnitude * damper);
            nextPos.y = originalLocalPosition.y + ((Mathf.PerlinNoise(elapsed * shakeSpeed, 0f) - 0.5f) * shakeMagnitude * damper);

            // 元のローカル位置にブレを加算して適用
            shakeTargetTransform.localPosition = nextPos;

            yield return null; // 1フレーム待機
        }

        // 完全に終わったら元の位置にピタッと戻す
        shakeTargetTransform.localPosition = originalLocalPosition;
        currentShakeCoroutine = null;
    }

    // 【モード2】頂点シェーダーコルーチン
    private IEnumerator DoShaderShake() {
        float elapsed = 0f;
        int matCount = cachedMaterials.Length;

        // 最初に一発設定
        for (int i = 0; i < matCount; i++) {
            if (cachedMaterials[i] == null) continue;
            cachedMaterials[i].SetFloat(ShakeSpeedProp, shakeSpeed);
            cachedMaterials[i].SetFloat(ShakeMagnitudeProp, shakeMagnitude);
        }

        while (elapsed < shakeDuration) {
            elapsed += Time.deltaTime;
            // 進行度を 1.0 から 0.0 へ減衰させる
            float progress = 1f - (elapsed / shakeDuration);

            // ループ内でのforeachを排除し、for文で回すことでGCを完全に0に抑えます
            for (int i = 0; i < matCount; i++) {
                if (cachedMaterials[i] != null) {
                    cachedMaterials[i].SetFloat(ShakeProgressProp, progress);
                }
            }
            yield return null;
        }

        // 完全に終了したら0に戻す
        for (int i = 0; i < matCount; i++) {
            if (cachedMaterials[i] != null) cachedMaterials[i].SetFloat(ShakeProgressProp, 0f);
        }
        currentShakeCoroutine = null;
    }

    // ゲーム終了時やオブジェクト破棄時にシェーダーの値を安全に0に戻す
    private void OnDisable() {
        ResetShakeStates();
    }
}
