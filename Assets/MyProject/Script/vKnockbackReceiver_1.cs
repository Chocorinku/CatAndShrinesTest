using UnityEngine;
using Invector.vCharacterController;
using Invector;
using Invector.vCharacterController.AI;
using System.Collections;
using System.Collections.Generic;

public class vKnockbackReceiver_1 : MonoBehaviour
{
    public enum ShakeMode {
        TransformOffset, // 1. スクリプトで座標を揺らす（要・親子関係調整）
        VertexShader     // 2. シェーダーで頂点を揺らす（構造変更なし・完全ゼロGC・安全）
    }

    [Header("Knockback Settings")]
    public float knockbackForce = 15f;
    public float knockbackDuration = 0.2f;
    private float knockbackTimer = 0f;
    private Vector3 currentPushDirection;
    private float currentKnockbackForce = 0f;

    private Rigidbody cachedHipsRb;
    private Rigidbody rb;
    private Animator animator;

    [Header("オンだとノックバック数値がreaction_idよって変わる")]
    [SerializeField] private bool bbbb;
    private float savedRagdollForceMagnitude = 0f;

    [Header("--- Shake Mode Settings ---")]
    [Tooltip("1（座標揺らし）か2（シェーダー揺らし）かを選択")]
    [SerializeField] private ShakeMode shakeMode = ShakeMode.VertexShader;

    [Header("Common Settings (共通設定)")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeSpeed = 120f; // シェーダー版は少し速めがキレが良くなります
    [SerializeField] private float maxShakeAmount = 0.08f; // 震えの最大幅
    [SerializeField] private float decreaseSpeed = 5f;    // シェーダーが元に戻る速さ

    [Header("Mode 1: Transform Settings (座標用)")]
    [Tooltip("モード1の時のみ使用。以前作成した空の親オブジェクトを指定")]
    [SerializeField] private Transform shakeTargetTransform;

    private Coroutine currentTransformShakeCoroutine;
    private Vector3 originalLocalPosition;

    // 【プロのGC対策】マテリアルのコピーを作らずに値を書き換える魔法の箱
    private MaterialPropertyBlock mpb;
    private Renderer[] cachedRenderers;
    private float currentShaderShakeAmount = 0f;

    // 高効率化（文字列検索を避ける）ためのIDキャッシュ
    private static readonly int ShakeAmountId = Shader.PropertyToID("_ShakeAmount");
    private static readonly int ShakeSpeedId = Shader.PropertyToID("_ShakeSpeed");

    void Start() {
        animator = GetComponent<Animator>();
        mpb = new MaterialPropertyBlock();

        // モード1用の初期位置記憶
        if (shakeTargetTransform != null) {
            originalLocalPosition = shakeTargetTransform.localPosition;
        }

        // 子階層にあるLODを含むすべてのRendererを起動時に1回だけ回収（Updateやヒット時のGetComponentを排除）
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        // --- 以下インベクター既存のStart処理 ---
        Rigidbody[] allRbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rbInChild in allRbs) {
            if (rbInChild.gameObject == this.gameObject) continue;
            if (rbInChild.gameObject.name.ToLower().Contains("hips")) {
                cachedHipsRb = rbInChild;
                rb = GetComponent<Rigidbody>();
                break;
            }
        }
        if (cachedHipsRb == null && allRbs.Length > 1) {
            foreach (var rbInChild in allRbs) {
                if (rbInChild.gameObject != this.gameObject) {
                    cachedHipsRb = rbInChild;
                    break;
                }
            }
        }
        if (cachedHipsRb == null) {
            cachedHipsRb = GetComponent<Rigidbody>();
        }
    }

    public void TakeKnockback(vDamage damage) {
        if (cachedHipsRb == null || damage == null || damage.sender == null) return;
        currentPushDirection = transform.position - damage.sender.position;
        currentPushDirection.y = 0f;
        currentPushDirection.Normalize();
        savedRagdollForceMagnitude = 0f;

        if (animator != null) {
            animator.SetTrigger("TriggerShake");
        }

        // --- ★シェイクのトリガー ---
        bool isRagdoll = (bbbb && damage.reaction_id == 5) || (!bbbb && damage.reaction_id >= 5);
        if (!isRagdoll) {
            if (shakeMode == ShakeMode.TransformOffset && shakeTargetTransform != null) {
                if (currentTransformShakeCoroutine != null) StopCoroutine(currentTransformShakeCoroutine);
                currentTransformShakeCoroutine = StartCoroutine(DoTransformShake());
            } else if (shakeMode == ShakeMode.VertexShader) {
                // シェーダーモード：数値を最大値にするだけでUpdateが自動減衰させる（コルーチンすら使わない超軽量処理）
                currentShaderShakeAmount = maxShakeAmount;
            }
        }
        // ------------------------------

        if (bbbb) {
            switch (damage.reaction_id) {
                case 0: knockbackTimer = 0.1f; currentKnockbackForce = 8f; break;
                case 1: knockbackTimer = 0.2f; currentKnockbackForce = 18f; break;
                case 2: knockbackTimer = 0.4f; currentKnockbackForce = 28f; break;
                case 5:
                    knockbackTimer = 0.35f; currentKnockbackForce = 35f;
                    savedRagdollForceMagnitude = 20f;
                    TriggerRagdollImmediate(currentPushDirection * savedRagdollForceMagnitude);
                    break;
                default: knockbackTimer = knockbackDuration; currentKnockbackForce = 15f; break;
            }
        }

        if (!bbbb) {
            if (damage.reaction_id >= 5) {
                TriggerRagdollImmediate(currentPushDirection * 20f);
                return;
            } else {
                knockbackTimer = knockbackDuration;
                currentKnockbackForce = knockbackForce;
            }
        }
    }

    void Update() {
        // ★シェーダーモードの減衰ロジック（毎フレームのGC発生を完全0に抑えてレンダラーへ通知）
        if (shakeMode == ShakeMode.VertexShader && currentShaderShakeAmount > 0f) {
            currentShaderShakeAmount = Mathf.MoveTowards(currentShaderShakeAmount, 0f, Time.deltaTime * decreaseSpeed);

            // MaterialPropertyBlockに値を詰め込む（マテリアルのインスタンス化が起きないため超軽量）
            mpb.SetFloat(ShakeAmountId, currentShaderShakeAmount);
            mpb.SetFloat(ShakeSpeedId, shakeSpeed);

            // すべてのLODメッシュへ一括適応
            int count = cachedRenderers.Length;
            for (int i = 0; i < count; i++) {
                if (cachedRenderers[i] != null) {
                    cachedRenderers[i].SetPropertyBlock(mpb);
                }
            }
        }
    }

    void FixedUpdate() {
        if (knockbackTimer > 0f) {
            if (cachedHipsRb != null && rb == null) {
                cachedHipsRb.AddForce(currentPushDirection * ((currentKnockbackForce + knockbackForce) * cachedHipsRb.mass), ForceMode.Impulse);
            } else if (rb != null) {
                rb.AddForce(currentPushDirection * ((currentKnockbackForce + knockbackForce) * cachedHipsRb.mass), ForceMode.Impulse);
            }
            knockbackTimer -= Time.fixedDeltaTime;
            if (knockbackTimer <= 0f) {
                currentKnockbackForce = 0f;
            }
        }
    }

    private void TriggerRagdollImmediate(Vector3 additionalForce) {
        var ragdoll = GetComponent<vRagdoll>();
        if (ragdoll != null) {
            if (currentTransformShakeCoroutine != null) StopCoroutine(currentTransformShakeCoroutine);
            ResetShakeStates();

            ragdoll.ActivateRagdoll();
            cachedHipsRb.AddForce(additionalForce * knockbackForce, ForceMode.Impulse);
        }
    }

    private void ResetShakeStates() {
        if (shakeTargetTransform != null) shakeTargetTransform.localPosition = originalLocalPosition;

        currentShaderShakeAmount = 0f;
        mpb.SetFloat(ShakeAmountId, 0f);
        int count = cachedRenderers.Length;
        for (int i = 0; i < count; i++) {
            if (cachedRenderers[i] != null) cachedRenderers[i].SetPropertyBlock(mpb);
        }
    }

    // 【モード1】座標オフセット
    private IEnumerator DoTransformShake() {
        float elapsed = 0f;
        Vector3 nextPos = originalLocalPosition;

        while (elapsed < shakeDuration) {
            elapsed += Time.deltaTime;
            float damper = 1f - (elapsed / shakeDuration);

            nextPos.x = originalLocalPosition.x + (Mathf.Sin(elapsed * shakeSpeed) * maxShakeAmount * damper);
            nextPos.y = originalLocalPosition.y + ((Mathf.PerlinNoise(elapsed * shakeSpeed, 0f) - 0.5f) * maxShakeAmount * damper);

            shakeTargetTransform.localPosition = nextPos;
            yield return null;
        }
        shakeTargetTransform.localPosition = originalLocalPosition;
        currentTransformShakeCoroutine = null;
    }

    private void OnDisable() {
        ResetShakeStates();
    }
}
