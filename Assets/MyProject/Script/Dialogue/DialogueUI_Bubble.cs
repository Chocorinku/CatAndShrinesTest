using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogueUI_Bubble : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI bubbleText;
    [SerializeField] private CanvasGroup canvasGroup; // フェード演出用（なければ後でアタッチ）

    private Coroutine autoCloseCoroutine;
    private Transform mainCameraTransform;

    private void Awake() {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        // 最初は完全に非表示（透明）にしておく
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // メインカメラのTransformを事前にキャッシュしてUpdateでの検索負荷をゼロにする（GCゼロ）
        if (Camera.main != null) {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void Update() {
        // ★最重要処理：吹き出しが常に2.5Dカメラの正面を真っ直ぐ向くようにする（ビルボード処理）
        if (mainCameraTransform != null && canvasGroup != null && canvasGroup.alpha > 0f) {
            // カメラの回転と同じ向きに固定することで、2.5D固定カメラに対して常に100%綺麗に正対します
            transform.rotation = mainCameraTransform.rotation;
        }
    }

    /// <summary>
    /// 頭上吹き出しを表示する（GCゼロ）
    /// </summary>
    public void ShowBubble(string text, float duration = 2.5f) {
        if (bubbleText != null) bubbleText.text = text;

        gameObject.SetActive(true); // 表示をON

        if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
        autoCloseCoroutine = StartCoroutine(BubbleLifeRoutine(duration));
    }

    private IEnumerator BubbleLifeRoutine(float duration) {
        // 1. フワッとフェードイン
        float elapsed = 0f;
        float fadeDuration = 0.15f;
        while (elapsed < fadeDuration) {
            elapsed += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = elapsed / fadeDuration;
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 2. 指定された秒数（足を止めずに表示する時間）待つ
        yield return new WaitForSeconds(duration);

        // 3. フワッとフェードアウト
        elapsed = 0f;
        while (elapsed < fadeDuration) {
            elapsed += Time.deltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // 使い終わったら自分自身をオブジェクトプールに戻すか消去する（今回はシンプルに非アクティブ）
        gameObject.SetActive(false);
    }

    public void ShowBubble(Transform npcHeadTarget, string line) {

    }
}