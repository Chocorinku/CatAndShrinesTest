using UnityEngine;
using UnityEngine.UI; // 通常のTextを使う場合
using TMPro;         // TextMeshProを使う場合

public class GoldUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text uguiText;         // 通常のTextを使う場合はここにドラッグ
    [SerializeField] private TextMeshProUGUI tmproText; // TextMeshProを使う場合はここにドラッグ

    private bool isSubscribed = false;

    private void Start() {
        // オブジェクト起動時に確実に登録する
        SubscribeToManager();
    }

    private void OnEnable() {
            // 念のため、非アクティブから復帰した時も再登録できるようにする
            SubscribeToManager();
    }

    private void OnDisable() {
        // オブジェクトが非アクティブになったら、メモリ漏れを防ぐために受信を解除する（GC・バグ対策）
        if (isSubscribed && CurrencyManager.Instance != null) {
            CurrencyManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
            isSubscribed = false;
        }
    }

    private void SubscribeToManager() {
        if (isSubscribed) return;

        if (CurrencyManager.Instance != null) {
            CurrencyManager.Instance.OnGoldChanged += UpdateGoldDisplay;
            isSubscribed = true;

            // 現在の所持金で表示を初期化
            UpdateGoldDisplay(CurrencyManager.Instance.CurrentGold);
        }
    }

    /// <summary>
    /// お金が変動した電波を受け取って、画面の文字を書き換える関数（GCアロケーション最小化）
    /// </summary>
    private void UpdateGoldDisplay(int currentGold) {
        // 文字列の結合をシンプルに行い、表示を更新
        string goldString = $"{currentGold} GOLD";

        if (tmproText != null) {
            tmproText.text = goldString;
        } else if (uguiText != null) {
            uguiText.text = goldString;
        }
    }
}
