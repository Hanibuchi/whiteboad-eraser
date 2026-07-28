using UnityEngine;

public class ToolCollisionRelay : MonoBehaviour
{
    private PenTool[] penTools;
    private EraserTool[] eraserTools;

    private void Awake()
    {
        // 子孫オブジェクトにアタッチされているすべてのツールを取得しておく
        penTools = GetComponentsInChildren<PenTool>(true);
        eraserTools = GetComponentsInChildren<EraserTool>(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        foreach (var pen in penTools) pen.ProcessCollision(collision);
        foreach (var eraser in eraserTools) eraser.ProcessCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (var pen in penTools) pen.ProcessCollision(collision);
        foreach (var eraser in eraserTools) eraser.ProcessCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        foreach (var pen in penTools) pen.HandleCollisionExit(collision);
    }
}