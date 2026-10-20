using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float fadeSpeed = 1.5f;

    private TextMeshPro textMesh;
    private Color textColor;
    private Camera mainCam;

    void Start()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh != null)
        {
            textColor = textMesh.color;
        }

        mainCam = Camera.main; // Автоматически находим главную камеру игрока

        // Гарантированно удаляем объект через 2 секунды
        Destroy(gameObject, 2f);
    }

    void Update()
    {
        if (mainCam != null)
        {
            // Принудительно поворачиваем текст лицом к камере
            transform.rotation = mainCam.transform.rotation;
        }

        // Летим вверх
        transform.position += new Vector3(0, moveSpeed * Time.deltaTime, 0);

        // Плавно становимся прозрачными
        if (textMesh != null)
        {
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;
        }
    }
}