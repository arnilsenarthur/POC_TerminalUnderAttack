using System;
using System.Collections.Generic;
using System.Linq;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Entities;
using TUA.I18n;
using TUA.Items;
using TUA.Misc;
using TUA.Systems;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

namespace TUA.UI
{
    [MovedFrom("TUA.Interface")]
    public class GameInterfaceController : SingletonBehaviour<GameInterfaceController>
    {
        #region Serialized Fields
        [Header("UI References")]
        public UIDocument uiDocument;
        [Header("Templates")]
        public VisualTreeAsset inventorySlotTemplate;
        [Header("Data")]
        public Registry itemRegistry;
        [NonSerialized] [Header("Health Settings")]
        public Gradient HealthGradient = new()
        {
            colorKeys = new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.3f),
                new GradientColorKey(Color.yellow, 0.6f),
                new GradientColorKey(Color.green, 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };
        #endregion
        
        #region Fields
        private GamePlayer _currentTargetPlayer;
        private IReadOnlyList<Entity> _currentTargetEntities;
        private IInventoryHolder _currentInventoryHolder;
        private Entity _currentTargetEntity;
        private VisualElement _root;
        private VisualElement _inventoryDisplay;
        private readonly Dictionary<int, VisualElement> _inventorySlots = new();
        private VisualElement _itemInfoContainer;
        private readonly Dictionary<string, VisualElement> _itemInfoLayouts = new();
        private Label _ammoCountText;
        private Label _itemNameText;
        private VisualElement _hackingProgressFill;
        private Label _hackingProgressLabel;
        private VisualElement _reloadProgressBar;
        private VisualElement _reloadProgressFill;
        private Label _healthText;
        private PlayerEntity _currentWeaponUser;
        private VisualElement _healthBarContainer;
        private List<VisualElement> _healthSegments = new();
        private IHealth _currentHealthHolder;
        private VisualElement _hackingTargetsContainer;
        private VisualElement _hackingTargetsRow;
        private VisualElement _team1Row;
        private VisualElement _team2Row;
        private VisualElement _team1Players;
        private VisualElement _team2Players;
        private readonly Dictionary<HackingTarget, VisualElement> _hackingTargetDots = new();
        private readonly Dictionary<HackingTarget, Coroutine> _hackingFlashCoroutines = new();
        private Label _matchInfoMessageText;
        private Label _matchInfoTimerText;
        private readonly List<Uuid> _lastTeam1 = new();
        private readonly List<Uuid> _lastTeam2 = new();
        private VisualElement _damageFlashOverlay;
        private VisualElement _flashOverlay;
        private float _previousHealth = -1f;
        private Coroutine _damageFlashCoroutine;
        private Coroutine _flashCoroutine;
        private VisualElement _spectatorMenu;
        private Button _spectatorPrevButton;
        private Button _spectatorNextButton;
        private Label _spectatorNameLabel;
        
        [Header("Minimap")]
        [SerializeField] private float minimapImageScale = 0.35f;
        [SerializeField] private bool rotateMinimapWithCamera = true;
        private VisualElement _minimapElement;
        private Image _minimapImage;
        private Vector2 _minimapSize;
        #endregion
        
        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            LocalizationManager.OnLanguageChangeEvent += _OnLanguageChanged;
            _InitializeUI();
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            LocalizationManager.OnLanguageChangeEvent -= _OnLanguageChanged;
        }
        
        public void Update()
        {
            var entity = GameWorld.Instance?.GetTargetEntity<Entity>();
            if (entity != _currentTargetEntity)
            {
                if (_currentInventoryHolder != null)
                    _currentInventoryHolder.OnInventoryChangeEvent -= _OnInventoryChanged;
                
                _currentTargetEntity = entity;
                _currentInventoryHolder = entity as IInventoryHolder;
                
                if (_currentInventoryHolder != null)
                {
                    _currentInventoryHolder.OnInventoryChangeEvent += _OnInventoryChanged;
                    _OnInventoryChanged(_currentInventoryHolder.Inventory);
                }
                else
                    _ClearInventoryUI();
                
                if (_currentHealthHolder != null)
                    _currentHealthHolder.OnHealthChangeEvent -= _OnHealthChanged;
                
                _currentHealthHolder = entity as IHealth;
                
                if (_currentHealthHolder != null)
                {
                    _currentHealthHolder.OnHealthChangeEvent += _OnHealthChanged;
                    var initialHealth = _currentHealthHolder.CurrentHealth;
                    _previousHealth = initialHealth;
                    _OnHealthChanged(initialHealth, _currentHealthHolder.MaxHealth);
                }
                else
                    _previousHealth = -1f;
                
                if (_currentWeaponUser != null)
                    _currentWeaponUser.OnReloadProgressChangeEvent -= _OnReloadProgressChanged;
                
                _currentWeaponUser = entity as PlayerEntity;
                if (_currentWeaponUser != null)
                {
                    _currentWeaponUser.OnReloadProgressChangeEvent += _OnReloadProgressChanged;
                    _OnReloadProgressChanged(_currentWeaponUser.ReloadProgress);
                }
                else
                    _OnReloadProgressChanged(0f);
            }
            if (_currentInventoryHolder != null)
                _UpdateItemInfo(_currentInventoryHolder.Inventory);
            
            if (HackingSystem.Instance)
                _UpdateHackingProgress();
            
            _UpdateHackingTargets();
            _UpdateTeams();
            _UpdateTeamsAndTargetsVisibility();
            _UpdateReloadProgress();
            _UpdateSpectatorMenu();
            _UpdateMatchInfo();
            _UpdateMinimap();
        }
        #endregion
        
        #region Methods
        private void _OnLanguageChanged()
        {
            if (_currentInventoryHolder != null)
                _UpdateItemInfo(_currentInventoryHolder.Inventory);
            _UpdateHackingProgress();
        }
        
        private void _InitializeUI()
        {
            if (!uiDocument)
                uiDocument = FindFirstObjectByType<UIDocument>();
            
            if (!uiDocument)
            {
                Debug.LogWarning("[GameInterfaceController] No UIDocument found!");
                return;
            }
            
            _root = uiDocument.rootVisualElement;
            
            if (_root == null)
            {
                Debug.LogWarning("[GameInterfaceController] Root visual element is null!");
                return;
            }

            _damageFlashOverlay = _root.Q<VisualElement>("DamageFlashOverlay");
            _flashOverlay = _root.Q<VisualElement>("FlashOverlay");

            if (_damageFlashOverlay == null)
                Debug.LogWarning("[GameInterfaceController] DamageFlashOverlay not found in HUD UXML!");

            if (_flashOverlay == null)
                Debug.LogWarning("[GameInterfaceController] FlashOverlay not found in HUD UXML!");

            _minimapElement = _root.Q<VisualElement>("Minimap");
            _minimapImage = _minimapElement?.Q<Image>("MinimapImage");
            if (_minimapElement != null)
                _minimapElement.RegisterCallback<GeometryChangedEvent>(_OnMinimapGeometryChanged);
            if (_minimapImage != null)
                _minimapImage.style.position = Position.Absolute;
            
            var inventoryContainer = _root.Q<VisualElement>("InventoryContainer");
            _reloadProgressBar = inventoryContainer?.Q<VisualElement>("ReloadProgressBar");
            _reloadProgressFill = inventoryContainer?.Q<VisualElement>("ReloadProgressFill");
            
            if (_reloadProgressBar != null)
                _reloadProgressBar.style.display = DisplayStyle.None;
            
            _inventoryDisplay = inventoryContainer?.Q<VisualElement>("InventoryDisplay");
            
            if (_inventoryDisplay == null)
            {
                Debug.LogWarning("[GameInterfaceController] InventoryDisplay not found!");
                return;
            }
            
            if (inventorySlotTemplate == null)
                Debug.LogWarning("[GameInterfaceController] InventorySlotTemplate not assigned in inspector!");
            
            _itemInfoContainer = _root.Q<VisualElement>("ItemInfo");
            
            if (_itemInfoContainer != null)
            {
                _itemInfoLayouts["Weapon"] = _itemInfoContainer.Q<VisualElement>("WeaponInfoLayout");
                _itemInfoLayouts["HackerTool"] = _itemInfoContainer.Q<VisualElement>("HackerToolInfoLayout");
                _ammoCountText = _root.Q<Label>("AmmoCount");
                _itemNameText = _root.Q<Label>("ItemName");
                _hackingProgressFill = _root.Q<VisualElement>("HackingProgressFill");
                _hackingProgressLabel = _root.Q<Label>("HackingProgressLabel");
            }
            
            var lifeDisplay = _root.Q<VisualElement>("LifeDisplay");
            
            if (lifeDisplay != null)
            {
                _healthText = lifeDisplay.Q<Label>("HealthText");
                _healthBarContainer = lifeDisplay.Q<VisualElement>("HealthBarContainer");
                if (_healthBarContainer != null)
                {
                    for (var i = 1; i <= 5; i++)
                    {
                        var segment = _healthBarContainer.Q<VisualElement>($"Segment{i}");
                        if (segment != null)
                            _healthSegments.Add(segment);
                    }
                }
            }
            
            var matchDisplayContainer = _root.Q<VisualElement>("MatchDisplayContainer");
            if (matchDisplayContainer == null) 
                return;
            
            _team1Row = matchDisplayContainer.Q<VisualElement>("Team1");
            _team2Row = matchDisplayContainer.Q<VisualElement>("Team2");
            _team1Players = matchDisplayContainer.Q<VisualElement>("Team1Players");
            _team2Players = matchDisplayContainer.Q<VisualElement>("Team2Players");
            _hackingTargetsRow = matchDisplayContainer.Q<VisualElement>("HackingTargetsRow");
            _hackingTargetsContainer = matchDisplayContainer.Q<VisualElement>("HackingTargetsContainer");

            var matchInfo = matchDisplayContainer.Q<VisualElement>("MatchInfo");
            _matchInfoMessageText = matchInfo?.Q<Label>("RoundText");
            _matchInfoTimerText = matchInfo?.Q<Label>("TimerText");

            _team1Players?.Clear();
            _team2Players?.Clear();
            _lastTeam1.Clear();
            _lastTeam2.Clear();
            
            // Initialize spectator menu
            _spectatorMenu = _root.Q<VisualElement>("SpectatorMenu");
            if (_spectatorMenu != null)
            {
                _spectatorPrevButton = _spectatorMenu.Q<Button>("SpectatorPrev");
                _spectatorNextButton = _spectatorMenu.Q<Button>("SpectatorNext");
                _spectatorNameLabel = _spectatorMenu.Q<Label>("SpectatorName");
                
                if (_spectatorPrevButton != null)
                    _spectatorPrevButton.clicked += _OnSpectatorPrevClicked;
                
                if (_spectatorNextButton != null)
                    _spectatorNextButton.clicked += _OnSpectatorNextClicked;
                
                _spectatorMenu.style.display = DisplayStyle.None;
            }
        }

        private void _OnMinimapGeometryChanged(GeometryChangedEvent evt)
        {
            _minimapSize = evt.newRect.size;
        }

        private void _UpdateMinimap()
        {
            if (_minimapElement == null || _minimapImage == null)
                return;

            var minimapController = MinimapController.Instance;
            if (minimapController == null)
                return;

            var renderTexture = minimapController.TargetRenderTexture;
            if (renderTexture == null)
                return;

            if (_minimapImage.image != renderTexture)
                _minimapImage.image = renderTexture;

            _minimapImage.scaleMode = ScaleMode.StretchToFill;

            var maskWidth = _minimapSize.x > 0f ? _minimapSize.x : _minimapElement.resolvedStyle.width;
            var maskHeight = _minimapSize.y > 0f ? _minimapSize.y : _minimapElement.resolvedStyle.height;
            if (maskWidth <= 0f || maskHeight <= 0f)
                return;

            var minScaleX = renderTexture.width > 0 ? (maskWidth / renderTexture.width) : 1f;
            var minScaleY = renderTexture.height > 0 ? (maskHeight / renderTexture.height) : 1f;
            var minScale = Mathf.Max(minScaleX, minScaleY);
            var zoom = Mathf.Max(minimapImageScale, minScale);
            var imageWidth = renderTexture.width * zoom;
            var imageHeight = renderTexture.height * zoom;

            _minimapImage.style.width = imageWidth;
            _minimapImage.style.height = imageHeight;

            var cameraWorldPos = Vector3.zero;
            var cameraWorldRot = Quaternion.identity;
            if (CameraSystem.Instance != null && CameraSystem.Instance.mainCamera != null)
            {
                cameraWorldPos = CameraSystem.Instance.mainCamera.transform.position;
                cameraWorldRot = CameraSystem.Instance.mainCamera.transform.rotation;
            }
            else if (_currentTargetEntity != null)
            {
                _currentTargetEntity.GetCameraView(out cameraWorldPos, out cameraWorldRot, out _);
            }

            var cameraPixelTex = minimapController.WorldToMinimapPixel(cameraWorldPos);
            var cameraPixel = new Vector2(cameraPixelTex.x, renderTexture.height - cameraPixelTex.y) * zoom;
            var centerX = maskWidth * 0.5f;
            var centerY = maskHeight * 0.5f;

            var angleDeg = rotateMinimapWithCamera ? -cameraWorldRot.eulerAngles.y : 0f;
            _minimapImage.style.rotate = new Rotate(new Angle(angleDeg, AngleUnit.Degree));

            var dx = cameraPixel.x - (imageWidth * 0.5f);
            var dy = cameraPixel.y - (imageHeight * 0.5f);
            var rad = angleDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            var rotatedDx = (dx * cos) - (dy * sin);
            var rotatedDy = (dx * sin) + (dy * cos);

            var left = centerX - (imageWidth * 0.5f) - rotatedDx;
            var top = centerY - (imageHeight * 0.5f) - rotatedDy;

            _minimapImage.style.left = left;
            _minimapImage.style.top = top;
        }

        private void _UpdateMatchInfo()
        {
            if (_matchInfoMessageText == null && _matchInfoTimerText == null)
                return;

            var gameWorld = GameWorld.Instance;
            if (!gameWorld)
                return;

            if (_matchInfoMessageText != null)
                _matchInfoMessageText.text = gameWorld.MatchInfoMessage ?? string.Empty;

            if (_matchInfoTimerText != null)
            {
                if (!gameWorld.MatchInfoShowTime)
                {
                    _matchInfoTimerText.style.display = DisplayStyle.None;
                }
                else
                {
                    _matchInfoTimerText.style.display = DisplayStyle.Flex;
                    _matchInfoTimerText.text = _FormatTime(gameWorld.MatchInfoTimeSeconds);
                }
            }
        }

        private static string _FormatTime(float seconds)
        {
            if (seconds <= 0f)
                return "0:00";

            var totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
            var s = totalSeconds % 60;
            var totalMinutes = totalSeconds / 60;
            if (totalMinutes < 60)
                return $"{totalMinutes}:{s:00}";

            var m = totalMinutes % 60;
            var h = totalMinutes / 60;
            return $"{h}:{m:00}:{s:00}";
        }
        
        private void _UpdateHackingTargets()
        {
            if (_hackingTargetsContainer == null || !GameWorld.Instance)
                return;
            
            var allTargets = GameWorld.Instance.GetEntities<HackingTarget>();
            var currentTargetSet = new HashSet<HackingTarget>(allTargets);
            var targetsToRemove = (from kvp in _hackingTargetDots where !currentTargetSet.Contains(kvp.Key) select kvp.Key).ToList();

            foreach (var target in targetsToRemove)
            {
                if (_hackingTargetDots.TryGetValue(target, out var element))
                {
                    element?.RemoveFromHierarchy();
                    _hackingTargetDots.Remove(target);
                }

                if (!_hackingFlashCoroutines.TryGetValue(target, out var coroutine)) 
                    continue;
                
                if (coroutine != null)
                    StopCoroutine(coroutine);
                _hackingFlashCoroutines.Remove(target);
            }
            
            foreach (var target in allTargets)
            {
                if (!target)
                    continue;
                
                if (!_hackingTargetDots.ContainsKey(target))
                {
                    var dotElement = new VisualElement();
                    dotElement.AddToClassList("player-square");
                    const float dotSize = 20f;
                    dotElement.style.width = dotSize;
                    dotElement.style.height = dotSize;
                    var borderRadius = dotSize * 0.5f;
                    dotElement.style.borderTopLeftRadius = borderRadius;
                    dotElement.style.borderTopRightRadius = borderRadius;
                    dotElement.style.borderBottomLeftRadius = borderRadius;
                    dotElement.style.borderBottomRightRadius = borderRadius;
                    dotElement.style.backgroundColor = new StyleColor(target.color);
                    _hackingTargetsContainer.Add(dotElement);
                    _hackingTargetDots[target] = dotElement;
                }
                
                var dot = _hackingTargetDots[target];
                if (dot == null) 
                    continue;
                
                if (target.IsHacked)
                {
                    var darkColor = new Color(target.color.r * 0.3f, target.color.g * 0.3f, target.color.b * 0.3f, target.color.a);
                    dot.style.backgroundColor = new StyleColor(darkColor);
                    dot.style.opacity = 1f;
                }
                else if (target.IsBeingHacked)
                {
                    dot.style.backgroundColor = new StyleColor(target.color);
                    dot.style.opacity = 1f;
                    if (!_hackingFlashCoroutines.ContainsKey(target))
                        _hackingFlashCoroutines[target] = StartCoroutine(_FlashHackingTarget(target, dot));
                }
                else
                {
                    dot.style.backgroundColor = new StyleColor(target.color);
                    dot.style.opacity = 1f;
                    if (_hackingFlashCoroutines.TryGetValue(target, out var coroutine))
                    {
                        if (coroutine != null)
                            StopCoroutine(coroutine);
                        _hackingFlashCoroutines.Remove(target);
                    }
                }
            }
        }
        
        private System.Collections.IEnumerator _FlashHackingTarget(HackingTarget target, VisualElement dot)
        {
            while (target && target.IsBeingHacked && !target.IsHacked)
            {
                var alpha = 0.5f + Mathf.Sin(Time.time * 4f) * 0.5f;
                dot.style.opacity = alpha;
                yield return null;
            }
            
            if (dot != null)
                dot.style.opacity = 1f;
            
            _hackingFlashCoroutines.Remove(target);
        }

        private void _UpdateTeams()
        {
            if (_team1Players == null || _team2Players == null)
                return;

            var world = GameWorld.Instance;
            var allPlayers = world?.AllPlayers;
            if (allPlayers == null)
                return;

            var teamList = world.GetTeams();
            var team1Name = teamList is { Count: > 0 } ? teamList[0]?.Name : null;
            var team2Name = teamList is { Count: > 1 } ? teamList[1]?.Name : null;
            if (string.IsNullOrWhiteSpace(team1Name) && string.IsNullOrWhiteSpace(team2Name))
            {
                team1Name = "guards";
                team2Name = "invaders";
            }
            else if (string.IsNullOrWhiteSpace(team1Name))
            {
                team1Name = team2Name;
                team2Name = null;
            }

            var team1 = new List<GamePlayer>();
            var team2 = new List<GamePlayer>();

            foreach (var p in allPlayers)
            {
                if (p == null || !p.IsOnline || p.IsSpectator)
                    continue;

                if (!string.IsNullOrEmpty(team1Name) && string.Equals(p.TeamName, team1Name, StringComparison.OrdinalIgnoreCase))
                    team1.Add(p);
                else if (!string.IsNullOrEmpty(team2Name) && string.Equals(p.TeamName, team2Name, StringComparison.OrdinalIgnoreCase))
                    team2.Add(p);
            }

            team1 = team1.OrderBy(p => p.Name).ToList();
            team2 = team2.OrderBy(p => p.Name).ToList();

            var nextTeam1Uuids = team1.Select(p => p.Uuid).ToList();
            var nextTeam2Uuids = team2.Select(p => p.Uuid).ToList();

            if (!SequenceEqual(_lastTeam1, nextTeam1Uuids))
            {
                _lastTeam1.Clear();
                _lastTeam1.AddRange(nextTeam1Uuids);
                var c = world.GetTeamColor(team1Name);
                if (c.a <= 0f)
                    c = _GetFallbackTeamColor(team1Name);
                RebuildTeamUI(_team1Players, team1, c);
            }

            if (!SequenceEqual(_lastTeam2, nextTeam2Uuids))
            {
                _lastTeam2.Clear();
                _lastTeam2.AddRange(nextTeam2Uuids);
                var c = world.GetTeamColor(team2Name);
                if (c.a <= 0f)
                    c = _GetFallbackTeamColor(team2Name);
                RebuildTeamUI(_team2Players, team2, c);
            }

            return;

            static bool SequenceEqual(List<Uuid> a, List<Uuid> b)
            {
                if (ReferenceEquals(a, b))
                    return true;
                if (a == null || b == null)
                    return false;
                if (a.Count != b.Count)
                    return false;
                for (var i = 0; i < a.Count; i++)
                {
                    if (a[i] != b[i])
                        return false;
                }
                return true;
            }

            void RebuildTeamUI(VisualElement container, List<GamePlayer> players, Color fallbackColor)
            {
                container.Clear();

                foreach (var player in players)
                {
                    if (player == null)
                        continue;

                    var icon = new VisualElement();
                    icon.AddToClassList("player-square");

                    var avatar = GetPlayerAvatar(player);
                    if (avatar != null)
                        icon.style.backgroundImage = new StyleBackground(avatar);
                    else
                        icon.style.backgroundColor = new StyleColor(fallbackColor);

                    container.Add(icon);
                }
            }
        }

        private static Color _GetFallbackTeamColor(string teamName)
        {
            if (string.Equals(teamName, "guards", StringComparison.OrdinalIgnoreCase))
                return new Color(0f, 120f / 255f, 1f);
            if (string.Equals(teamName, "invaders", StringComparison.OrdinalIgnoreCase))
                return new Color(220f / 255f, 50f / 255f, 50f / 255f);
            return Color.gray;
        }

        private void _UpdateTeamsAndTargetsVisibility()
        {
            if (_team1Row != null && _team1Players != null)
                _team1Row.style.display = _team1Players.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (_team2Row != null && _team2Players != null)
                _team2Row.style.display = _team2Players.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (_hackingTargetsRow != null && _hackingTargetsContainer != null)
                _hackingTargetsRow.style.display = _hackingTargetsContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private Sprite GetPlayerAvatar(GamePlayer player)
        {
            return null;
        }
        
        private void _OnInventoryChanged(Inventory inventory)
        {
            _UpdateInventoryUI(inventory);
            _UpdateItemInfo(inventory);
        }
        
        private void _UpdateInventoryUI(Inventory inventory)
        {
            var slotCount = inventory.slots.Length;
            for (var i = 0; i < slotCount; i++)
            {
                if (!_inventorySlots.ContainsKey(i))
                {
                    if (!inventorySlotTemplate)
                    {
                        Debug.LogError("[GameInterfaceController] inventorySlotTemplate is null! Cannot create inventory slots.");
                        return;
                    }
                    
                    if (_inventoryDisplay == null)
                    {
                        Debug.LogError("[GameInterfaceController] _inventoryDisplay is null! Cannot add inventory slots.");
                        return;
                    }
                    
                    var slotElement = inventorySlotTemplate.Instantiate();
                    _inventoryDisplay.Add(slotElement);
                    _inventorySlots[i] = slotElement;
                }
            }
            
            if (_inventorySlots.Count > slotCount)
            {
                var keysToRemove = new List<int>();
                foreach (var kvp in _inventorySlots.Where(kvp => kvp.Key >= slotCount))
                {
                    kvp.Value?.RemoveFromHierarchy();
                    keysToRemove.Add(kvp.Key);
                }
                foreach (var key in keysToRemove)
                    _inventorySlots.Remove(key);
            }
            
            for (var i = 0; i < slotCount; i++)
            {
                if (!_inventorySlots.TryGetValue(i, out var slotElement))
                    continue;
                
                _UpdateSelectedSlot(slotElement, inventory.slots[i], i, inventory.selectedSlot == i);
                slotElement.style.marginRight = i == slotCount - 1 ? 0 : 5;
            }
        }
        
        private void _UpdateSelectedSlot(VisualElement slotElement, ItemStack stack, int slotIndex, bool isSelected)
        {
            if (slotElement == null) 
                return;
            
            if (isSelected)
                slotElement.AddToClassList("inventory-slot-selected");
            else
                slotElement.RemoveFromClassList("inventory-slot-selected");
            
            var slotNumberLabel = slotElement.Q<Label>("SlotNumber");
            if (slotNumberLabel != null)
                slotNumberLabel.text = (slotIndex + 1).ToString();
            var iconContainer = slotElement.Q<VisualElement>("Icon");
            var iconImage = slotElement.Q<VisualElement>("IconImage");

            if (iconContainer == null || iconImage == null) 
                return;
            
            if (stack == null || string.IsNullOrEmpty(stack.item))
            {
                iconImage.style.backgroundImage = null;
                const float slotHeight = 48f;
                const float borderWidth = 2f;
                const float paddingY = 6f;
                const float paddingX = 10f;
                var iconHeight = slotHeight - 2f * (borderWidth + paddingY);
                iconContainer.style.width = iconHeight;
                iconContainer.style.height = iconHeight;
                slotElement.style.width = iconHeight + 2f * (borderWidth + paddingX);
            }
            else
            {
                Item itemDef = null;
                if (itemRegistry)
                    itemDef = itemRegistry.GetEntry<Item>(stack.item);
                
                if (itemDef != null && itemDef.sprite)
                {
                    iconImage.style.backgroundImage = new StyleBackground(itemDef.sprite);
                    iconImage.style.opacity = 1f;
                    var inventoryHolder = _currentInventoryHolder;
                    var itemColor = itemDef.GetRenderColor(inventoryHolder, stack);
                    iconImage.style.unityBackgroundImageTintColor = new StyleColor(itemColor);
                    const float slotHeight = 48f;
                    const float borderWidth = 2f;
                    const float paddingY = 6f;
                    const float paddingX = 10f;
                    var aspectRatio = itemDef.sprite.rect.width / itemDef.sprite.rect.height;
                    var iconHeight = slotHeight - 2f * (borderWidth + paddingY);
                    var iconWidth = iconHeight * aspectRatio;
                    iconContainer.style.width = iconWidth;
                    iconContainer.style.height = iconHeight;
                    slotElement.style.width = iconWidth + 2f * (borderWidth + paddingX);
                }
                else
                {
                    iconImage.style.backgroundImage = null;
                    iconImage.style.opacity = 0.3f;
                    const float slotHeight = 48f;
                    const float borderWidth = 2f;
                    const float paddingY = 6f;
                    const float paddingX = 10f;
                    var iconHeight = slotHeight - 2f * (borderWidth + paddingY);
                    iconContainer.style.width = iconHeight;
                    iconContainer.style.height = iconHeight;
                    slotElement.style.width = iconHeight + 2f * (borderWidth + paddingX);
                }
            }
        }
        
        private void _UpdateItemInfo(Inventory inventory)
        {
            var selectedItem = inventory.GetSelectedItem();
            if (selectedItem == null || string.IsNullOrEmpty(selectedItem.item))
            {
                _ShowItemInfoLayout("Weapon");
                _ClearItemInfo();
                return;
            }
            var itemID = selectedItem.item;
            var locKey = $"item.{itemID.ToLowerInvariant()}";
            var itemName = LocalizationManager.Get(locKey);
            if (string.IsNullOrEmpty(itemName) || itemName.StartsWith("<"))
            {
                Item item = null;
                if (itemRegistry) 
                    item = itemRegistry.GetEntry<Item>(itemID);
                
                itemName = item ? item.GetDisplayLabel(selectedItem) : itemID;
            }
            switch (selectedItem)
            {
                case WeaponItemStack weaponStack:
                    _ShowItemInfoLayout("Weapon");
                    _UpdateWeaponInfo(weaponStack, itemName);
                    break;
                case GadgetItemStack gadgetStack:
                    _ShowItemInfoLayout("Weapon");
                    _UpdateGadgetInfo(gadgetStack, itemName);
                    break;
                default:
                    Item itemDef = null;
                    if (itemRegistry) itemDef = itemRegistry.GetEntry<Item>(selectedItem.item);
                    if (itemDef is HackerToolItem)
                    {
                        _ShowItemInfoLayout("HackerTool");
                        _UpdateHackerToolInfo();
                    }
                    else
                    {
                        _ShowItemInfoLayout("Weapon");
                        _UpdateDefaultItemInfo(itemName);
                    }
                    break;
            }
        }
        
        private void _ShowItemInfoLayout(string layoutName)
        {
            foreach (var kvp in _itemInfoLayouts.Where(kvp => kvp.Value != null))
            {
                kvp.Value.style.display = kvp.Key == layoutName ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        private void _UpdateWeaponInfo(WeaponItemStack weaponStack, string itemName)
        {
            if (_ammoCountText != null)
                _ammoCountText.text = $"{weaponStack.ammo} / {weaponStack.maxAmmo}";
            
            if (_itemNameText != null)
                _itemNameText.text = itemName;
        }
        
        private void _UpdateHackerToolInfo()
        {
            _UpdateHackingProgress();
        }
        
        private void _UpdateGadgetInfo(GadgetItemStack gadgetStack, string itemName)
        {
            if (_ammoCountText != null)
                _ammoCountText.text = gadgetStack.count.ToString();
            if (_itemNameText != null)
                _itemNameText.text = itemName;
        }

        private void _UpdateDefaultItemInfo(string itemName)
        {
            if (_ammoCountText != null)
                _ammoCountText.text = "";
            if (_itemNameText != null)
                _itemNameText.text = itemName;
        }
        
        private void _ClearItemInfo()
        {
            if (_ammoCountText != null)
                _ammoCountText.text = "";
            if (_itemNameText != null)
                _itemNameText.text = "";
        }
        
        private void _UpdateHackingProgress()
        {
            var hackingSystem = HackingSystem.Instance;
            if (!hackingSystem || !hackingSystem.CurrentHackingTarget)
            {
                if (_hackingProgressFill != null)
                    _hackingProgressFill.style.width = Length.Percent(0);
                
                if (_hackingProgressLabel != null)
                    _hackingProgressLabel.text = LocalizationManager.Get("hud.no_target_found");
                
                return;
            }
            
            var target = hackingSystem.CurrentHackingTarget;
            var progress = target.HackingProgress;
            progress = Mathf.Clamp01(progress);
            
            if (_hackingProgressFill != null)
            {
                _hackingProgressFill.style.width = Length.Percent(progress * 100f);
                _hackingProgressFill.style.backgroundColor = new StyleColor(target.color);
            }

            if (_hackingProgressLabel == null) 
                return;
            
            var progressText = LocalizationManager.Get("hud.hacking_progress");
            _hackingProgressLabel.text = $"{progressText} {Mathf.RoundToInt(progress * 100f)}%";
        }
        private void _ClearInventoryUI()
        {
            foreach (var slotElement in _inventorySlots.Values)
                slotElement?.RemoveFromHierarchy();
            
            _inventorySlots.Clear();
            
            if (_ammoCountText != null)
                _ammoCountText.text = "";
            
            if (_itemNameText != null)
                _itemNameText.text = "";
        }
        
        private void _OnHealthChanged(float currentHealth, float maxHealth)
        {
            // Check if damage was taken (health decreased)
            if (_previousHealth > 0f && currentHealth < _previousHealth)
            {
                _TriggerDamageFlash();
            }
            
            _previousHealth = currentHealth;
            _UpdateHealthUI(currentHealth, maxHealth);
        }
        
        private void _TriggerDamageFlash()
        {
            if (_damageFlashOverlay == null)
                return;
            
            // Stop existing flash coroutine if running
            if (_damageFlashCoroutine != null)
            {
                StopCoroutine(_damageFlashCoroutine);
            }
            
            _damageFlashCoroutine = StartCoroutine(_FlashDamageEffect());
        }
        
        private System.Collections.IEnumerator _FlashDamageEffect()
        {
            if (_damageFlashOverlay == null)
                yield break;
            
            // Show overlay with red color
            _damageFlashOverlay.style.display = DisplayStyle.Flex;
            _damageFlashOverlay.style.backgroundColor = new StyleColor(new Color(1f, 0f, 0f, 0.3f)); // Red with 30% opacity
            
            // Quick flash - fade out over 0.2 seconds
            float duration = 0.2f;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float alpha = Mathf.Lerp(0.3f, 0f, progress);
                _damageFlashOverlay.style.backgroundColor = new StyleColor(new Color(1f, 0f, 0f, alpha));
                yield return null;
            }
            
            // Hide overlay
            _damageFlashOverlay.style.display = DisplayStyle.None;
            _damageFlashOverlay.style.backgroundColor = new StyleColor(Color.clear);
            _damageFlashCoroutine = null;
        }

        public void TriggerFlashEffect(float duration)
        {
            if (_flashOverlay == null)
                return;

            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);

            _flashCoroutine = StartCoroutine(_FlashEffect(duration));
        }

        private System.Collections.IEnumerator _FlashEffect(float duration)
        {
            if (_flashOverlay == null)
                yield break;

            _flashOverlay.BringToFront();
            _flashOverlay.style.display = DisplayStyle.Flex;
            _flashOverlay.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 1f));

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                _flashOverlay.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, alpha));
                yield return null;
            }

            _flashOverlay.style.display = DisplayStyle.None;
            _flashOverlay.style.backgroundColor = new StyleColor(Color.clear);
            _flashCoroutine = null;
        }
        
        private void _OnReloadProgressChanged(float progress)
        {
            _UpdateReloadProgressUI(progress);
        }
        
        private void _UpdateReloadProgress()
        {
            _UpdateReloadProgressUI(_currentWeaponUser ? _currentWeaponUser.ReloadProgress : 0f);
        }
        
        private void _UpdateReloadProgressUI(float progress)
        {
            if (_reloadProgressBar == null || _reloadProgressFill == null)
                return;
            
            if (progress > 0f && progress < 1.0f)
            {
                _reloadProgressBar.style.display = DisplayStyle.Flex;
                _reloadProgressFill.style.width = Length.Percent(progress * 100f);
            }
            else
            {
                _reloadProgressBar.style.display = DisplayStyle.None;
                _reloadProgressFill.style.width = Length.Percent(0f);
            }
        }
        
        private void _UpdateHealthUI(float currentHealth, float maxHealth)
        {
            if (_healthText != null)
                _healthText.text = Mathf.RoundToInt(currentHealth).ToString();
            
            if (_healthBarContainer == null || _healthSegments.Count == 0)
                return;
            
            var healthPercentage = maxHealth > 0f ? currentHealth / maxHealth : 0f;
            healthPercentage = Mathf.Clamp01(healthPercentage);
            var healthColor = HealthGradient.Evaluate(healthPercentage);
            var healthPerSegment = maxHealth / _healthSegments.Count;
            for (var i = 0; i < _healthSegments.Count; i++)
            {
                var segment = _healthSegments[i];

                var fill = segment?.Q<VisualElement>("Fill");
                if (fill == null)
                    continue;
                
                var segmentStartHealth = i * healthPerSegment;
                var segmentEndHealth = (i + 1) * healthPerSegment;
                var segmentFill = 0f;
                
                if (currentHealth > segmentStartHealth)
                {
                    var segmentHealth = Mathf.Min(currentHealth, segmentEndHealth) - segmentStartHealth;
                    segmentFill = segmentHealth / healthPerSegment;
                }
                
                fill.style.width = Length.Percent(segmentFill * 100f);
                fill.style.backgroundColor = new StyleColor(healthColor);
            }
        }
        
        private void _UpdateSpectatorMenu()
        {
            if (_spectatorMenu == null)
                return;

            var localGamePlayer = GameWorld.Instance?.LocalGamePlayer;
            var isSpectating = localGamePlayer != null && localGamePlayer.IsSpectator;

            _spectatorMenu.style.display = isSpectating ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isSpectating)
                return;

            if (_spectatorNameLabel != null)
            {
                var targetEntity = GameWorld.Instance?.GetEntityByUuid<Entity>(localGamePlayer.SpectatorTargetUuid);
                if (targetEntity != null && targetEntity.GamePlayer != null)
                {
                    var targetName = targetEntity.GamePlayer.Name ?? LocalizationManager.Get("hud.unknown");
                    var spectatingText = LocalizationManager.Get("hud.spectating");
                    _spectatorNameLabel.text = $"{spectatingText}: {targetName.ToUpperInvariant()}";
                }
                else
                    _spectatorNameLabel.text = LocalizationManager.Get("hud.spectating_none");
            }
        }

        private void _OnSpectatorPrevClicked()
        {
            var localGamePlayer = GameWorld.Instance?.LocalGamePlayer;
            if (localGamePlayer == null || !localGamePlayer.IsSpectator)
                return;

            var gameMode = GameWorld.Instance?.GameMode;
            if (gameMode == null)
                return;

            var prevTargetUuid = gameMode.OnGetPrevSpectateTarget(localGamePlayer, GameWorld.Instance);
            if (prevTargetUuid.IsValid)
                GameWorld.Instance.Client_RequestSpectatorTarget(prevTargetUuid);
        }

        private void _OnSpectatorNextClicked()
        {
            var localGamePlayer = GameWorld.Instance?.LocalGamePlayer;
            if (localGamePlayer == null || !localGamePlayer.IsSpectator)
                return;

            var gameMode = GameWorld.Instance?.GameMode;
            if (gameMode == null)
                return;

            var nextTargetUuid = gameMode.OnGetNextSpectateTarget(localGamePlayer, GameWorld.Instance);
            if (nextTargetUuid.IsValid)
                GameWorld.Instance.Client_RequestSpectatorTarget(nextTargetUuid);
        }
        #endregion
    }
}
