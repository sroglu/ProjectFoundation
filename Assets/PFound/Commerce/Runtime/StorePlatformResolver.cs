using PFound.Commerce.Core;
using UnityEngine;

namespace PFound.Commerce
{
    /// <summary>Maps the running Unity platform to the store whose SKUs the catalog resolves against.</summary>
    public static class StorePlatformResolver
    {
        /// <summary>
        /// The store for the current runtime. iOS/tvOS/macOS map to the App Store; everything else maps
        /// to Google Play (the Android/editor default), so editor testing resolves Google Play SKUs.
        /// </summary>
        public static StorePlatform Current
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.IPhonePlayer:
                    case RuntimePlatform.tvOS:
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        return StorePlatform.AppStore;
                    default:
                        return StorePlatform.GooglePlay;
                }
            }
        }
    }
}
