using UnityEngine;
using UnityEngine.UI;
using TMPro;

//選択肢ボタンが出たときに、プレイヤーがどっちを選んだかを正確に判断するために必要なスクリプト。
//DialogueManagerにあるボタン入力はあくまでセリフを回す（めくる）ためのボタン入力
public class DialogueChoiceButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI choiceText;

    private int targetNodeIndex = 0;

    private void Awake() {
        if (button == null) button = GetComponent<Button>();
    }

    public void Setup(string text, int targetIndex) {
        if (choiceText != null) choiceText.text = text;
        targetNodeIndex = targetIndex;

        // ボタンがプールから使い回される際、古いジャンプ番号が残るのを防ぎます。
        // 設定するたびに、一度クリックイベントを綺麗にリセットしてから、最新の数字を登録し直します。
        if (button != null) {
            button.onClick.RemoveAllListeners();
            // ボタンクリック時にマネージャーへジャンプ先インデックスを通知する
            button.onClick.AddListener(OnClickButton);
        }
    }

    private void OnClickButton() {

        if (DialogueManager.Instance != null) {
            DialogueManager.Instance.OnSelectChoice(targetNodeIndex);
        }
    }
}
