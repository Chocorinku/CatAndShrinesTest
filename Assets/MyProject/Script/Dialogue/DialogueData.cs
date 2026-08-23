//== DialogueData.cs ====
using UnityEngine;
using UnityEngine.Events; // ★【追加】UnityEventを使用するために必須

//構造体（struct）のスクリプト
//構造体とはガベージコレクションが発生しないでメモリ住所だけを伝える軽いデータの受け渡し方法
//ゴミを出さずGetComponentより遥かに軽いが、重いデータ量だと逆に重くなってしまうデメリット
//そんな重いデータだと参照渡しを使うと軽くてすむので、構造体より重いデータの場合は参照渡しを◎

// 選択肢1つ分のデータ
[System.Serializable]
public struct DialogueChoice {
    public string choiceText;    // 選択肢のボタンに表示する文字（例：「はい」「いいえ」）

    [Tooltip("次にジャンプするElement（ノード）の番号")]
    public int targetNodeIndex;  // この選択肢を選んだときにジャンプする、次の会話のインデックス

    // ★【追加】この選択肢を選んだときに自動でONにしたいフラグを指定（なければNoneでOK）
    [Tooltip("この選択肢を選んだ時にON(True)にするゲームフラグ")]
    public StoryFlagType flagToSet;
}

// 会話の1ページ（1発言）分のデータ
[System.Serializable]
public struct DialogueNode {
    [Header("=== ここには敢えて空欄にしてElement数字がノードの数字に[インスペクター設定] ===")]
    public string nodeDebugName;


    [Header("=== セリフの設定 ===")]
    [TextArea(2, 5)]
    public string text;          // セリフ本文
    public DialogueChoice[] choices; // このページに紐づく選択肢（配列が空なら「次へ」の通常進行）

    [Tooltip("-1で会話終了、-2で通常通り次の配列へ")]
    // このセリフが終わった後に進むべき次のノードのインデックス（-1なら会話終了、-2なら通常通り次の配列要素へ）
    public int nextNodeIndex;

    // ★【追加】このページ（ノード）を表示して会話が閉じた瞬間に実行したいゲームイベント
    [Header("--- 会話終了時の連動イベント ---")]
    [Tooltip("このセリフが終わって会話ウィンドウが閉じた瞬間に実行される処理")]
    public UnityEvent onNodeEndEvent;
}
