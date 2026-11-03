using UnityEngine;
using TMPro;

public class FloatingUI : MonoBehaviour
{
    public TMP_Text textComponent;

    [Header("Cài đặt Lượn sóng (Wobble)")]
    public float wobbleSpeed = 3f;    // Tốc độ nhấp nhô của chữ
    public float wobbleAmount = 1.5f; // Biên độ dao động theo chiều dọc.
    public float waveOffset = 0.5f;   // Độ lệch pha giữa các chữ (Tạo hiệu ứng gợn sóng)

    void Start()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TMP_Text>();
        }
    }

    void Update()
    {
        if (textComponent == null) return;

        // Cập nhật mesh gốc trước mỗi lần biến dạng để độ lệch không cộng dồn.
        textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = textComponent.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            float offset = Mathf.Sin(Time.time * wobbleSpeed + (i * waveOffset)) * wobbleAmount;

            vertices[vertexIndex + 0].y += offset;
            vertices[vertexIndex + 1].y += offset;
            vertices[vertexIndex + 2].y += offset;
            vertices[vertexIndex + 3].y += offset;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}