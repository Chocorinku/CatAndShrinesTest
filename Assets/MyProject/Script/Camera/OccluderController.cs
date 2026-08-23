using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// トリガーに接触したオブジェクトが OccludeeController を持っていたら、その機能を呼んで（半）透明にする。
/// </summary>
[RequireComponent(typeof(Collider))]
public class OccluderController : MonoBehaviour
{
    /// <summary>（半）透明状態にする時にどれくらいの alpha にするか指定する</summary>
    [Header("どれくらいの透明度合いにするか")]
    [SerializeField, Range(0f, 1f)] float m_transparency = 0.2f;

    private void OnTriggerEnter(Collider other) {
        OccludeeController dee = other.gameObject.GetComponent<OccludeeController>();
        if (dee) {
            dee.ChangeAlpha(m_transparency);
        }
    }
    private void OnTriggerExit(Collider other) {
        OccludeeController dee = other.gameObject.GetComponent<OccludeeController>();
        if (dee) {
            dee.ChangeAlpha2Original();
        }
    }
}
