using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ３Dオブジェクトを（段階的に）（半）透明にする機能を提供する。
/// マテリアルのシェーダーには「Rendering Mode = Transparent の Standard Shader」など、color で alpha を指定できるものをアサインすること。
/// ChangeAlpha() を呼ぶことで alpha を指定することができる。
/// フレームレートによって変化具合が変わる実装になっていることに注意。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class OccludeeController : MonoBehaviour
{
    /// <summary>1フレームごとにどれくらいずつ alpha を変化させるかを指定する</summary>
    [SerializeField] float m_step = 0.01f;
    /// <summary>この alpha にするというターゲットの値</summary>
    float m_targetAlpha = 1.0f;
    /// <summary>alpha の初期値</summary>
    float m_originalAlpha = 1.0f;
    Material m_material;

    void Start()
    {
        // このオブジェクトのマテリアルを取得しておく
        Renderer r = GetComponent<Renderer>();
        if (r) {
            m_material = r.material;
        }
        if (m_material) {
            m_originalAlpha = m_material.color.a;
        } else {
            Debug.LogError(name + " needs Renderer and Material for occulusion.");
        }
        
    }
    /// <summary>
    /// alpha を初期値に戻す
    /// </summary>
    public void ChangeAlpha2Original() {
        ChangeAlpha(m_originalAlpha);
    }

    /// <summary>
    /// alpha を変更する
    /// </summary>
    /// <param name="targetAlpha">ターゲットとなる alpha の値</param>
    public void ChangeAlpha(float targetAlpha) {
        m_targetAlpha = targetAlpha;
        if (m_material) {
            StartCoroutine(ChangeAlpha());
        }
    }

    /// <summary>
    /// alpha を（徐々に）変更する
    /// 『 WaitForEndOfFrame() 』はフレームレートが高いほど早く透明になるが
    /// どの環境でも透明する速度を変わらないようにするのなら『 WaitForSeconds() 』を使う。（引数に指定した秒数後に処理を行います）
    /// </summary>
    /// <returns></returns>
    IEnumerator ChangeAlpha() {
        if(m_material.color.a > m_targetAlpha) {
            while(m_material.color.a > m_targetAlpha) {
                Color c = m_material.color;
                c.a -= m_step;
                m_material.color = c;
                yield return new WaitForEndOfFrame();
            }
        } else {
            while(m_material.color.a < m_targetAlpha) {
                Color c = m_material.color;
                c.a += m_step;
                m_material.color = c;
                yield return new WaitForEndOfFrame();
            }
        }
    }
}
