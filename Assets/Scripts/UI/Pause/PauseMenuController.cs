using TUA.I18n;
using TUA.Temp;
using TUA.Windowing;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TUA.UI
{
    [RequireComponent(typeof(UIDocument))]
    [MovedFrom("TUA.Interface")]
    public class PauseMenuController : Window
    {
        #region Serialized Fields
        [Header("References")]
        [SerializeField] private SettingsMenuController _settingsMenuController;
        #endregion

        #region Fields
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

        #region Methods
        private void _CancelPanelAnimations()
        {
            _scheduledOpenAnim?.Pause();
            _scheduledOpenAnim = null;

            if (_panelAnim != null)
            {
                try { _panelAnim.Stop(); } catch {  }
                _panelAnim = null;
            }
        }

        protected override VisualElement _FindOverlay()
        {
            return _root?.Q<VisualElement>("PauseOverlay");
        }
        protected override void _InitializeElements()
        {
            _backdrop = _overlay?.Q<VisualElement>("Backdrop");
            _panel = _overlay?.Q<VisualElement>("PausePanel");
            _closeButton = _overlay?.Q<Button>("CloseButton");
            _settingsButton = _overlay?.Q<Button>("SettingsButton");
            _leaveMatchButton = _overlay?.Q<Button>("LeaveMatchButton");
            _resumeButton = _overlay?.Q<Button>("ResumeButton");
            _titleLabel = _overlay?.Q<Label>("Title");
            _roomCodeContainer = _overlay?.Q<VisualElement>("RoomCodeContainer");
            _roomCodeLabel = _overlay?.Q<Label>("RoomCodeLabel");
            _roomCode = _overlay?.Q<Label>("RoomCode");
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
                    if (_settingsMenuController != null)
                    {
                        var manager = WindowManager.FindInScene();
                        if (manager != null)
                        {
                            manager.OpenWindow(_settingsMenuController);
                        }
                    }
                };
            }

            if (_leaveMatchButton != null)
            {
                _leaveMatchButton.clicked += () =>
                {
                };
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
            string code = relayLobby != null ? relayLobby.GetRoomCode() : "";
            
            if (_roomCodeContainer != null)
            {
                _roomCodeContainer.style.display = string.IsNullOrEmpty(code) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            
            if (_roomCode != null)
            {
                _roomCode.text = code;
            }
        }

        public override void SetVisible(bool visible)
        {
            if (_overlay == null) return;

            if (visible)
            {
                _CancelPanelAnimations();

                _overlay.style.display = DisplayStyle.Flex;
                _overlay.pickingMode = PickingMode.Position;

                
                _UpdateRoomCode();

                
                if (_backdrop != null)
                {
                    _backdrop.style.opacity = 1f;
                }

                
                if (_panel != null)
                {
                    _panel.style.opacity = 0f;
                    _panel.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));
                    _scheduledOpenAnim = _panel.schedule.Execute(() =>
                    {
                        _panelAnim = _panel.experimental.animation
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

                
                if (_backdrop != null)
                {
                    _backdrop.style.opacity = 0f;
                }

                
                if (_panel != null)
                {
                    _panelAnim = _panel.experimental.animation
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

        public override void Close()
        {
            
            base.Close();
        }
        #endregion
    }
}
