using UnityEngine;

namespace TUA.Misc
{
    public abstract class SingletonNetBehaviour<T> : NetBehaviour where T : NetBehaviour
    {
        #region Fields
        private static T _instance;
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object Lock = new();
        // ReSharper disable once StaticMemberInGenericType
        private static bool _applicationIsQuitting;
        #endregion
        
        #region Properties
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;
                lock (Lock)
                {
                    if (_instance) 
                        return _instance;
                    
                    _instance = FindFirstObjectByType<T>(FindObjectsInactive.Exclude);
                    if (_instance) 
                        return _instance;
                    
                    GameObject singleton = new GameObject();
                    _instance = singleton.AddComponent<T>();
                    singleton.name = typeof(T).ToString();
                    return _instance;
                }
            }
        }
        #endregion
        
        #region Unity Callbacks
        protected virtual void Awake()
        {
            if (!_instance)
                _instance = this as T;
            
            else if (_instance != this)
                Destroy(gameObject);
        }
        
        protected virtual void OnEnable()
        {
        }
        
        protected virtual void OnDisable()
        {
        }
        
        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
        
        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
        #endregion
    }
}
