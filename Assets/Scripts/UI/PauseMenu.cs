using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public const string SensitivityKey = "Sensitivity";

    public static bool IsOpen { get; private set; }

    static readonly (string label, FullScreenMode mode)[] WindowModes = {
        ("Fullscreen", FullScreenMode.ExclusiveFullScreen),
        ("Borderless", FullScreenMode.FullScreenWindow),
        ("Windowed", FullScreenMode.Windowed),
    };

    [Serializable]
    struct VolumeSlider
    {
        public string mixerParameter;
        public Slider slider;
    }

    [SerializeField] GameObject canvas;
    [SerializeField] TMP_InputField sensitivityField;
    [SerializeField] TMP_Dropdown resolutionDropdown;
    [SerializeField] TMP_Dropdown windowModeDropdown;
    [SerializeField] TMP_Dropdown graphicsDropdown;
    [SerializeField] AudioMixer mixer;
    [SerializeField] VolumeSlider[] volumeSliders;
    [SerializeField] GameObject leaveRoomButton;

    PlayerInputs inputs;
    Vector2Int[] resolutions;

    static bool InRoom => SteamManager.Instance != null && SteamManager.Instance.CurrentLobby != null;

    void Awake()
    {
        canvas.SetActive(false);
        IsOpen = false;
        sensitivityField.onEndEdit.AddListener(OnSensitivityChanged);

        inputs = new PlayerInputs();
        inputs.Gameplay.Escape.Enable();
    }

    // AudioMixer.SetFloat is ignored during Awake
    void Start()
    {
        SetupResolutionDropdown();
        SetupWindowModeDropdown();
        SetupGraphicsDropdown();
        foreach(VolumeSlider volume in volumeSliders) SetupVolumeSlider(volume);
    }

    void Update()
    {
        if(inputs.Gameplay.Escape.WasPressedThisFrame()) SetOpen(!IsOpen);
    }

    void OnDestroy()
    {
        sensitivityField.onEndEdit.RemoveListener(OnSensitivityChanged);
        inputs.Dispose();
        IsOpen = false;
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        canvas.SetActive(open);

        bool lockCursor = !open && PlayerCamera.local != null;
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;

        if(open) {
            ShowSensitivity();
            leaveRoomButton.SetActive(InRoom);
        }
        else PlayerPrefs.Save();
    }

    void ShowSensitivity()
    {
        if(PlayerCamera.local != null)
            sensitivityField.text = PlayerCamera.local.sensitivity.ToString();
        else if(PlayerPrefs.HasKey(SensitivityKey))
            sensitivityField.text = PlayerPrefs.GetFloat(SensitivityKey).ToString();
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

    void SetupResolutionDropdown()
    {
        resolutions = Screen.resolutions
            .Select(r => new Vector2Int(r.width, r.height))
            .Distinct()
            .ToArray();

        var current = new Vector2Int(Screen.width, Screen.height);
        SetupDropdown(
            resolutionDropdown,
            resolutions.Select(r => $"{r.x} x {r.y}").ToArray(),
            Array.IndexOf(resolutions, current),
            i => Screen.SetResolution(resolutions[i].x, resolutions[i].y, Screen.fullScreenMode));
    }

    void SetupWindowModeDropdown()
    {
        SetupDropdown(
            windowModeDropdown,
            WindowModes.Select(w => w.label).ToArray(),
            Array.FindIndex(WindowModes, w => w.mode == Screen.fullScreenMode),
            i => Screen.fullScreenMode = WindowModes[i].mode);
    }

    // Each quality level points at its own URP asset in Project Settings > Quality
    void SetupGraphicsDropdown()
    {
        SetupDropdown(
            graphicsDropdown,
            QualitySettings.names,
            QualitySettings.GetQualityLevel(),
            i => QualitySettings.SetQualityLevel(i));
    }

    static void SetupDropdown(TMP_Dropdown dropdown, string[] options, int selected, UnityAction<int> onSelect)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(options.ToList());
        dropdown.SetValueWithoutNotify(selected);
        dropdown.onValueChanged.AddListener(onSelect);
    }

    void SetupVolumeSlider(VolumeSlider volume)
    {
        float saved = PlayerPrefs.GetFloat(volume.mixerParameter, 1);
        volume.slider.SetValueWithoutNotify(saved);
        SetVolume(volume.mixerParameter, saved);

        volume.slider.onValueChanged.AddListener(value => {
            SetVolume(volume.mixerParameter, value);
            PlayerPrefs.SetFloat(volume.mixerParameter, value);
        });
    }

    void SetVolume(string mixerParameter, float linear)
    {
        float decibels = Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20;
        mixer.SetFloat(mixerParameter, decibels);
    }

    public void LeaveRoom()
    {
        if(InRoom) SteamManager.Instance.ReturnToMenu();
    }

    public void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
