// == InteractableNPC.cs ==
using UnityEngine;

[System.Serializable]
public struct DialogueBranchCondition {
    [Tooltip("このフラグがON（True）の時に…")]
    public StoryFlagType requiredFlag;
    [Tooltip("このElement(ノード)番号から会話を開始する")]
    public int startNodeIndex;
}

//NPC用：インスペクターで1か2かを選択可能
public class InteractableNPC : MonoBehaviour
{
    public enum DialogueType {
        ImportantWindow, // 1 画面下ウィンドウ（足を止める）
        CasualBubble     // 2 頭上吹き出し（足を止めない）
    }

    [SerializeField] private string[] dialogueLines; // 会話内容（GC対策として起動後は固定配列を参照）

    [Header("NPC Settings")]
    [SerializeField] private string npcName = "村人";
    [SerializeField] private DialogueType dialogueType = DialogueType.ImportantWindow;

    [Header("Texts (For Window)")]
    [SerializeField] private DialogueNode[] windowNodes;

    // ★【追加】インスペクターで「このフラグの時はこのノードから開始」を複数設定できるリスト
    [Header("--- セリフの分岐条件設定 ---")]
    [SerializeField] private DialogueBranchCondition[] branchConditions;

    [Header("Text (For Bubble)")]
    [SerializeField] private string bubbleLine = "うーん、困ったなぁ…";
    [SerializeField] private Transform headTarget; // 2 の吹き出しを表示させる頭上の位置オブジェクト

    private bool playerInZone = false;
    private bool bubbleTriggered = false;

    [Header("Look At Settings")]
    [Tooltip("会話始めに相手に向くかどうか")]
    [SerializeField] private bool isLookAtEnabled = true; // ★追加：インスペクターで向くかどうかを選べるスイッチ

    private void Update() {
        if (!playerInZone) return;

        // ★最重要ガード：マネージャーがすでに会話中なら、NPC側の話しかけ判定を物理的に100%完全遮断
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue) return;

        // エリア内にプレイヤーがいて、かつ特定のボタン（例：Eキー、コントローラーの◯ボタン等）が押されたら
        // ※InvectorのInputではなく、独立したInputシステム（または標準のInput）で会話開始ボタンを検知
        // １ 画面下ウィンドウ形式の起動（ボタンを押して開始）
        //if (dialogueType == DialogueType.ImportantWindow && !DialogueManager.Instance.IsInDialogue) {
        //    if (dialogueType == DialogueType.ImportantWindow) {
        //        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0)) {
        //            DialogueManager.Instance.StartWindowDialogue(transform, npcName, windowNodes, true);
        //        }
        //    }

        // プレイヤーの特殊体勢ガード（前回実装した鉄壁ハック）
        if (DialogueManager.Instance != null) {
            var pInput = DialogueManager.Instance.PlayerInput;
            if (pInput != null && pInput.cc != null) {
                // 1. 地面に足がついていない（ジャンプ中・落下中）・ローリング中・念のため、カスタムアクション中（ハシゴ登りや特定の演出中など）なら会話不可
                if (!pInput.cc.isGrounded || pInput.cc.isRolling || pInput.cc.customAction) return;
            }
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0))
            { 
                if (dialogueType == DialogueType.ImportantWindow) {
                // ★会話開始時の初期ノード番号を、フラグの状態から自動計算する
                int finalStartNodeIndex = GetCurrentStartNodeIndex();

                // DialogueManager側のStartWindowDialogueを、開始位置を指定できるように拡張して呼び出す
                DialogueManager.Instance.StartWindowDialogue(transform, npcName, windowNodes, finalStartNodeIndex);


                // ウィンドウ会話：自動で向く仕様
                DialogueManager.Instance.StartWindowDialogue(transform, npcName, windowNodes);
                }else if(dialogueType == DialogueType.CasualBubble)
                // ★頭上吹き出し会話：インスペクターの設定（isLookAtEnabled）を渡す！
                DialogueManager.Instance.StartBubbleDialogue(transform, bubbleLine, isLookAtEnabled);
        }
    }

    /// <summary>
    /// 現在のゲームフラグ状況を見て、どのセリフから始めるべきかを最速・GCゼロで判定する
    /// </summary>
    private int GetCurrentStartNodeIndex() {
        if (branchConditions == null || branchConditions.Length == 0 || StoryFlagManager.Instance == null) return 0;

        // インスペクターの下に登録されている条件（後から追加された進行度の高いフラグ）を優先してチェック
        for (int i = branchConditions.Length - 1; i >= 0; i--) {
            StoryFlagType flag = branchConditions[i].requiredFlag;

            // None以外で、かつそのフラグが実際にON（True）になっていれば、その開始インデックスを採用
            if (flag != StoryFlagType.None && StoryFlagManager.Instance.GetFlag(flag)) {
                return branchConditions[i].startNodeIndex;
            }
        }
        return 0; // どのフラグも立っていなければ、通常通り最初の0番から開始
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) return;
        playerInZone = true;

        // ２ 頭上吹き出し形式の起動（近づいた瞬間、自動で1回だけフワッと喋る）
        //　これをコメント化するとNPC前でボタン押した時にしか吹き出し出ないようにできる
        if (dialogueType == DialogueType.CasualBubble && !bubbleTriggered) {
            Transform target = headTarget != null ? headTarget : transform;
            DialogueManager.Instance.StartBubbleDialogue(target, bubbleLine);
            bubbleTriggered = true;         // 何度も連続で出ないようにロック
        }

    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player")) return;
        playerInZone = false;

        // エリアから離れたら、②の吹き出しトリガーをリセットして再侵入時に喋れるようにする
        if (dialogueType == DialogueType.CasualBubble) {
            bubbleTriggered = false;
        }
    }
}
