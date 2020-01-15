// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.CameraSystem
{
    /// <summary>
    /// The Camera system controls the settings of the main camera.
    /// </summary>
    [HelpURL("https://microsoft.github.io/MixedRealityToolkit-Unity/Documentation/MixedRealityConfigurationGuide.html#camera")]
    public class MixedRealityCameraSystem : BaseCoreSystem, IMixedRealityCameraSystem
    {
        private enum DisplayType
        {
            Opaque = 0,
            Transparent
        }

        public MixedRealityCameraSystem(
            IMixedRealityServiceRegistrar registrar,
            BaseMixedRealityProfile profile = null) : base(registrar, profile)
        {
        }

        /// <inheritdoc/>
        public override string Name { get; protected set; } = "Mixed Reality Camera System";

        /// <summary>
        /// Is the current camera displaying on an Opaque (AR) device or a VR / immersive device
        /// </summary>
        public bool IsOpaque
        {
            get
            {
                currentDisplayType = DisplayType.Opaque;
#if UNITY_WSA
                if (!UnityEngine.XR.WSA.HolographicSettings.IsDisplayOpaque)
                {
                    currentDisplayType = DisplayType.Transparent;
                }
#endif
                return currentDisplayType == DisplayType.Opaque;
            }
        }

        /// <inheritdoc />
        public uint SourceId { get; } = 0;

        /// <inheritdoc />
        public string SourceName { get; } = "Mixed Reality Camera System";

        private MixedRealityCameraProfile cameraProfile = null;

        /// <inheritdoc/>
        public MixedRealityCameraProfile CameraProfile
        {
            get
            {
                if (cameraProfile == null)
                {
                    cameraProfile = ConfigurationProfile as MixedRealityCameraProfile;
                }
                return cameraProfile;
            }
        }

        private DisplayType currentDisplayType;
        private bool cameraOpaqueLastFrame = false;

        /// <inheritdoc />
        public override void Initialize()
        {
            cameraOpaqueLastFrame = IsOpaque;

            if (IsOpaque)
            {
                ApplySettingsForOpaqueDisplay();
            }
            else
            {
                ApplySettingsForTransparentDisplay();
            }

            // Ensure the camera is parented to the playspace which starts, unrotated, at the origin.
            MixedRealityPlayspace.Position = Vector3.zero;
            MixedRealityPlayspace.Rotation = Quaternion.identity;
            if (CameraCache.Main != null && CameraCache.Main.transform.position != Vector3.zero)
            {
                Debug.LogWarning($"The main camera is not positioned at the origin ({Vector3.zero}), immersive experiences may not behave as expected.");
            }
        }

        /// <inheritdoc />
        public override void Update()
        {
            if (IsOpaque != cameraOpaqueLastFrame)
            {
                cameraOpaqueLastFrame = IsOpaque;

                if (IsOpaque)
                {
                    ApplySettingsForOpaqueDisplay();
                }
                else
                {
                    ApplySettingsForTransparentDisplay();
                }
            }
        }

        /// <summary>
        /// Applies opaque settings from camera profile.
        /// </summary>
        private void ApplySettingsForOpaqueDisplay()
        {
            Camera camera = CameraCache.Main;
            if (camera != null)
            {
                camera.clearFlags = CameraProfile.CameraClearFlagsOpaqueDisplay;
                camera.nearClipPlane = CameraProfile.NearClipPlaneOpaqueDisplay;
                camera.farClipPlane = CameraProfile.FarClipPlaneOpaqueDisplay;
                camera.backgroundColor = CameraProfile.BackgroundColorOpaqueDisplay;
            }
            QualitySettings.SetQualityLevel(CameraProfile.OpaqueQualityLevel, false);
        }

        /// <summary>
        /// Applies transparent settings from camera profile.
        /// </summary>
        private void ApplySettingsForTransparentDisplay()
        {
            Camera camera = CameraCache.Main;
            if (camera != null)
            {
                camera.clearFlags = CameraProfile.CameraClearFlagsTransparentDisplay;
                camera.backgroundColor = CameraProfile.BackgroundColorTransparentDisplay;
                camera.nearClipPlane = CameraProfile.NearClipPlaneTransparentDisplay;
                camera.farClipPlane = CameraProfile.FarClipPlaneTransparentDisplay;
            }

            QualitySettings.SetQualityLevel(CameraProfile.HoloLensQualityLevel, false);
        }

        /// <inheritdoc />
        bool IEqualityComparer.Equals(object x, object y)
        {
            // There shouldn't be other Camera Systems to compare to.
            return false;
        }

        /// <inheritdoc />
        int IEqualityComparer.GetHashCode(object obj)
        {
            return Mathf.Abs(SourceName.GetHashCode());
        }
    }
}