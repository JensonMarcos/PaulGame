using TMPro;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public const string SensitivityKey = "Sensitivity";

    public static bool IsOpen { get; private set; }

    [SerializeField] GameObject canvas;
    [SerializeField] TMP_InputField sensitivityField;

    PlayerInputs inputs;

    void Awake()
    {
        inputs = new PlayerInputs();
        inputs.Enable();
        canvas.SetActive(false);
        IsOpen = false;
        sensitivityField.onEndEdit.AddListener(OnSensitivityChanged);

        //delete later?
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDestroy()
    {
        sensitivityField.onEndEdit.RemoveListener(OnSensitivityChanged);
        inputs.Dispose();
    }

    void Update()
    {
        if (inputs.Gameplay.Escape.WasPressedThisFrame())
            SetOpen(!IsOpen);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        canvas.SetActive(open);

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (open)
            sensitivityField.text = PlayerPrefs.GetFloat(SensitivityKey, 0.15f).ToString();
    }

    void OnSensitivityChanged(string value)
    {
        if (!float.TryParse(value, out float sensitivity))
        {
            sensitivityField.text = PlayerPrefs.GetFloat(SensitivityKey, 0.15f).ToString();
            return;
        }

        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        PlayerPrefs.Save();
    }
}
