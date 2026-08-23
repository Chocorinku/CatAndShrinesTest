using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    // どこからでも CurrencyManager.Instance でアクセスできるようにする（シングルトン）
    public static CurrencyManager Instance { get; private set; }

    [Header("Player Wallet")]
    [SerializeField] private int currentGold = 0;

    // 現在の所持金を取得するプロパティ（外部からは読み取り専用）
    //public int CurrentGold { get; private set; } と同じ意味。ただ見た目がスマート。
    // => これをアロー演算子という
    public int CurrentGold => currentGold;

    // UIが更新されたときに通知するためのイベント（UI側でこれを監視する）
    public System.Action<int> OnGoldChanged;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ステージを切り替えてもお金を保持
        } else {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// お金を獲得する処理
    /// </summary>
    public void AddGold(int amount) {
        if (amount <= 0) return;

        currentGold += amount;

        // UIなどに「お金が変わったよ！」と通知する
        //?.Invoke の意味「もし誰もこの電波を受信していなくても、エラー（ぬるぽ）を出さずに無視してね」という安全機能です。
        OnGoldChanged?.Invoke(currentGold);
    }

    // <summary>
    /// お金を消費する処理（将来のショップ機能で使用）
    /// </summary>
    /// <returns>購入に成功したかどうか</returns>
    public bool TrySpendGold(int amount) {
        if (amount <= 0) return false;

        if (currentGold >= amount) {
            currentGold -= amount;
            OnGoldChanged?.Invoke(currentGold);
            return true; // お金が足りたので購入成功
        }

        return false; // お金が足りないので購入失敗
    }
}
