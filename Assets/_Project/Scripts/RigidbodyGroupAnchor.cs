using System.Collections.Generic;
using UnityEngine;

public class RigidbodyGroupAnchor : MonoBehaviour
{
    [Header("固定したいRigidbodyのリスト")]
    [SerializeField] private List<Rigidbody> childRigidbodies = new List<Rigidbody>();

    private struct RigidbodyOffset
    {
        public Rigidbody Rigidbody;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    private readonly List<RigidbodyOffset> _offsets = new List<RigidbodyOffset>();

    private void Awake()
    {
        SaveInitialOffsets();
    }

    /// <summary>
    /// 親に対する各Rigidbodyの初期相対位置・回転を記録
    /// </summary>
    public void SaveInitialOffsets()
    {
        _offsets.Clear();

        foreach (var rb in childRigidbodies)
        {
            if (rb == null) continue;

            // このオブジェクト（親）基準の相対位置・回転を算出
            Vector3 localPos = transform.InverseTransformPoint(rb.position);
            Quaternion localRot = Quaternion.Inverse(transform.rotation) * rb.rotation;

            _offsets.Add(new RigidbodyOffset
            {
                Rigidbody = rb,
                LocalPosition = localPos,
                LocalRotation = localRot
            });
        }
    }

    private void FixedUpdate()
    {
        foreach (var offset in _offsets)
        {
            if (offset.Rigidbody == null) continue;

            // 親の現在の位置・回転から目標のワールド座標を計算
            Vector3 targetPos = transform.TransformPoint(offset.LocalPosition);
            Quaternion targetRot = transform.rotation * offset.LocalRotation;

            // 物理エンジン経由で位置と回転を強制設定
            offset.Rigidbody.MovePosition(targetPos);
            offset.Rigidbody.MoveRotation(targetRot);

            // 衝突反動による速度の蓄積・ブレを防ぎ、固定位置を維持する
            #if UNITY_2023_1_OR_NEWER
            offset.Rigidbody.linearVelocity = Vector3.zero;
            #else
            offset.Rigidbody.velocity = Vector3.zero;
            #endif
            offset.Rigidbody.angularVelocity = Vector3.zero;
        }
    }
}