using UnityEngine;

public sealed class EraserTool : MonoBehaviour
{
    [SerializeField] private Whiteboard whiteboard;
    [SerializeField] private Collider bottomCollider;
    [SerializeField, Min(0.0001f)] private float minimumEraseSize = 0.01f;
    [SerializeField] private bool autoFindWhiteboard = true;

    private void Awake()
    {
        if (bottomCollider == null)
        {
            bottomCollider = GetComponent<Collider>();
        }

        if (autoFindWhiteboard && whiteboard == null)
        {
            whiteboard = FindFirstObjectByType<Whiteboard>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessCollision(collision);
    }
private void ProcessCollision(Collision collision)
    {
        if (whiteboard == null || !whiteboard.IsInitialized || whiteboard.BoardCollider == null)
        {
            return;
        }

        if (collision.collider != whiteboard.BoardCollider)
        {
            return;
        }
        
        Collider sourceCollider = bottomCollider != null ? bottomCollider : GetComponent<Collider>();
        Vector3 centerPoint = sourceCollider != null ? sourceCollider.bounds.center : transform.position;

        if (!whiteboard.TryGetUv(centerPoint, out Vector2 uv))
        {
            return;
        }

        Vector2 eraseSizeUv = ResolveEraseSizeUv();
        
        // --- ここから変更 ---
        // 消しゴムの長辺方向（ローカルのX軸 = right）を、ホワイトボードのローカル座標系に変換
        Vector3 localDir = whiteboard.transform.InverseTransformDirection(transform.right);
        
        // ホワイトボードのXY平面上での角度を計算（ラジアンから度数法へ変換）
        float angleDeg = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
        // --- ここまで変更 ---

        whiteboard.DrawRectangle(uv, eraseSizeUv, angleDeg);
    }
    private Vector2 ResolveEraseSizeUv()
    {
        Collider sourceCollider = bottomCollider != null ? bottomCollider : GetComponent<Collider>();
        if (sourceCollider == null || whiteboard == null)
        {
            return Vector2.one * minimumEraseSize;
        }

        Vector2 uvSize;
        if (sourceCollider is BoxCollider boxCollider)
        {
            // 1. 消しゴムの接触面（X軸とZ軸）の実際のワールドサイズ（長さ）を計算
            float realWidth = boxCollider.size.x * sourceCollider.transform.lossyScale.x;
            float realHeight = boxCollider.size.z * sourceCollider.transform.lossyScale.z;

            // 2. その長さを「ホワイトボードのX軸・Y軸に沿ったベクトル」として組み立てる
            Vector3 alignedWorldSize = whiteboard.transform.right * realWidth 
                                     + whiteboard.transform.up * realHeight;
            
            // 3. 既存の変換メソッドを通すことで、スケールや回転の影響を受けない正確なUVサイズを取得
            uvSize = whiteboard.WorldSizeToUvSize(alignedWorldSize);
        }
        else
        {
            uvSize = whiteboard.WorldSizeToUvSize(sourceCollider.bounds.size);
        }

        uvSize.x = Mathf.Max(minimumEraseSize, uvSize.x);
        uvSize.y = Mathf.Max(minimumEraseSize, uvSize.y);
        return uvSize;
    }
}