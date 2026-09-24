using TMPro;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public const string SensitivityKey = "Sensitivity";

    public static PauseMenu instance;
    public static bool IsOpen { get; private set; }

    [SerializeField] GameObject canvas;
    [SerializeField] TMP_InputField sensitivityField;

    void Awake()
    {
        instance = this;
        canvas.SetActive(false);
        IsOpen = false;
        sensitivityField.onEndEdit.AddListener(OnSensitivityChanged);
    }

    void OnDestroy()
    {
        sensitivityField.onEndEdit.RemoveListener(OnSensitivityChanged);
        if(instance == this) instance = null;
        IsOpen = false;
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        canvas.SetActive(open);
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if(open) ShowSensitivity();
    }

    void ShowSensitivity()
    {
        if(PlayerCamera.local != null)
            sensitivityField.text = PlayerCamera.local.sensitivity.ToString();
    }

    void OnSensitivityChanged(string value)
    {
        if(!float.TryParse(value, out float sensitivity)) {
            ShowSensitivity();
            return;
        }

        if(PlayerCamera.local != null) PlayerCamera.local.sensitivity = sensitivity;
        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        PlayerPrefs.Save();
    }

    public void Leave()
    {
        if(SteamManager.Instance == null) {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
            return;
        }

        SteamManager.Instance.ReturnToMenu();
    }
}
