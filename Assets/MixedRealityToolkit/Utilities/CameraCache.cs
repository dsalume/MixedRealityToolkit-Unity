// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Utilities
{
    /// <summary>
    /// The purpose of this class is to provide a cached reference to the main camera. Calling Camera.main
    /// executes a FindByTag on the scene, which will get worse and worse with more tagged objects.
    /// </summary>
    public static class CameraCache
    {
        private static Camera cachedCamera;

        /// <summary>
        /// Returns a cached reference to the main camera and uses Camera.main if it hasn't been cached yet.
        /// </summary>
        public static Camera Main
        {
            get
            {
                if (cachedCamera != null)
                {
                    if (cachedCamera.gameObject.activeInHierarchy)
                    {   // If the cached camera is active, return it
                        // Otherwise, our playspace may have been disabled
                        // We'll have to search for the next available
                        return cachedCamera;
                    }
                }

                // Cache the main camera
                cachedCamera = Camera.main;

                if (Application.isPlaying && cachedCamera == null)
                {
                    Debug.LogError("Trying to access camera but one is not available in the scene.");
                }

                return cachedCamera;
            }
        }
    }
}
