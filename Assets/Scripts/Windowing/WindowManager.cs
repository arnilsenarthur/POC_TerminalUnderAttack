using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TUA.Windowing
{
    public class WindowManager : MonoBehaviour
    {
        #region Serialized Fields
        [FormerlySerializedAs("_controlsMouse")]
        [Header("Mouse Control")]
        [Tooltip("Lock/unlock mouse when windows are open or not")]
        [SerializeField] private bool controlsMouse = true;
        [FormerlySerializedAs("_windowToOpenOnEsc")]
        [Header("ESC Key Behavior")]
        [Tooltip("Window to open when ESC is pressed and no windows are open. Leave null to do nothing.")]
        [SerializeField] private Window windowToOpenOnEsc;
        [FormerlySerializedAs("_fixedView")]
        [Header("Fixed View")]
        [Tooltip("UIDocument that should be turned on/off based on when windows are open")]
        [SerializeField] private UIDocument fixedView;
        [FormerlySerializedAs("_closeDelay")]
        [Header("Animation")]
        [Tooltip("Delay before disabling GameObject when closing (to allow fade-out animation)")]
        [SerializeField] private float closeDelay = 0.3f;
        #endregion
        
        #region Fields
        private VisualElement _fixedViewRoot;
        private Stack<Window> _windowStack = new();
        private readonly Dictionary<Window, Coroutine> _closeCoroutines = new();
        private Coroutine _pendingStateUpdate;
        #endregion
        
        #region Unity Callbacks
        private void Update()
        {
            if (_IsEscapePressed())
                _HandleEscape();
        }
        #endregion
        
        #region Methods
        private bool _IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
                return keyboard.escapeKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }
        
        private void _HandleEscape()
        {
            if (_windowStack.Count > 0)
            {
                var stackList = new List<Window>(_windowStack);
                for (int i = stackList.Count - 1; i >= 0; i--)
                {
                    Window window = stackList[i];
                    if (!window || !window.gameObject.activeSelf || !window.CanCloseWithEsc) 
                        continue;
                    
                    CloseWindow(window);
                    return;
                }
            }
            
            if (windowToOpenOnEsc && !windowToOpenOnEsc.gameObject.activeSelf)
                OpenWindow(windowToOpenOnEsc);
        }
        
        public void OpenWindow(Window window)
        {
            if (!window) 
                return;
            
            if (_closeCoroutines.TryGetValue(window, out var existingClose))
            {
                if (existingClose != null)
                    StopCoroutine(existingClose);
                
                _closeCoroutines.Remove(window);
            }
            
            var stackList = new List<Window>(_windowStack);
            foreach (var existingWindow in stackList.Where(existingWindow => existingWindow && existingWindow.gameObject.activeSelf))
            {
                existingWindow.SetVisibleInstant(false);
                existingWindow.gameObject.SetActive(false);
            }
            
            if (stackList.Contains(window))
                stackList.Remove(window);
            
            stackList.Add(window);
            _windowStack = new Stack<Window>(stackList);
            window.gameObject.SetActive(true);
            window.SetVisibleInstant(true);
            // Update cursor state immediately when opening window to allow clicking
            _UpdateState();
            StartCoroutine(_ShowWindowAfterFrame(window));
        }
        
        private IEnumerator _ShowWindowAfterFrame(Window window)
        {
            yield return null;
            if (window && window.gameObject.activeSelf)
                window.SetVisible(true);
        }
        
        public void CloseWindow(Window window)
        {
            if (!window) 
                return;
            
            window.SetVisible(false);
            var stackList = new List<Window>(_windowStack);
            stackList.Remove(window);
            _windowStack = new Stack<Window>(stackList);
            
            if (_closeCoroutines.TryGetValue(window, out var existingCoroutine))
            {
                if (existingCoroutine != null)
                    StopCoroutine(existingCoroutine);
            }
            
            // Cancel any pending state update to prevent double updates
            if (_pendingStateUpdate != null)
            {
                StopCoroutine(_pendingStateUpdate);
                _pendingStateUpdate = null;
            }
            
            var coroutine = StartCoroutine(_CloseWindowDelayed(window, closeDelay));
            _closeCoroutines[window] = coroutine;
            if (stackList.Count > 0)
            {
                var previousWindow = stackList[^1];
                if (previousWindow == null) 
                    return;
                
                previousWindow.gameObject.SetActive(true);
                previousWindow.SetVisibleInstant(true);
                // Wait for window animation to complete before updating state
                _pendingStateUpdate = StartCoroutine(_UpdateStateAfterWindowAnimation(previousWindow));
            }
            else
            {
                // Wait for close animation to complete before updating cursor state
                _pendingStateUpdate = StartCoroutine(_UpdateStateAfterCloseAnimation());
            }
        }
        
        private IEnumerator _CloseWindowDelayed(Window window, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (window)
                window.gameObject.SetActive(false);
            
            _closeCoroutines.Remove(window);
        }
        
        private IEnumerator _UpdateStateAfterFrame()
        {
            yield return null;
            var hasOpenWindow = _windowStack.Any(window => window && window.gameObject.activeSelf && window.IsVisible);
            
            if (!hasOpenWindow)
                _UpdateState();
        }
        
        private IEnumerator _UpdateStateAfterCloseAnimation()
        {
            // Wait for window close animation to complete
            // Most window animations are 300ms, so wait slightly longer to be safe
            yield return new WaitForSeconds(closeDelay + 0.05f);
            // Wait one more frame to ensure window visibility is fully updated
            yield return null;
            _UpdateState();
            _pendingStateUpdate = null;
        }
        
        private IEnumerator _UpdateStateAfterWindowAnimation(Window window)
        {
            // Wait one frame for window to be set visible
            yield return null;
            if (window && window.gameObject.activeSelf)
            {
                _UpdateState();
            }
            _pendingStateUpdate = null;
        }
        
        private void _UpdateState()
        {
            var hasOpenWindow = _windowStack.Any(window => window && window.gameObject.activeSelf && window.IsVisible);
            if (controlsMouse)
            {
                // Only update cursor state if it needs to change to prevent flickering
                var shouldLock = !hasOpenWindow;
                var currentlyLocked = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
                
                if (shouldLock != currentlyLocked)
                {
                    if (hasOpenWindow)
                    {
                        // Unlock cursor immediately when window opens to allow clicking
                        UnityEngine.Cursor.lockState = CursorLockMode.None;
                        UnityEngine.Cursor.visible = true;
                    }
                    else
                    {
                        // Lock cursor when no windows are open
                        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                        UnityEngine.Cursor.visible = false;
                    }
                }
            }

            if (!fixedView) 
                return;
            
            if (_fixedViewRoot == null && fixedView.rootVisualElement != null)
                _fixedViewRoot = fixedView.rootVisualElement;
            if (_fixedViewRoot != null)
                _fixedViewRoot.style.display = hasOpenWindow ? DisplayStyle.None : DisplayStyle.Flex;
            else
                fixedView.gameObject.SetActive(!hasOpenWindow);
        }
        
        public static WindowManager FindInScene()
        {
            return FindFirstObjectByType<WindowManager>();
        }

        public static bool HasOpenWindow()
        {
            var manager = FindInScene();
            if (!manager)
                return false;
            
            // Check windows in stack
            var hasOpenInStack = manager._windowStack.Any(window => window && window.gameObject.activeSelf && window.IsVisible);
            if (hasOpenInStack)
                return true;
            
            // Also check if any windows are in the process of closing but still visible (during animation)
            // This prevents cursor from locking during window close animations
            foreach (var window in manager._closeCoroutines.Keys)
            {
                if (window && window.gameObject.activeSelf && window.IsVisible)
                    return true;
            }
            
            return false;
        }
        #endregion
    }
}
