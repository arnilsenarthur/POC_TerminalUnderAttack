using TUA.I18n;
using TUA.Temp;
using TUA.Windowing;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace TUA.UI
{
    [RequireComponent(typeof(UIDocument))]
    [MovedFrom("TUA.Interface")]
    public class PauseMenuController : Window
    {
        #region Serialized Fields
        [FormerlySerializedAs("_settingsMenuController")]
        [Header("References")]
        [SerializeField] private SettingsMenuController settingsMenuController;
        #endregion

        #region Private Fields
        private Button _closeButton;
        private Button _settingsButton;
        private Button _leaveMatchButton;
        private Button _resumeButton;
        private Label _titleLabel;
        private Label _roomCodeLabel;
        private Label _roomCode;
        private VisualElement _roomCodeContainer;
        private IVisualElementScheduledItem _scheduledOpenAnim;
        private UnityEngine.UIElements.Experimental.ValueAnimation<float> _panelAnim;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            LocalizationManager.OnLanguageChangeEvent += _UpdateLocalizedText;
            _UpdateLocalizedText();
            _UpdateRoomCode();
        }

        protected virtual void OnDisable()
        {
            LocalizationManager.OnLanguageChangeEvent -= _UpdateLocalizedText;
        }
        #endregion

        #region Public Methods
        public override void SetVisible(bool visible)
        {
            if (Overlay == null)
                return;

            if (visible)
            {
                _CancelPanelAnimations();

                Overlay.style.display = DisplayStyle.Flex;
                Overlay.pickingMode = PickingMode.Position;

                _UpdateRoomCode();

                if (Backdrop != null)
                    Backdrop.style.opacity = 1f;

                if (Panel != null)
                {
                    Panel.style.opacity = 0f;
                    Panel.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));
                    _scheduledOpenAnim = Panel.schedule.Execute(() =>
                    {
                        _panelAnim = Panel.experimental.animation
                            .Start(0f, 1f, 300, (element, value) =>
                        {
                            element.style.opacity = value;
                            float scale = 0.9f + (value * 0.1f);
                            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                        })
                            .KeepAlive()
                            .OnCompleted(() => _panelAnim = null);
                    });
                    _scheduledOpenAnim.ExecuteLater(1);
                }

                base.SetVisible(true);
            }
            else
            {
                _CancelPanelAnimations();

                if (Backdrop != null)
                    Backdrop.style.opacity = 0f;

                if (Panel != null)
                {
                    _panelAnim = Panel.experimental.animation
                        .Start(1f, 0f, 300, (element, value) =>
                    {
                        element.style.opacity = value;
                        float scale = 0.9f + (value * 0.1f);
                        element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                    })
                        .KeepAlive()
                        .OnCompleted(() =>
                        {
                            _panelAnim = null;
                            base.SetVisible(false);
                        });
                }
                else
                {
                    base.SetVisible(false);
                }
            }
        }
        #endregion

        #region Protected Methods
        protected override VisualElement _FindOverlay()
        {
            return Root?.Q<VisualElement>("PauseOverlay");
        }

        protected override void _InitializeElements()
        {
            Backdrop = Overlay?.Q<VisualElement>("Backdrop");
            Panel = Overlay?.Q<VisualElement>("PausePanel");
            _closeButton = Overlay?.Q<Button>("CloseButton");
            _settingsButton = Overlay?.Q<Button>("SettingsButton");
            _leaveMatchButton = Overlay?.Q<Button>("LeaveMatchButton");
            _resumeButton = Overlay?.Q<Button>("ResumeButton");
            _titleLabel = Overlay?.Q<Label>("Title");
            _roomCodeContainer = Overlay?.Q<VisualElement>("RoomCodeContainer");
            _roomCodeLabel = Overlay?.Q<Label>("RoomCodeLabel");
            _roomCode = Overlay?.Q<Label>("RoomCode");
        }

        protected override void _SetupCallbacks()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked += () => Close();
            }

            if (_resumeButton != null)
            {
                _resumeButton.clicked += () => Close();
            }

            if (_settingsButton != null)
            {
                _settingsButton.clicked += () =>
                {
                    if (!settingsMenuController)
                        return;

                    var manager = WindowManager.FindInScene();
                    if (manager != null)
                        manager.OpenWindow(settingsMenuController);
                };
            }

            if (_leaveMatchButton != null)
            {
                _leaveMatchButton.clicked += () =>
                {
                };
            }
        }
        #endregion

        #region Private Methods
        private void _CancelPanelAnimations()
        {
            _scheduledOpenAnim?.Pause();
            _scheduledOpenAnim = null;

            if (_panelAnim != null)
            {
                try { _panelAnim.Stop(); }
                catch
                {
                }

                _panelAnim = null;
            }
        }

        private void _UpdateLocalizedText()
        {
            if (_titleLabel != null)
                _titleLabel.text = LocalizationManager.Get("pause.title");

            if (_settingsButton != null)
                _settingsButton.text = LocalizationManager.Get("pause.settings");

            if (_leaveMatchButton != null)
                _leaveMatchButton.text = LocalizationManager.Get("pause.leave_match");

            if (_resumeButton != null)
                _resumeButton.text = LocalizationManager.Get("pause.close");

            if (_roomCodeLabel != null)
                _roomCodeLabel.text = LocalizationManager.Get("pause.room_code");
        }

        private void _UpdateRoomCode()
        {
            var relayLobby = FindFirstObjectByType<RelayLobbyGUI>();
            string code = relayLobby ? relayLobby.GetRoomCode() : "";

            if (_roomCodeContainer != null)
                _roomCodeContainer.style.display = string.IsNullOrEmpty(code) ? DisplayStyle.None : DisplayStyle.Flex;

            if (_roomCode != null)
                _roomCode.text = code;
        }
        #endregion
    }
}
