using UnityEngine;

public class BodyMaterials : MonoBehaviour
{
    [SerializeField] Renderer bodyRenderer;

    void Awake()
    {
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
    }

    public void ApplyTeamColor(int team)
    {
        Color color = (GameManager.instance != null) ? GameManager.instance.GetTeamColor(team) : new Color(1f, 0.5f, 0f);

        if (bodyRenderer == null) return;

        bodyRenderer.material.color = color;
    }
}
