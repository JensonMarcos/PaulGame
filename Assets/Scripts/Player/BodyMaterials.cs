using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class BodyMaterials : MonoBehaviour
{
    [SerializeField] Renderer bodyRenderer;
    [SerializeField] Renderer screenRenderer;

    static readonly Dictionary<ulong, Texture2D> avatarCache = new Dictionary<ulong, Texture2D>();
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

    // domain reload is disabled, so statics survive between play sessions
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => avatarCache.Clear();

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

    public async void ApplyFace(ulong steamId)
    {
        if (screenRenderer == null || steamId == 0 || !SteamClient.IsValid) return;

        if (!avatarCache.TryGetValue(steamId, out Texture2D tex) || tex == null)
        {
            Steamworks.Data.Image? image = await SteamFriends.GetLargeAvatarAsync(steamId);
            if (image == null || this == null) return;

            tex = ToTexture(image.Value);
            avatarCache[steamId] = tex;
        }

        screenRenderer.material.SetTexture(BaseMap, tex);
        screenRenderer.material.SetTexture(EmissionMap, tex);
    }

    //some vibe coded shi idk
    static Texture2D ToTexture(Steamworks.Data.Image image)
    {
        int w = (int)image.Width, h = (int)image.Height;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        // Steam images are top-down, Unity textures are bottom-up
        byte[] flipped = new byte[image.Data.Length];
        int row = w * 4;
        for (int y = 0; y < h; y++)
            System.Buffer.BlockCopy(image.Data, y * row, flipped, (h - 1 - y) * row, row);

        tex.LoadRawTextureData(flipped);
        tex.Apply();
        return tex;
    }
}
