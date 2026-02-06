using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUA.Misc;
using TUA.Systems;
using TUA.I18n;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UIElements;

namespace TUA.UI
{
    [MovedFrom("TUA.Interface")]
    public class FeedController : SingletonBehaviour<FeedController>
    {
        #region Constants
        private const int MaxFeedItems = 10;
        #endregion

        #region Serialized Fields
        [Header("UI References")]
        public UIDocument uiDocument;
        [Header("Templates")]
        public VisualTreeAsset feedItemTemplate;
        #endregion

        #region Private Fields
        private VisualElement _root;
        private VisualElement _feedContainer;
        private readonly Queue<VisualElement> _activeFeedItems = new();
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            FeedSystem.OnFeedItemAddEvent += _OnFeedItemAdded;
            FeedSystem.OnFeedLocalizedItemAddEvent += _OnFeedLocalizedItemAdded;
            _InitializeUI();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            FeedSystem.OnFeedItemAddEvent -= _OnFeedItemAdded;
            FeedSystem.OnFeedLocalizedItemAddEvent -= _OnFeedLocalizedItemAdded;
        }
        #endregion

        #region Private Methods
        private void _InitializeUI()
        {
            if (uiDocument == null)
                uiDocument = FindFirstObjectByType<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogWarning("[FeedController] No UIDocument found!");
                return;
            }
            _root = uiDocument.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[FeedController] Root visual element is null!");
                return;
            }
            _feedContainer = _root.Q<VisualElement>("KillFeed");
            if (_feedContainer == null)
            {
                Debug.LogWarning("[FeedController] KillFeed container not found!");
                return;
            }
            var existingItems = _feedContainer.Children().ToArray();
            foreach (var item in existingItems)
                item.RemoveFromHierarchy();
        }

        private void _OnFeedLocalizedItemAdded(string key, object[] args, float duration)
        {
            var message = (args != null && args.Length > 0)
                ? LocalizationManager.Get(key, args)
                : LocalizationManager.Get(key);

            _OnFeedItemAdded(message, duration);
        }

        private void _OnFeedItemAdded(string message, float duration)
        {
            if (_feedContainer == null || feedItemTemplate == null)
                return;

            var feedItemElement = feedItemTemplate.Instantiate();
            var feedItem = feedItemElement.Q<VisualElement>("FeedItem") ?? feedItemElement;
            var messageLabel = feedItem.Q<Label>("FeedMessage");

            if (messageLabel != null)
            {
                messageLabel.enableRichText = true;
                messageLabel.text = message;
            }
            else
            {
                var label = feedItem.Q<Label>();
                if (label != null)
                {
                    label.enableRichText = true;
                    label.text = message;
                }
            }

            _feedContainer.Add(feedItem);
            _activeFeedItems.Enqueue(feedItem);

            if (_activeFeedItems.Count > MaxFeedItems)
            {
                var oldestItem = _activeFeedItems.Dequeue();
                if (oldestItem != null && oldestItem.parent != null)
                    oldestItem.RemoveFromHierarchy();
            }

            StartCoroutine(_RemoveFeedItemAfterDelay(feedItem, duration));
        }

        private IEnumerator _RemoveFeedItemAfterDelay(VisualElement feedItem, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (feedItem is { parent: not null })
                feedItem.RemoveFromHierarchy();

            var queueArray = _activeFeedItems.ToArray();
            _activeFeedItems.Clear();

            foreach (var item in queueArray)
            {
                if (item != null && item.parent != null)
                    _activeFeedItems.Enqueue(item);
            }
        }
        #endregion
    }
}
