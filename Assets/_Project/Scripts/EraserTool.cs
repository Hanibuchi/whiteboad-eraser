using UnityEngine;

public sealed class EraserTool : MonoBehaviour
{
    [SerializeField] private Whiteboard whiteboard;
    [SerializeField] private Collider bottomCollider;
    [SerializeField, Min(0.0001f)] private float minimumEraseSize = 0.01f;
    [SerializeField, Min(0.0001f)] private float eraseSpacingFactor = 0.5f; // 追加: 消す間隔の調整用
    [SerializeField] private bool autoFindWhiteboard = true;

    // 追加: 前回の位置を記憶する変数
    private bool hasLastUv;
    private Vector2 lastUv;

    public void SetWhiteboard(Whiteboard targetWhiteboard)
    {
        whiteboard = targetWhiteboard;
    }

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

    // 追加: ペンと同様に、ホワイトボードから離れたら連続状態をリセットする
    public void HandleCollisionExit(Collision collision)
    {
        if (whiteboard != null && collision.collider == whiteboard.BoardCollider)
        {
            hasLastUv = false;
        }
    }
    
    public void ProcessCollision(Collision collision)
    {
        if (whiteboard == null || !whiteboard.IsInitialized || whiteboard.BoardCollider == null)
            return;

        if (collision.collider != whiteboard.BoardCollider)
            return;

        if (collision.contactCount == 0)
            return;

        Collider sourceCollider = bottomCollider != null ? bottomCollider : GetComponent<Collider>();

        // 消しゴム自身のコライダーが実際に触れているか確認
        bool isTouching = false;
        ContactPoint validContact = default; // 接触点を取得するために追加
        
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.thisCollider == sourceCollider)
            {
                validContact = contact;
                isTouching = true;
                break;
            }
        }

        if (!isTouching) return;

        Vector3 centerPoint = sourceCollider != null ? sourceCollider.bounds.center : transform.position;
        if (!whiteboard.TryGetUv(centerPoint, out Vector2 uv))
            return;

        Vector2 eraseSizeUv = ResolveEraseSizeUv();
        Vector3 localDir = whiteboard.transform.InverseTransformDirection(transform.right);
        float angleDeg = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;

        // --- ここから追加・修正: 線を繋げるための補間処理 ---
        float movementDistance = hasLastUv ? Vector2.Distance(lastUv, uv) : 0f;
        int stampCount = 1;

        if (hasLastUv)
        {
            // 消しゴムのサイズ（XとYの小さい方）を基準にスタンプする間隔を決定
            float minEraseSize = Mathf.Min(eraseSizeUv.x, eraseSizeUv.y);
            float spacing = Mathf.Max(0.0001f, minEraseSize * eraseSpacingFactor);
            stampCount = Mathf.Max(1, Mathf.CeilToInt(movementDistance / spacing));
        }

        // 計算した回数分、前回の位置から今回の位置まで間を埋めるように描画する
        for (int stampIndex = 0; stampIndex < stampCount; stampIndex++)
        {
            float t = stampCount == 1 ? 1f : (float)stampIndex / (stampCount - 1);
            Vector2 stampUv = hasLastUv ? Vector2.Lerp(lastUv, uv, t) : uv;
            whiteboard.DrawRectangle(stampUv, eraseSizeUv, angleDeg);
        }

        lastUv = uv;
        hasLastUv = true;
        // --- ここまで ---
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
            float realWidth = boxCollider.size.x * sourceCollider.transform.lossyScale.x;
            float realHeight = boxCollider.size.z * sourceCollider.transform.lossyScale.z;

            Vector3 alignedWorldSize = whiteboard.transform.right * realWidth
                                     + whiteboard.transform.up * realHeight;

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