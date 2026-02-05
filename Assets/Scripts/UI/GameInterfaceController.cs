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
                    _OnHealthChanged(_currentHealthHolder.CurrentHealth, _currentHealthHolder.MaxHealth);
                }
                
                if (_currentWeaponUser)
                    _currentWeaponUser.OnReloadProgressChangeEvent -= _OnReloadProgressChanged;
                
                _currentWeaponUser = entity as PlayerEntity;
                if (_currentWeaponUser)
                {
                    _currentWeaponUser.OnReloadProgressChangeEvent += _OnReloadProgressChanged;
                    _OnReloadProgressChanged(_currentWeaponUser.ReloadProgress);
                }
                else
                {
                    _OnReloadProgressChanged(0f);
                }
            }
            if (_currentInventoryHolder != null)
                _UpdateItemInfo(_currentInventoryHolder.Inventory);
            
            if (HackingSystem.Instance)
                _UpdateHackingProgress();
            
            _UpdateHackingTargets();
            _UpdateTeamsAndTargetsVisibility();
            _UpdateReloadProgress();
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
        private void _UpdateTeamsAndTargetsVisibility()
        {
            if (_team1Row != null && _team1Players != null)
                _team1Row.style.display = _team1Players.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (_team2Row != null && _team2Players != null)
                _team2Row.style.display = _team2Players.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (_hackingTargetsRow != null && _hackingTargetsContainer != null)
                _hackingTargetsRow.style.display = _hackingTargetsContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
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
            
            if (string.IsNullOrEmpty(stack.item))
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
            _UpdateHealthUI(currentHealth, maxHealth);
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
        #endregion
    }
}
