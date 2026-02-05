using System;
using System.Collections.Generic;
using System.Reflection;
using TUA.I18n;
using TUA.Settings;
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
    public class SettingsMenuController : Window
    {
        #region Serialized Fields
        [Header("Pages")]
        [SerializeField] private List<SettingsAsset> _pages = new List<SettingsAsset>();
        [SerializeField] private int _startPageIndex;

        [Header("Widgets (UXML Templates)")]
        [SerializeField] private VisualTreeAsset _labelWidget;
        [SerializeField] private VisualTreeAsset _boolWidget;
        [SerializeField] private VisualTreeAsset _intWidget;
        [SerializeField] private VisualTreeAsset _floatWidget;
        [SerializeField] private VisualTreeAsset _stringWidget;
        #endregion

        #region Fields
        private IVisualElementScheduledItem _scheduledOpenAnim;
        private UnityEngine.UIElements.Experimental.ValueAnimation<float> _panelAnim;
        private VisualElement _tabs;
        private VisualElement _list;
        private Label _title;
        private Button _closeButton;
        private ScrollView _scrollView;
        private SettingsAsset _activePage;
        private readonly Dictionary<string, Action<SettingChanged>> _activeBindings = new Dictionary<string, Action<SettingChanged>>(StringComparer.Ordinal);
        private readonly List<Button> _tabButtons = new List<Button>();
        private EventCallback<GeometryChangedEvent> _onOverlayGeometryChanged;
        private bool _layoutFallbackApplied;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();

            
            if (_activePage != null)
                _activePage.OnAnyChangeEvent -= _OnSettingChanged;

            _activePage = null;
            _activeBindings.Clear();

            LocalizationManager.OnLanguageChangeEvent += _OnLanguageChanged;
        }

        protected virtual void OnDisable()
        {
            if (_activePage != null)
                _activePage.OnAnyChangeEvent -= _OnSettingChanged;
            
            LocalizationManager.OnLanguageChangeEvent -= _OnLanguageChanged;
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
            _EnsureWorldSpaceColliderIfNeeded();
            return _root?.Q<VisualElement>("SettingsOverlay");
        }
        protected override void _InitializeElements()
        {
            
            _onOverlayGeometryChanged ??= _ =>
            {
                if (_overlay == null) return;
                if (_overlay.style.display == DisplayStyle.None) return; 
                if (_layoutFallbackApplied) return;
                if (_overlay.resolvedStyle.width < 2f || _overlay.resolvedStyle.height < 2f)
                {
                    Debug.LogWarning("[SettingsMenuController] SettingsOverlay has near-zero size. Applying fallback layout styles.", this);
                    _ApplyFallbackLayoutStyles();
                    _layoutFallbackApplied = true;
                }
            };
            _overlay.UnregisterCallback<GeometryChangedEvent>(_onOverlayGeometryChanged);
            _overlay.RegisterCallback<GeometryChangedEvent>(_onOverlayGeometryChanged);

            
            _backdrop = _overlay.Q<VisualElement>("Backdrop");
            _panel = _overlay.Q<VisualElement>("SettingsPanel");
            _tabs = _overlay.Q<VisualElement>("Tabs");
            _list = _overlay.Q<VisualElement>("SettingsList");
            _scrollView = _overlay.Q<ScrollView>("SettingsScroll");
            _title = _overlay.Q<Label>("Title");
            _closeButton = _overlay.Q<Button>("CloseButton");

            if (_scrollView != null)
            {
                
                _scrollView.AddToClassList("tua-scrollview");
                
                
                _scrollView.contentContainer.style.width = Length.Percent(100);
                _scrollView.contentContainer.style.alignItems = Align.Stretch;
            }

            if (_list != null)
            {
                _list.style.width = Length.Percent(100);
                _list.style.flexGrow = 1f;
                _list.style.alignItems = Align.Stretch;
            }

            
            if (_panel != null && _overlay != null)
            {
                void UpdatePanelSize()
                {
                    if (_panel != null && _overlay != null)
                    {
                        float screenHeight = _overlay.resolvedStyle.height;
                        float panelHeight = screenHeight - 52f; 
                        _panel.style.height = panelHeight;
                        _panel.style.maxHeight = panelHeight;
                    }
                }
                
                _overlay.RegisterCallback<GeometryChangedEvent>(evt => UpdatePanelSize());
                
                
                if (_overlay.resolvedStyle.height > 0)
                {
                    UpdatePanelSize();
                }
            }

            _BuildTabs();
            int index = Mathf.Clamp(_startPageIndex, 0, Mathf.Max(0, _pages.Count - 1));
            if (_pages.Count > 0)
                _ShowPage(index);
        }

        protected override void _SetupCallbacks()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked -= _OnCloseClicked;
                _closeButton.clicked += _OnCloseClicked;
            }
        }

        private void _EnsureWorldSpaceColliderIfNeeded()
        {
            if (_uiDocument == null) return;

            
            
            try
            {
                var existing = _uiDocument.GetComponent<BoxCollider>();
                if (existing == null)
                    existing = _uiDocument.gameObject.AddComponent<BoxCollider>();

                existing.isTrigger = true;
                existing.center = Vector3.zero;
                existing.size = new Vector3(10f, 10f, 0.1f);

                
                
                var t = typeof(UIDocument);
                var field = t.GetField("m_WorldSpaceCollider", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var current = field.GetValue(_uiDocument) as Collider;
                    if (current == null)
                        field.SetValue(_uiDocument, existing);
                }
            }
            catch
            {
                
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

        public void ToggleVisible()
        {
            if (_overlay == null)
            {
                return;
            }
            bool isVisible = IsVisible;
            if (isVisible)
                Close();
            else
            {
                var manager = WindowManager.FindInScene();
                if (manager != null)
                    manager.OpenWindow(this);
                else
                    SetVisible(true);
            }
        }

        private void _OnCloseClicked()
        {
            Close();
        }

        public override void Close()
        {
            
            base.Close();
        }

        private void _ApplyFallbackLayoutStyles()
        {
            if (_overlay == null) return;

            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
        }

        private void _BuildTabs()
        {
            if (_tabs == null) return;

            _tabs.Clear();
            _tabButtons.Clear();

            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                int pageIndex = i;

                
                string displayName = _GetLocalized(page.UnlocalizedName, "settings." + page.UnlocalizedName.ToLowerInvariant());

                var btn = new Button(() => _ShowPage(pageIndex))
                {
                    text = displayName
                };
                btn.AddToClassList("tua-tab");
                _tabs.Add(btn);
                _tabButtons.Add(btn);
            }
        }

        private void _ShowPage(int index)
        {
            if (_pages == null || _pages.Count == 0) return;
            index = Mathf.Clamp(index, 0, _pages.Count - 1);

            var page = _pages[index];
            if (page == null) return;

            if (_activePage != null)
                _activePage.OnAnyChangeEvent -= _OnSettingChanged;

            _activePage = page;
            _activePage.OnAnyChangeEvent += _OnSettingChanged;

            _activeBindings.Clear();
            if (_list != null) _list.Clear();

            if (_title != null)
            {
                
                string key = "settings.title";
                _title.text = _GetLocalized("Settings", key);
            }

            
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var b = _tabButtons[i];
                if (b == null) continue;
                if (i == index) b.AddToClassList("tua-tab--active");
                else b.RemoveFromClassList("tua-tab--active");
            }

            var entries = page.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                if (!entry.Visible) continue;

                switch (entry.Type)
                {
                    case SettingType.Label:
                        _AddLabel(page, entry);
                        break;
                    case SettingType.Bool:
                        _AddBool(page, entry);
                        break;
                    case SettingType.Int:
                        _AddInt(page, entry);
                        break;
                    case SettingType.Float:
                        _AddFloat(page, entry);
                        break;
                    case SettingType.String:
                        _AddString(page, entry);
                        break;
                }
            }
        }

        private void _OnLanguageChanged()
        {
            
            if (_activePage != null)
            {
                
                int index = _pages.IndexOf(_activePage);
                _BuildTabs(); 
                _ShowPage(index); 
            }
            else
            {
                _BuildTabs();
            }
        }

        private string _GetLocalized(string defaultText, string key)
        {
            if (string.IsNullOrEmpty(key))
                return "";
            
            return LocalizationManager.Get(key);
        }

        private string _GetLabelText(SettingsAsset page, SettingEntry entry)
        {
            
            string pageName = page != null ? page.UnlocalizedName.ToLowerInvariant() : "unknown";
            string key = !string.IsNullOrEmpty(entry?.Key) ? entry.Key.ToLowerInvariant() : "";
            
            
            string fullKey = $"settings.{pageName}.{key}";
            return LocalizationManager.Get(fullKey);
        }

        private void _AddLabel(SettingsAsset page, SettingEntry entry)
        {
            if (_labelWidget == null || _list == null) return;

            var ve = _labelWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var text = ve.Q<Label>("Text");
            if (text != null) text.text = _GetLabelText(page, entry);
            _list.Add(root);
        }

        private void _AddBool(SettingsAsset page, SettingEntry entry)
        {
            if (_boolWidget == null || _list == null || !entry.IsKeyed) return;

            var ve = _boolWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var toggle = ve.Q<Toggle>("Toggle");

            if (label != null) label.text = _GetLabelText(page, entry);
            if (toggle != null)
            {
                bool value = page.GetBool(entry.Key, entry.DefaultBool);
                toggle.SetValueWithoutNotify(value);

                toggle.RegisterValueChangedCallback(evt =>
                {
                    page.SetBool(entry.Key, evt.newValue);
                    entry.Provider?.ApplyValue(SettingValue.FromBool(evt.newValue));
                });
            }

            _Bind(entry.Key, change =>
            {
                if (toggle == null) return;
                toggle.SetValueWithoutNotify(page.GetBool(entry.Key, entry.DefaultBool));
            });

            _list.Add(root);
        }

        private void _AddInt(SettingsAsset page, SettingEntry entry)
        {
            if (_intWidget == null || _list == null || !entry.IsKeyed) return;

            var ve = _intWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var slider = ve.Q<SliderInt>("Slider");
            var field = ve.Q<IntegerField>("Field");

            if (label != null) label.text = _GetLabelText(page, entry);

            bool hasClamp = entry.IntClamp;
            if (slider != null)
            {
                slider.style.display = hasClamp ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasClamp)
                {
                    slider.lowValue = entry.IntMin;
                    slider.highValue = entry.IntMax;
                }
            }

            void SetUI(int v)
            {
                field?.SetValueWithoutNotify(v);
                slider?.SetValueWithoutNotify(v);
            }

            int value = page.GetInt(entry.Key, entry.DefaultInt);
            SetUI(value);

            int step = Mathf.Max(1, entry.IntStep);
            int baseValue = hasClamp ? entry.IntMin : 0;

            if (slider != null)
            {
                slider.RegisterValueChangedCallback(evt =>
                {
                    int v = evt.newValue;
                    if (step > 1)
                        v = baseValue + Mathf.RoundToInt((v - baseValue) / (float)step) * step;
                    page.SetInt(entry.Key, v);
                    entry.Provider?.ApplyValue(SettingValue.FromInt(v));
                    // Update field to keep in sync
                    field?.SetValueWithoutNotify(v);
                });
            }

            if (field != null)
            {
                field.RegisterValueChangedCallback(evt =>
                {
                    int v = evt.newValue;
                    if (step > 1)
                        v = baseValue + Mathf.RoundToInt((v - baseValue) / (float)step) * step;
                    page.SetInt(entry.Key, v);
                    entry.Provider?.ApplyValue(SettingValue.FromInt(v));
                    // Update slider to keep in sync
                    slider?.SetValueWithoutNotify(v);
                });
            }

            _Bind(entry.Key, change =>
            {
                SetUI(page.GetInt(entry.Key, entry.DefaultInt));
            });

            _list.Add(root);
        }

        private void _AddFloat(SettingsAsset page, SettingEntry entry)
        {
            if (_floatWidget == null || _list == null || !entry.IsKeyed) return;

            var ve = _floatWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var slider = ve.Q<Slider>("Slider");
            var field = ve.Q<FloatField>("Field");

            if (label != null) label.text = _GetLabelText(page, entry);

            bool hasClamp = entry.FloatClamp;
            if (slider != null)
            {
                slider.style.display = hasClamp ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasClamp)
                {
                    slider.lowValue = entry.FloatMin;
                    slider.highValue = entry.FloatMax;
                }
            }

            void SetUI(float v)
            {
                field?.SetValueWithoutNotify(v);
                slider?.SetValueWithoutNotify(v);
            }

            float value = page.GetFloat(entry.Key, entry.DefaultFloat);
            SetUI(value);

            float step = Mathf.Max(0f, entry.FloatStep);
            float baseValue = hasClamp ? entry.FloatMin : 0f;

            float Snap(float v)
            {
                if (step <= 0.000001f) return v;
                return baseValue + Mathf.Round((v - baseValue) / step) * step;
            }

            if (slider != null)
            {
                slider.RegisterValueChangedCallback(evt =>
                {
                    float v = Snap(evt.newValue);
                    page.SetFloat(entry.Key, v);
                    entry.Provider?.ApplyValue(SettingValue.FromFloat(v));
                    // Update field to keep in sync
                    field?.SetValueWithoutNotify(v);
                });
            }

            if (field != null)
            {
                field.RegisterValueChangedCallback(evt =>
                {
                    float v = Snap(evt.newValue);
                    page.SetFloat(entry.Key, v);
                    entry.Provider?.ApplyValue(SettingValue.FromFloat(v));
                    // Update slider to keep in sync
                    slider?.SetValueWithoutNotify(v);
                });
            }

            _Bind(entry.Key, change =>
            {
                SetUI(page.GetFloat(entry.Key, entry.DefaultFloat));
            });

            _list.Add(root);
        }

        private void _AddString(SettingsAsset page, SettingEntry entry)
        {
            if (_stringWidget == null || _list == null || !entry.IsKeyed) return;

            var ve = _stringWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var dropdown = ve.Q<DropdownField>("Dropdown");
            var textField = ve.Q<TextField>("TextField");

            if (label != null) label.text = _GetLabelText(page, entry);

            string value = page.GetString(entry.Key, entry.DefaultString);
            string[] options = entry.StringOptions;

            
            if (entry.Provider != null)
            {
                var dynamicOpts = entry.Provider.GetStringOptions(page, entry);
                if (dynamicOpts != null && dynamicOpts.Length > 0)
                    options = dynamicOpts;
            }

            bool hasOptions = options != null && options.Length > 0;

            if (dropdown != null)
            {
                dropdown.style.display = hasOptions ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasOptions)
                {
                    dropdown.choices = new List<string>(options);
                    
                    
                    if (!dropdown.choices.Contains(value) && dropdown.choices.Count > 0)
                        value = dropdown.choices[0];
                        
                    dropdown.SetValueWithoutNotify(value);
                    dropdown.RegisterValueChangedCallback(evt =>
                    {
                        page.SetString(entry.Key, evt.newValue);
                        
                        entry.Provider?.ApplyValue(SettingValue.FromString(evt.newValue));
                    });
                }
            }

            if (textField != null)
            {
                textField.style.display = !hasOptions ? DisplayStyle.Flex : DisplayStyle.None;
                if (!hasOptions)
                {
                    textField.SetValueWithoutNotify(value);
                    textField.RegisterValueChangedCallback(evt =>
                    {
                        page.SetString(entry.Key, evt.newValue);
                        entry.Provider?.ApplyValue(SettingValue.FromString(evt.newValue));
                    });
                }
            }

            _Bind(entry.Key, change =>
            {
                string newVal = page.GetString(entry.Key, entry.DefaultString);
                dropdown?.SetValueWithoutNotify(newVal);
                textField?.SetValueWithoutNotify(newVal);
            });

            _list.Add(root);
        }

        private void _OnSettingChanged(SettingChanged change)
        {
            if (_activeBindings.TryGetValue(change.Key, out var action))
            {
                action?.Invoke(change);
            }
        }

        private void _Bind(string key, Action<SettingChanged> action)
        {
            if (_activeBindings.ContainsKey(key))
                _activeBindings[key] += action;
            else
                _activeBindings[key] = action;
        }
        #endregion
    }
}
