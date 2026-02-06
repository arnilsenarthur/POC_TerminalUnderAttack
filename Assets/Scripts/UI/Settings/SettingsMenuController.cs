using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TUA.I18n;
using TUA.Settings;
using TUA.Windowing;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace TUA.UI
{
    [RequireComponent(typeof(UIDocument))]
    [MovedFrom("TUA.Interface")]
    public class SettingsMenuController : Window
    {
        #region Serialized Fields
        [FormerlySerializedAs("_pages")]
        [Header("Pages")]
        [SerializeField] private List<SettingsAsset> pages = new();
        [FormerlySerializedAs("_startPageIndex")] [SerializeField] private int startPageIndex;

        [FormerlySerializedAs("_labelWidget")]
        [Header("Widgets (UXML Templates)")]
        [SerializeField] private VisualTreeAsset labelWidget;
        [FormerlySerializedAs("_boolWidget")] [SerializeField] private VisualTreeAsset boolWidget;
        [FormerlySerializedAs("_intWidget")] [SerializeField] private VisualTreeAsset intWidget;
        [FormerlySerializedAs("_floatWidget")] [SerializeField] private VisualTreeAsset floatWidget;
        [FormerlySerializedAs("_stringWidget")] [SerializeField] private VisualTreeAsset stringWidget;
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
        private readonly Dictionary<string, Action<SettingChanged>> _activeBindings = new(StringComparer.Ordinal);
        private readonly List<Button> _tabButtons = new();
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
                try { _panelAnim.Stop(); }
                catch
                {
                    // ignored
                }

                _panelAnim = null;
            }
        }

        protected override VisualElement _FindOverlay()
        {
            _EnsureWorldSpaceColliderIfNeeded();
            return Root?.Q<VisualElement>("SettingsOverlay");
        }
        protected override void _InitializeElements()
        {
            _onOverlayGeometryChanged ??= _ =>
            {
                if (Overlay == null) return;
                if (Overlay.style.display == DisplayStyle.None) return; 
                if (_layoutFallbackApplied) return;
                if (Overlay.resolvedStyle.width < 2f || Overlay.resolvedStyle.height < 2f)
                {
                    Debug.LogWarning("[SettingsMenuController] SettingsOverlay has near-zero size. Applying fallback layout styles.", this);
                    _ApplyFallbackLayoutStyles();
                    _layoutFallbackApplied = true;
                }
            };
            Overlay.UnregisterCallback(_onOverlayGeometryChanged);
            Overlay.RegisterCallback(_onOverlayGeometryChanged);

            
            Backdrop = Overlay.Q<VisualElement>("Backdrop");
            Panel = Overlay.Q<VisualElement>("SettingsPanel");
            _tabs = Overlay.Q<VisualElement>("Tabs");
            _list = Overlay.Q<VisualElement>("SettingsList");
            _scrollView = Overlay.Q<ScrollView>("SettingsScroll");
            _title = Overlay.Q<Label>("Title");
            _closeButton = Overlay.Q<Button>("CloseButton");

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
            
            if (Panel != null && Overlay != null)
            {
                void UpdatePanelSize()
                {
                    if (Panel != null && Overlay != null)
                    {
                        var screenHeight = Overlay.resolvedStyle.height;
                        var panelHeight = screenHeight - 52f; 
                        Panel.style.height = panelHeight;
                        Panel.style.maxHeight = panelHeight;
                    }
                }
                
                Overlay.RegisterCallback<GeometryChangedEvent>(_ => UpdatePanelSize());
                
                if (Overlay.resolvedStyle.height > 0)
                    UpdatePanelSize();
            }

            _BuildTabs();
            var index = Mathf.Clamp(startPageIndex, 0, Mathf.Max(0, pages.Count - 1));
            if (pages.Count > 0)
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
            if (!uiDocument) 
                return;
            
            try
            {
                var existing = uiDocument.GetComponent<BoxCollider>();
                if (!existing)
                    existing = uiDocument.gameObject.AddComponent<BoxCollider>();

                existing.isTrigger = true;
                existing.center = Vector3.zero;
                existing.size = new Vector3(10f, 10f, 0.1f);
                
                var t = typeof(UIDocument);
                var field = t.GetField("m_WorldSpaceCollider", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var current = field.GetValue(uiDocument) as Collider;
                    if (!current)
                        field.SetValue(uiDocument, existing);
                }
            }
            catch
            {
                // ignored
            }
        }

        public override void SetVisible(bool visible)
        {
            if (Overlay == null) 
                return;
            
            if (visible)
            {
                _CancelPanelAnimations();
                
                Overlay.style.display = DisplayStyle.Flex;
                Overlay.pickingMode = PickingMode.Position;
                
                if (Backdrop != null)
                    Backdrop.style.opacity = 1f;

                if (Panel == null) 
                    return;
                
                Panel.style.opacity = 0f;
                Panel.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));
                _scheduledOpenAnim = Panel.schedule.Execute(() =>
                {
                    _panelAnim = Panel.experimental.animation
                        .Start(0f, 1f, 300, (element, value) =>
                        {
                            element.style.opacity = value;
                            var scale = 0.9f + (value * 0.1f); 
                            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
                        })
                        .KeepAlive()
                        .OnCompleted(() => _panelAnim = null);
                });
                _scheduledOpenAnim.ExecuteLater(1);
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
                            var scale = 0.9f + (value * 0.1f); 
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
            if (Overlay == null)
            {
                return;
            }
            var isVisible = IsVisible;
            if (isVisible)
                Close();
            else
            {
                var manager = WindowManager.FindInScene();
                if (manager)
                    manager.OpenWindow(this);
                else
                    SetVisible(true);
            }
        }

        private void _OnCloseClicked()
        {
            Close();
        }
        
        private void _ApplyFallbackLayoutStyles()
        {
            if (Overlay == null) return;

            Overlay.style.position = Position.Absolute;
            Overlay.style.left = 0;
            Overlay.style.right = 0;
            Overlay.style.top = 0;
            Overlay.style.bottom = 0;
            Overlay.style.justifyContent = Justify.Center;
            Overlay.style.alignItems = Align.Center;
        }

        private void _BuildTabs()
        {
            if (_tabs == null) return;

            _tabs.Clear();
            _tabButtons.Clear();

            for (var i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                var pageIndex = i;
                var displayName = LocalizationManager.Get("settings." + page.UnlocalizedName.ToLowerInvariant());

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
            if (pages == null || pages.Count == 0) 
                return;
            
            index = Mathf.Clamp(index, 0, pages.Count - 1);

            var page = pages[index];
            if (!page) 
                return;

            if (_activePage)
                _activePage.OnAnyChangeEvent -= _OnSettingChanged;

            _activePage = page;
            _activePage.OnAnyChangeEvent += _OnSettingChanged;

            _activeBindings.Clear();
            _list?.Clear();

            if (_title != null)
                _title.text = LocalizationManager.Get("settings.title");
            
            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var b = _tabButtons[i];
                if (b == null) continue;
                if (i == index) b.AddToClassList("tua-tab--active");
                else b.RemoveFromClassList("tua-tab--active");
            }

            var entries = page.Entries;
            foreach (var entry in entries)
            {
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
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void _OnLanguageChanged()
        {
            if (_activePage)
            {
                var index = pages.IndexOf(_activePage);
                _BuildTabs(); 
                _ShowPage(index); 
            }
            else
                _BuildTabs();
        }
        
        private string _GetLabelText(SettingsAsset page, SettingEntry entry)
        {
            var pageName = page == null ? page.UnlocalizedName.ToLowerInvariant() : "unknown";
            var key = !string.IsNullOrEmpty(entry?.Key) ? entry.Key.ToLowerInvariant() : "";
            
            var fullKey = $"settings.{pageName}.{key}";
            return LocalizationManager.Get(fullKey);
        }

        private void _AddLabel(SettingsAsset page, SettingEntry entry)
        {
            if (!labelWidget || _list == null) 
                return;

            var ve = labelWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var text = ve.Q<Label>("Text");
            if (text != null) text.text = _GetLabelText(page, entry);
            _list.Add(root);
        }

        private void _AddBool(SettingsAsset page, SettingEntry entry)
        {
            if (!boolWidget || _list == null || !entry.IsKeyed) 
                return;

            var ve = boolWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var toggle = ve.Q<Toggle>("Toggle");

            if (label != null) label.text = _GetLabelText(page, entry);
            if (toggle != null)
            {
                var value = page.GetBool(entry.Key, entry.DefaultBool);
                toggle.SetValueWithoutNotify(value);

                toggle.RegisterValueChangedCallback(evt =>
                {
                    page.SetBool(entry.Key, evt.newValue);
                    entry.Provider?.ApplyValue(SettingValue.FromBool(evt.newValue));
                });
            }

            _Bind(entry.Key, _ =>
            {
                if (toggle == null) return;
                toggle.SetValueWithoutNotify(page.GetBool(entry.Key, entry.DefaultBool));
            });

            _list.Add(root);
        }

        private void _AddInt(SettingsAsset page, SettingEntry entry)
        {
            if (!intWidget || _list == null || !entry.IsKeyed) 
                return;

            var ve = intWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var slider = ve.Q<SliderInt>("Slider");
            var field = ve.Q<IntegerField>("Field");

            if (label != null) 
                label.text = _GetLabelText(page, entry);

            var hasClamp = entry.IntClamp;
            if (slider != null)
            {
                slider.style.display = hasClamp ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasClamp)
                {
                    slider.lowValue = entry.IntMin;
                    slider.highValue = entry.IntMax;
                }
            }

            if (field != null)
            {
                field.isDelayed = true;
            }

            void SetUI(int v)
            {
                if (field != null && !field.isReadOnly)
                    field.SetValueWithoutNotify(v);
                slider?.SetValueWithoutNotify(v);
            }

            var value = page.GetInt(entry.Key, entry.DefaultInt);
            SetUI(value);

            var step = Mathf.Max(1, entry.IntStep);
            var baseValue = hasClamp ? entry.IntMin : 0;
            var isFieldFocused = false;

            slider?.RegisterValueChangedCallback(evt =>
            {
                if (isFieldFocused)
                    return;

                var v = evt.newValue;
                if (step > 1)
                    v = baseValue + Mathf.RoundToInt((v - baseValue) / (float)step) * step;
                page.SetInt(entry.Key, v);
                entry.Provider?.ApplyValue(SettingValue.FromInt(v));
                field?.SetValueWithoutNotify(v);
            });

            if (field != null)
            {
                field.RegisterCallback<FocusInEvent>(_ => isFieldFocused = true);
                field.RegisterCallback<FocusOutEvent>(evt =>
                {
                    isFieldFocused = false;
                    var text = field.text;
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        var v = parsed;
                        if (hasClamp)
                            v = Mathf.Clamp(v, entry.IntMin, entry.IntMax);
                        if (step > 1)
                            v = baseValue + Mathf.RoundToInt((v - baseValue) / (float)step) * step;
                        page.SetInt(entry.Key, v);
                        entry.Provider?.ApplyValue(SettingValue.FromInt(v));
                        SetUI(v);
                    }
                    else
                    {
                        SetUI(page.GetInt(entry.Key, entry.DefaultInt));
                    }
                });

                field.RegisterValueChangedCallback(evt =>
                {
                    if (!isFieldFocused)
                        return;

                    var v = evt.newValue;
                    if (hasClamp)
                        v = Mathf.Clamp(v, entry.IntMin, entry.IntMax);
                    if (step > 1)
                        v = baseValue + Mathf.RoundToInt((v - baseValue) / (float)step) * step;
                    page.SetInt(entry.Key, v);
                    entry.Provider?.ApplyValue(SettingValue.FromInt(v));
                    slider?.SetValueWithoutNotify(v);
                });
            }

            _Bind(entry.Key, _ =>
            {
                SetUI(page.GetInt(entry.Key, entry.DefaultInt));
            });

            _list.Add(root);
        }

        private void _AddFloat(SettingsAsset page, SettingEntry entry)
        {
            if (!floatWidget || _list == null || !entry.IsKeyed) 
                return;

            var ve = floatWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var slider = ve.Q<Slider>("Slider");
            var field = ve.Q<FloatField>("Field");

            if (label != null) 
                label.text = _GetLabelText(page, entry);

            var hasClamp = entry.FloatClamp;
            if (slider != null)
            {
                slider.style.display = hasClamp ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasClamp)
                {
                    slider.lowValue = entry.FloatMin;
                    slider.highValue = entry.FloatMax;
                }
            }

            if (field != null)
            {
                field.isDelayed = true;
            }

            void SetUI(float v)
            {
                if (field != null && !field.isReadOnly)
                    field.SetValueWithoutNotify(v);
                slider?.SetValueWithoutNotify(v);
            }

            var value = page.GetFloat(entry.Key, entry.DefaultFloat);
            SetUI(value);

            var step = Mathf.Max(0f, entry.FloatStep);
            var baseValue = hasClamp ? entry.FloatMin : 0f;
            var isFieldFocused = false;

            float Snap(float v)
            {
                if (step <= 0.000001f) return v;
                return baseValue + Mathf.Round((v - baseValue) / step) * step;
            }

            slider?.RegisterValueChangedCallback(evt =>
            {
                if (isFieldFocused)
                    return;

                var v = Snap(evt.newValue);
                page.SetFloat(entry.Key, v);
                entry.Provider?.ApplyValue(SettingValue.FromFloat(v));
                field?.SetValueWithoutNotify(v);
            });

            if (field != null)
            {
                field.RegisterCallback<FocusInEvent>(_ => isFieldFocused = true);
                field.RegisterCallback<FocusOutEvent>(evt =>
                {
                    isFieldFocused = false;
                    var text = field.text;
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        var v = parsed;
                        if (hasClamp)
                            v = Mathf.Clamp(v, entry.FloatMin, entry.FloatMax);
                        v = Snap(v);
                        page.SetFloat(entry.Key, v);
                        entry.Provider?.ApplyValue(SettingValue.FromFloat(v));
                        SetUI(v);
                    }
                    else
                    {
                        SetUI(page.GetFloat(entry.Key, entry.DefaultFloat));
                    }
                });

                field.RegisterValueChangedCallback(evt =>
                {
                    if (!isFieldFocused)
                        return;

                    var v = evt.newValue;
                    if (hasClamp)
                        v = Mathf.Clamp(v, entry.FloatMin, entry.FloatMax);
                    v = Snap(v);
                    page.SetFloat(entry.Key, v);
                    entry.Provider?.ApplyValue(SettingValue.FromFloat(v));
                    slider?.SetValueWithoutNotify(v);
                });
            }

            _Bind(entry.Key, _ =>
            {
                SetUI(page.GetFloat(entry.Key, entry.DefaultFloat));
            });

            _list.Add(root);
        }

        private void _AddString(SettingsAsset page, SettingEntry entry)
        {
            if (!stringWidget || _list == null || !entry.IsKeyed) 
                return;

            var ve = stringWidget.Instantiate();
            var root = ve.Q<VisualElement>("Root") ?? ve;
            root.style.width = Length.Percent(100);
            var label = ve.Q<Label>("Label");
            var dropdown = ve.Q<DropdownField>("Dropdown");
            var textField = ve.Q<TextField>("TextField");

            if (label != null) 
                label.text = _GetLabelText(page, entry);

            var value = page.GetString(entry.Key, entry.DefaultString);
            var options = entry.StringOptions;
            
            if (entry.Provider)
            {
                var dynamicOpts = entry.Provider.GetStringOptions(page, entry);
                if (dynamicOpts != null && dynamicOpts.Length > 0)
                    options = dynamicOpts;
            }

            var hasOptions = options != null && options.Length > 0;
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

            _Bind(entry.Key, _ =>
            {
                var newVal = page.GetString(entry.Key, entry.DefaultString);
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
