using PFound.MVC.core;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.MVC
{
    /// <summary>
    /// Tracks page views and instantiates instance-view prefabs.
    /// Maintains a back-stack of previously shown page views.
    /// Pure view lifecycle — no input handling. Games wire their own input
    /// to controllers (see MODULE.md "Game-specific input").
    /// </summary>
    public class ViewManager : MonoBehaviour
    {
        //Singleton
        private static ViewManager _instance;
        public static ViewManager Instance
        {
            get { return _instance; }
            private set
            {
                if (_instance == null)
                    _instance = value;
                else
                    Destroy(value);
            }
        }
        #region EditorBindings
        [Header("Views")]
        [SerializeField]
        private ViewBase[] pageViews;
        private ViewBase currentPageView;

        [Header("UI Prefabs")]
        [SerializeField]
        private ViewBase[] instancePrefabs;
        #endregion

        #region Properties
        private readonly Stack<ViewBase> history = new Stack<ViewBase>();
        #endregion

        private void Awake()
        {
            Instance = this;
        }

        #region ViewMethods
        /// <summary>
        /// Creates a new instance view for the requested view type.
        /// </summary>
        public T CreateInstanceView<T>() where T : ViewBase
        {
            if (typeof(T) == typeof(EmptyView))
                return new GameObject().AddComponent(typeof(EmptyView)) as T;

            foreach (var instancePrefab in instancePrefabs)
            {
                if (instancePrefab is T tInstancePrefab)
                {
                    return GameObject.Instantiate(tInstancePrefab);
                }
            }
            return null;
        }
        /// <summary> Returns a pre-designed page view from the inspector list. </summary>
        public static T GetPageView<T>() where T : ViewBase
        {
            for (int i = 0; i < Instance.pageViews.Length; i++)
            {
                if (Instance.pageViews[i] is T tPageView)
                    return tPageView;
            }
            return null;
        }

        /// <summary> Shows a page view by type, hiding the current one (optionally pushed onto history). </summary>
        public static void ShowPageView<T>(bool remember = true) where T : ViewBase
        {
            for (int i = 0; i < Instance.pageViews.Length; i++)
            {
                if (Instance.pageViews[i] is T)
                {
                    if (Instance.currentPageView != null)
                    {
                        if (remember)
                            Instance.history.Push(Instance.currentPageView);
                        Instance.currentPageView.Hide();
                    }
                    Instance.pageViews[i].Show();
                    Instance.currentPageView = Instance.pageViews[i];
                }
            }
        }
        /// <summary> Shows a specific page view instance. </summary>
        public static void ShowPageView(ViewBase view, bool remember = true)
        {
            if (Instance.currentPageView != null)
            {
                if (remember)
                    Instance.history.Push(Instance.currentPageView);
                Instance.currentPageView.Hide();
            }
            view.Show();
            Instance.currentPageView = view;
        }
        /// <summary> Pops the last page view off the history stack and shows it. </summary>
        public static void ShowLastPageView()
        {
            if (Instance.history.Count > 0)
            {
                ShowPageView(Instance.history.Pop(), false);
            }
        }

        #endregion
    }

}
