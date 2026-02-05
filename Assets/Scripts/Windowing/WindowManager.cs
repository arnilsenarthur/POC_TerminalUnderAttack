using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TUA.Windowing
{
    public class WindowManager : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Mouse Control")]
        [Tooltip("Lock/unlock mouse when windows are open or not")]
        [SerializeField] private bool _controlsMouse = true;
        [Header("ESC Key Behavior")]
        [Tooltip("Window to open when ESC is pressed and no windows are open. Leave null to do nothing.")]
        [SerializeField] private Window _windowToOpenOnEsc;
        [Header("Fixed View")]
        [Tooltip("UIDocument that should be turned on/off based on when windows are open")]
        [SerializeField] private UIDocument _fixedView;
        [Header("Animation")]
        [Tooltip("Delay before disabling GameObject when closing (to allow fade-out animation)")]
        [SerializeField] private float _closeDelay = 0.3f;
        #endregion
        #region Fields
        private VisualElement _fixedViewRoot;
        private Stack<Window> _windowStack = new Stack<Window>();
        private Dictionary<Window, Coroutine> _closeCoroutines = new Dictionary<Window, Coroutine>();
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
                    if (window != null && window.gameObject.activeSelf && window.CanCloseWithEsc)
                    {
                        CloseWindow(window);
                        return;
                    }
                }
            }
            if (_windowToOpenOnEsc != null && !_windowToOpenOnEsc.gameObject.activeSelf)
                OpenWindow(_windowToOpenOnEsc);
        }
        public void OpenWindow(Window window)
        {
            if (window == null) return;
            if (_closeCoroutines.TryGetValue(window, out var existingClose))
            {
                if (existingClose != null)
                    StopCoroutine(existingClose);
                _closeCoroutines.Remove(window);
            }
            var stackList = new List<Window>(_windowStack);
            foreach (var existingWindow in stackList)
            {
                if (existingWindow != null && existingWindow.gameObject.activeSelf)
                {
                    existingWindow.SetVisibleInstant(false);
                    existingWindow.gameObject.SetActive(false);
                }
            }
            if (stackList.Contains(window))
                stackList.Remove(window);
            stackList.Add(window);
            _windowStack = new Stack<Window>(stackList);
            window.gameObject.SetActive(true);
            window.SetVisibleInstant(true);
            _UpdateState();
            StartCoroutine(_ShowWindowAfterFrame(window));
        }
        private IEnumerator _ShowWindowAfterFrame(Window window)
        {
            yield return null;
            if (window != null && window.gameObject.activeSelf)
                window.SetVisible(true);
        }
        public void CloseWindow(Window window)
        {
            if (window == null) return;
            window.SetVisible(false);
            var stackList = new List<Window>(_windowStack);
            stackList.Remove(window);
            _windowStack = new Stack<Window>(stackList);
            if (_closeCoroutines.TryGetValue(window, out var existingCoroutine))
            {
                if (existingCoroutine != null)
                    StopCoroutine(existingCoroutine);
            }
            var coroutine = StartCoroutine(_CloseWindowDelayed(window, _closeDelay));
            _closeCoroutines[window] = coroutine;
            if (stackList.Count > 0)
            {
                Window previousWindow = stackList[stackList.Count - 1];
                if (previousWindow != null)
                {
                    previousWindow.gameObject.SetActive(true);
                    previousWindow.SetVisibleInstant(true);
                    _UpdateState();
                    StartCoroutine(_ShowWindowAfterFrame(previousWindow));
                }
            }
            else
                StartCoroutine(_UpdateStateAfterFrame());
        }
        private IEnumerator _CloseWindowDelayed(Window window, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (window != null)
                window.gameObject.SetActive(false);
            if (_closeCoroutines.ContainsKey(window))
                _closeCoroutines.Remove(window);
        }
        private IEnumerator _UpdateStateAfterFrame()
        {
            yield return null;
            bool hasOpenWindow = false;
            foreach (var window in _windowStack)
            {
                if (window != null && window.gameObject.activeSelf && window.IsVisible)
                {
                    hasOpenWindow = true;
                    break;
                }
            }
            if (!hasOpenWindow)
                _UpdateState();
        }
        private void _UpdateState()
        {
            bool hasOpenWindow = false;
            foreach (var window in _windowStack)
            {
                if (window != null && window.gameObject.activeSelf && window.IsVisible)
                {
                    hasOpenWindow = true;
                    break;
                }
            }
            if (_controlsMouse)
            {
                if (hasOpenWindow)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
            if (_fixedView != null)
            {
                if (_fixedViewRoot == null && _fixedView.rootVisualElement != null)
                    _fixedViewRoot = _fixedView.rootVisualElement;
                if (_fixedViewRoot != null)
                    _fixedViewRoot.style.display = hasOpenWindow ? DisplayStyle.None : DisplayStyle.Flex;
                else
                    _fixedView.gameObject.SetActive(!hasOpenWindow);
            }
        }
        public static WindowManager FindInScene()
        {
            return FindFirstObjectByType<WindowManager>();
        }

        /// <summary>
        /// Checks if any window is currently open and visible.
        /// </summary>
        public static bool HasOpenWindow()
        {
            var manager = FindInScene();
            if (manager == null)
                return false;
            
            foreach (var window in manager._windowStack)
            {
                if (window != null && window.gameObject.activeSelf && window.IsVisible)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
