using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace TUA.Windowing
{
    public abstract class Window : MonoBehaviour
    {
        #region Serialized Fields
        [FormerlySerializedAs("_uiDocument")]
        [Header("UI")]
        [SerializeField] protected UIDocument uiDocument;
        [FormerlySerializedAs("_canCloseWithEsc")]
        [Tooltip("If true, this window can be closed with ESC key.")]
        [SerializeField] protected bool canCloseWithEsc = true;
        #endregion
        
        #region Fields
        protected VisualElement Root;
        protected VisualElement Overlay;
        protected VisualElement Backdrop;
        protected VisualElement Panel;
        #endregion
        
        #region Properties
        public bool IsVisible => Overlay != null && Overlay.style.display != DisplayStyle.None;
        public bool CanCloseWithEsc => canCloseWithEsc;
        #endregion
        
        #region Unity Callbacks
        protected virtual void OnEnable()
        {
            if (!uiDocument)
                uiDocument = GetComponent<UIDocument>();
            
            if (!uiDocument || uiDocument.rootVisualElement == null)
                return;
            
            Root = uiDocument.rootVisualElement;
            Overlay = _FindOverlay();
            if (Overlay == null)
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
            if (Overlay == null) return;
            if (visible)
            {
                Overlay.style.display = DisplayStyle.Flex;
                Overlay.pickingMode = PickingMode.Position;
                _OnShow();
            }
            else
            {
                _OnHide();
                Overlay.style.display = DisplayStyle.None;
                Overlay.pickingMode = PickingMode.Ignore;
            }
        }
        public void SetVisibleInstant(bool visible)
        {
            if (Overlay == null) return;
            if (visible)
            {
                Overlay.style.display = DisplayStyle.Flex;
                Overlay.pickingMode = PickingMode.Position;
            }
            else
            {
                Overlay.style.display = DisplayStyle.None;
                Overlay.pickingMode = PickingMode.Ignore;
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
