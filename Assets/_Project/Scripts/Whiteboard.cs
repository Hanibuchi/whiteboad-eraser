using UnityEngine;
using UnityEngine.Rendering;

public sealed class Whiteboard : MonoBehaviour
{
    private const string DrawShaderName = "Custom/WhiteboardDrawShader";
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int CircleCenterId = Shader.PropertyToID("_CircleCenter");
    private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");
    private static readonly int CircleOpacityId = Shader.PropertyToID("_CircleOpacity");
    private static readonly int RectangleCenterId = Shader.PropertyToID("_RectangleCenter");
    private static readonly int RectangleSizeId = Shader.PropertyToID("_RectangleSize");
    private static readonly int RectangleAngleId = Shader.PropertyToID("_RectangleAngle");

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private MeshCollider boardCollider;
    [SerializeField] private Texture2D initialTexture;
    [SerializeField] private Shader drawShader;
    [SerializeField, Min(32)] private int referenceTextureWidth = 1024;
    [SerializeField, Min(32)] private int referenceTextureHeight = 1024;
    [SerializeField] private bool matchQuadAspect = true;
    [SerializeField] private bool autoInitialize = true;

    private MaterialPropertyBlock propertyBlock;
    private Material drawMaterial;
    private RenderTexture frontTexture;
    private RenderTexture backTexture;
    private Texture2D whitePercentageReadbackTexture;
    private bool isInitialized;
    private bool whitePercentageReadbackPending;
    private bool whitePercentageDirty = true;
    private float cachedWhiteRatio = 1f;

    public RenderTexture CurrentTexture => frontTexture;
    public MeshCollider BoardCollider => boardCollider;
    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        EnsureReferences();

        if (autoInitialize)
        {
            Initialize();
        }
    }

    private void OnEnable()
    {
        if (autoInitialize)
        {
            Initialize();
        }
    }

    private void OnDisable()
    {
        ClearTextureFromRenderer();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        referenceTextureWidth = Mathf.Max(32, referenceTextureWidth);
        referenceTextureHeight = Mathf.Max(32, referenceTextureHeight);
    }

    public void Initialize()
    {
        EnsureReferences();

        if (isInitialized)
        {
            if (frontTexture != null && backTexture != null)
            {
                return;
            }

            ReleaseResources();
        }

        if (targetRenderer == null)
        {
            Debug.LogWarning("Whiteboard に Renderer が見つかりません。", this);
            return;
        }

        if (boardCollider == null)
        {
            Debug.LogWarning("Whiteboard に MeshCollider が見つかりません。", this);
            return;
        }

        if (drawMaterial == null)
        {
            if (drawShader == null)
            {
                Debug.LogError("DrawShaderがInspectorに設定されていません。", this);
                return;
            }

            drawMaterial = new Material(drawShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        Vector2Int textureSize = ResolveTextureSize();
        frontTexture = CreateRenderTexture(textureSize.x, textureSize.y, "Whiteboard Front Buffer");
        backTexture = CreateRenderTexture(textureSize.x, textureSize.y, "Whiteboard Back Buffer");

        Texture sourceTexture = initialTexture != null ? initialTexture : Texture2D.whiteTexture;
        Graphics.Blit(sourceTexture, frontTexture, drawMaterial, 2);

        ApplyTextureToRenderer(frontTexture);
        cachedWhiteRatio = 1f;
        whitePercentageDirty = true;
        whitePercentageReadbackPending = false;
        isInitialized = true;

        QueueWhitePercentageRefresh();
    }
    public void ClearBoard()
    {
        if (!CanDraw())
        {
            return;
        }

        Texture sourceTexture = initialTexture != null ? initialTexture : Texture2D.whiteTexture;

        Graphics.Blit(sourceTexture, frontTexture, drawMaterial, 2);
        Graphics.Blit(sourceTexture, backTexture, drawMaterial, 2);

        ApplyTextureToRenderer(frontTexture);

        // 追加: 非同期読み込みのフラグをリセット
        whitePercentageDirty = true;
        whitePercentageReadbackPending = false;

        // 変更: 非同期処理を待つと「前回のクリア時の白さ」が数フレーム残ってしまうため、
        // リセット時のみ強制的に同期処理でピクセルを読み込み、即座に正しい割合をキャッシュさせる
        RefreshWhitePercentageImmediately();
    }

    public void DrawCircle(Vector2 uvPosition, float radius, float opacity)
    {
        if (!CanDraw() || radius <= 0f || opacity <= 0f)
        {
            return;
        }

        uvPosition = ClampUv(uvPosition);
        radius = Mathf.Max(0.0001f, radius);
        opacity = Mathf.Clamp01(opacity);

        drawMaterial.SetVector(CircleCenterId, new Vector4(uvPosition.x, uvPosition.y, 0f, 0f));
        drawMaterial.SetFloat(CircleRadiusId, radius);
        drawMaterial.SetFloat(CircleOpacityId, opacity);

        BlitToBackBuffer(0);
        whitePercentageDirty = true;
        QueueWhitePercentageRefresh();
    }

    public void DrawRectangle(Vector2 uvPosition, Vector2 uvSize, float angleDeg)
    {
        if (!CanDraw() || uvSize.x <= 0f || uvSize.y <= 0f)
        {
            return;
        }

        uvPosition = ClampUv(uvPosition);
        uvSize = new Vector2(Mathf.Abs(uvSize.x), Mathf.Abs(uvSize.y));

        drawMaterial.SetVector(RectangleCenterId, new Vector4(uvPosition.x, uvPosition.y, 0f, 0f));
        drawMaterial.SetVector(RectangleSizeId, new Vector4(uvSize.x, uvSize.y, 0f, 0f));
        drawMaterial.SetFloat(RectangleAngleId, angleDeg);

        BlitToBackBuffer(1);
        whitePercentageDirty = true;
        QueueWhitePercentageRefresh();
    }

    public bool TryGetUv(Vector3 worldPosition, out Vector2 uv)
    {
        uv = default;

        if (boardCollider == null)
        {
            return false;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        uv = new Vector2(localPosition.x + 0.5f, localPosition.y + 0.5f);

        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
        {
            return false;
        }

        return true;
    }

    public Vector2 WorldSizeToUvSize(Vector3 worldSize)
    {
        Vector3 localSize = transform.InverseTransformVector(worldSize);
        return new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
    }

    public float GetWhiteRatio()
    {
        if (whitePercentageDirty)
        {
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                QueueWhitePercentageRefresh();
            }
            else
            {
                RefreshWhitePercentageImmediately();
            }
        }

        return cachedWhiteRatio;
    }

    public float GetWhitePercentage()
    {
        return GetWhiteRatio() * 100f;
    }

    public void ForceWhitePercentageRefresh()
    {
        whitePercentageDirty = true;
        QueueWhitePercentageRefresh();
    }

    private void EnsureReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (boardCollider == null)
        {
            boardCollider = GetComponent<MeshCollider>();
        }
    }

    private bool CanDraw()
    {
        return isInitialized && frontTexture != null && backTexture != null && drawMaterial != null;
    }

    private Vector2Int ResolveTextureSize()
    {
        int width = referenceTextureWidth;
        int height = referenceTextureHeight;

        if (!matchQuadAspect)
        {
            return new Vector2Int(width, height);
        }

        float aspect = GetQuadAspect();
        if (aspect <= 0f)
        {
            return new Vector2Int(width, height);
        }

        if (aspect >= 1f)
        {
            width = Mathf.Max(1, Mathf.RoundToInt(height * aspect));
        }
        else
        {
            height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
        }

        return new Vector2Int(width, height);
    }

    private float GetQuadAspect()
    {
        Vector3 scale = transform.lossyScale;
        float width = Mathf.Abs(scale.x);
        float height = Mathf.Abs(scale.y);

        if (width <= 0.0001f || height <= 0.0001f)
        {
            return 1f;
        }

        return width / height;
    }

    private RenderTexture CreateRenderTexture(int width, int height, string textureName)
    {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0)
        {
            msaaSamples = 1,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear ? false : true,
            useMipMap = false,
            autoGenerateMips = false,
            depthBufferBits = 0
        };

        RenderTexture texture = new RenderTexture(descriptor)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.Create();
        return texture;
    }

    private void BlitToBackBuffer(int passIndex)
    {
        Graphics.Blit(frontTexture, backTexture, drawMaterial, passIndex);
        SwapBuffers();
    }

    private void SwapBuffers()
    {
        RenderTexture temporary = frontTexture;
        frontTexture = backTexture;
        backTexture = temporary;

        ApplyTextureToRenderer(frontTexture);
    }

    private void ApplyTextureToRenderer(Texture texture)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        propertyBlock.Clear();
        if (texture != null)
        {
            propertyBlock.SetTexture(MainTexId, texture);
            propertyBlock.SetTexture(BaseMapId, texture);
        }

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ClearTextureFromRenderer()
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        propertyBlock.Clear();
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ReleaseResources()
    {
        isInitialized = false;

        if (frontTexture != null)
        {
            frontTexture.Release();
            DestroyTexture(frontTexture);
            frontTexture = null;
        }

        if (backTexture != null)
        {
            backTexture.Release();
            DestroyTexture(backTexture);
            backTexture = null;
        }

        if (drawMaterial != null)
        {
            DestroyMaterial(drawMaterial);
            drawMaterial = null;
        }

        if (whitePercentageReadbackTexture != null)
        {
            DestroyTexture(whitePercentageReadbackTexture);
            whitePercentageReadbackTexture = null;
        }
    }

    private static void DestroyTexture(Object textureObject)
    {
        if (Application.isPlaying)
        {
            Destroy(textureObject);
            return;
        }

        DestroyImmediate(textureObject);
    }

    private static void DestroyMaterial(Object materialObject)
    {
        if (Application.isPlaying)
        {
            Destroy(materialObject);
            return;
        }

        DestroyImmediate(materialObject);
    }

    private Vector2 ClampUv(Vector2 uv)
    {
        return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
    }

    private void QueueWhitePercentageRefresh()
    {
        if (!isInitialized || frontTexture == null || whitePercentageReadbackPending || !whitePercentageDirty)
        {
            return;
        }

        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            return;
        }

        whitePercentageReadbackPending = true;
        AsyncGPUReadback.Request(frontTexture, 0, OnWhitePercentageReadbackCompleted);
    }

    private void RefreshWhitePercentageImmediately()
    {
        if (!isInitialized || frontTexture == null)
        {
            return;
        }

        EnsureWhitePercentageReadbackTexture(frontTexture.width, frontTexture.height);

        RenderTexture previousActiveTexture = RenderTexture.active;
        RenderTexture.active = frontTexture;

        Rect readbackRect = new Rect(0f, 0f, frontTexture.width, frontTexture.height);
        whitePercentageReadbackTexture.ReadPixels(readbackRect, 0, 0, false);
        whitePercentageReadbackTexture.Apply(false, false);

        var pixelData = whitePercentageReadbackTexture.GetRawTextureData<Color32>();
        long total = 0;
        for (int index = 0; index < pixelData.Length; index++)
        {
            total += pixelData[index].r;
        }

        cachedWhiteRatio = pixelData.Length == 0 ? 1f : total / (255f * pixelData.Length);
        whitePercentageDirty = false;

        RenderTexture.active = previousActiveTexture;
    }

    private void EnsureWhitePercentageReadbackTexture(int width, int height)
    {
        if (whitePercentageReadbackTexture != null
            && whitePercentageReadbackTexture.width == width
            && whitePercentageReadbackTexture.height == height)
        {
            return;
        }

        if (whitePercentageReadbackTexture != null)
        {
            DestroyTexture(whitePercentageReadbackTexture);
            whitePercentageReadbackTexture = null;
        }

        whitePercentageReadbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "Whiteboard White Percentage Readback",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void OnWhitePercentageReadbackCompleted(AsyncGPUReadbackRequest request)
    {
        whitePercentageReadbackPending = false;

        if (request.hasError)
        {
            return;
        }

        var pixelData = request.GetData<Color32>();
        if (!pixelData.IsCreated || pixelData.Length == 0)
        {
            return;
        }

        long total = 0;
        for (int index = 0; index < pixelData.Length; index++)
        {
            total += pixelData[index].r;
        }

        cachedWhiteRatio = total / (255f * pixelData.Length);
        whitePercentageDirty = false;
    }
}