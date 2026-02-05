using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

namespace TUA.Windowing
{
    public abstract class Window : MonoBehaviour
    {
        #region Serialized Fields
        [Header("UI")]
        [SerializeField] protected UIDocument _uiDocument;
        [Tooltip("If true, this window can be closed with ESC key.")]
        [SerializeField] protected bool _canCloseWithEsc = true;
        #endregion
        #region Fields
        protected VisualElement _root;
        protected VisualElement _overlay;
        protected VisualElement _backdrop;
        protected VisualElement _panel;
        #endregion
        #region Properties
        public bool IsVisible => _overlay != null && _overlay.style.display != DisplayStyle.None;
        public bool CanCloseWithEsc => _canCloseWithEsc;
        #endregion
        #region Unity Callbacks
        protected virtual void OnEnable()
        {
            if (_uiDocument == null)
                _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
                return;
            _root = _uiDocument.rootVisualElement;
            _overlay = _FindOverlay();
            if (_overlay == null)
                return;
            _InitializeElements();
            _SetupCallbacks();
            SetVisibleInstant(false);
        }
        #endregion
        #region Methods
        protected abstract VisualElement _FindOverlay();
        protected abstract void _InitializeElements();
        protected abstract void _SetupCallbacks();
        public virtual void SetVisible(bool visible)
        {
            if (_overlay == null) return;
            if (visible)
            {
                _overlay.style.display = DisplayStyle.Flex;
                _overlay.pickingMode = PickingMode.Position;
                _OnShow();
            }
            else
            {
                _OnHide();
                _overlay.style.display = DisplayStyle.None;
                _overlay.pickingMode = PickingMode.Ignore;
            }
        }
        public void SetVisibleInstant(bool visible)
        {
            if (_overlay == null) return;
            if (visible)
            {
                _overlay.style.display = DisplayStyle.Flex;
                _overlay.pickingMode = PickingMode.Position;
            }
            else
            {
                _overlay.style.display = DisplayStyle.None;
                _overlay.pickingMode = PickingMode.Ignore;
            }
        }
        protected virtual void _OnShow()
        {
        }
        protected virtual void _OnHide()
        {
        }
        public virtual void Close()
        {
            var manager = WindowManager.FindInScene();
            if (manager != null)
                manager.CloseWindow(this);
            else
                SetVisible(false);
        }
        #endregion
    }
}
