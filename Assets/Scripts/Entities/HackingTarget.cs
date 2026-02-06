using TUA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TUA.Entities
{
    public partial class HackingTarget : Entity
    {
        #region Serialized Fields
        [Header("Settings")]
        public Color color = Color.white;
        public string unlocalizedName = "target";

        [Header("References")]
        public Image hackingBackground;
        public GameObject hackingInformation;
        public GameObject hackFinishedEffect;
        public RectTransform hackingProgressFill;
        #endregion

        #region Properties
        public float HackingProgress { get; private set; }
        public bool IsHacked { get; private set; }
        public bool IsBeingHacked { get; private set; }
        #endregion

        #region Events
        public event System.Action<float> OnHackingProgressChangeEvent;
        public event System.Action<bool> OnIsHackedChangeEvent;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            hackingBackground.color = new Color(color.r, color.g, color.b, color.a);
            OnHackingProgressChangeEvent += _OnHackingProgressChanged;
            OnIsHackedChangeEvent += _OnIsHackedChanged;
            _OnHackingProgressChanged(0f);
            _OnIsHackedChanged(false);
        }

        private void OnDisable()
        {
            OnHackingProgressChangeEvent -= _OnHackingProgressChanged;
            OnIsHackedChangeEvent -= _OnIsHackedChanged;
        }

        private new void OnValidate()
        {
            if (!hackingBackground)
                return;
            hackingBackground.color = new Color(color.r, color.g, color.b, color.a);
        }
        #endregion

        #region Public Methods
        public void Server_SetHackingProgress(float progress)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetHackingProgress can only be called on server side");

            _Server_SetHackingProgressInternal(progress);
        }

        public void Server_SetIsHacked(bool isHacked)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetIsHacked can only be called on server side");

            _Server_SetIsHackedInternal(isHacked);
        }

        public void Server_SetIsBeingHacked(bool isBeingHacked)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetIsBeingHacked can only be called on server side");

            _Server_SetIsBeingHackedInternal(isBeingHacked);
        }
        #endregion

        #region Private Methods
        private void _OnIsHackedChanged(bool isHacked)
        {
            if (isHacked)
            {
                hackingBackground.color = new Color(0, 0, 0, 1);
                hackingInformation.SetActive(false);

                if (hackFinishedEffect)
                    hackFinishedEffect.SetActive(true);
            }
            else
                hackingInformation.SetActive(true);
        }

        private void _OnHackingProgressChanged(float progress)
        {
            hackingProgressFill.localScale = new Vector3(progress, 1f, 1f);
        }
        #endregion
    }
}
