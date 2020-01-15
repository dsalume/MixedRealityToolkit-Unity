// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// Snapshot of simulated hand data.
    /// </summary>
    [Serializable]
    public class SimulatedHandData
    {
        private static readonly int jointCount = Enum.GetNames(typeof(TrackedHandJoint)).Length;

        [SerializeField]
        private bool isTracked = false;
        public bool IsTracked => isTracked;
        [SerializeField]
        private MixedRealityPose[] joints = new MixedRealityPose[jointCount];
        public MixedRealityPose[] Joints => joints;
        [SerializeField]
        private bool isPinching = false;
        public bool IsPinching => isPinching;

        public delegate void HandJointDataGenerator(MixedRealityPose[] jointPoses);

        public void Copy(SimulatedHandData other)
        {
            isTracked = other.isTracked;
            isPinching = other.isPinching; 
            for (int i = 0; i < jointCount; ++i)
            {
                joints[i] = other.joints[i];
            }
        }

        /// <summary>
        /// Replace the hand data with the given values.
        /// </summary>
        /// <returns>True if the hand data has been changed.</returns>
        /// <param name="isTrackedNew">True if the hand is currently tracked.</param>
        /// <param name="isPinchingNew">True if the hand is in a pinching pose that causes a "Select" action.</param>
        /// <param name="generator">Generator function that produces joint positions and rotations. The joint data generator is only used when the hand is tracked.</param>
        /// <remarks>The timestamp of the hand data will be the current time, see [DateTime.UtcNow](https://docs.microsoft.com/en-us/dotnet/api/system.datetime.utcnow?view=netframework-4.8).</remarks>
        public bool Update(bool isTrackedNew, bool isPinchingNew, HandJointDataGenerator generator)
        {
            bool handDataChanged = false;

            if (isTracked != isTrackedNew || isPinching != isPinchingNew)
            {
                isTracked = isTrackedNew;
                isPinching = isPinchingNew;
                handDataChanged = true;
            }

            if (isTracked)
            {
                generator(Joints);
                handDataChanged = true;
            }

            return handDataChanged;
        }
    }

    public abstract class SimulatedHand : BaseHand
    {
        public abstract HandSimulationMode SimulationMode { get; }

        protected static readonly int jointCount = Enum.GetNames(typeof(TrackedHandJoint)).Length;

        protected readonly Dictionary<TrackedHandJoint, MixedRealityPose> jointPoses = new Dictionary<TrackedHandJoint, MixedRealityPose>();

        protected MixedRealityInputAction holdAction = MixedRealityInputAction.None;
        protected MixedRealityInputAction navigationAction = MixedRealityInputAction.None;
        protected MixedRealityInputAction manipulationAction = MixedRealityInputAction.None;
        protected MixedRealityInputAction selectAction = MixedRealityInputAction.None;

        protected Vector3 currentPosition = Vector3.zero;

        private bool useRailsNavigation = false;
        private float holdStartDuration = 0.0f;
        private float navigationStartThreshold = 0.0f;

        private Vector3 cumulativeDelta = Vector3.zero;
        private Vector3 currentRailsUsed = Vector3.one;

        private bool initializedFromProfile = false;

        private float SelectDownStartTime = 0.0f;
        private bool holdInProgress = false;
        private bool manipulationInProgress = false;
        private bool navigationInProgress = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        protected SimulatedHand(TrackingState trackingState, Handedness controllerHandedness, IMixedRealityInputSource inputSource = null, MixedRealityInteractionMapping[] interactions = null)
                : base(trackingState, controllerHandedness, inputSource, interactions)
        {}

        private Vector3 navigationDelta => new Vector3(
            Mathf.Clamp(cumulativeDelta.x, -1.0f, 1.0f) * currentRailsUsed.x,
            Mathf.Clamp(cumulativeDelta.y, -1.0f, 1.0f) * currentRailsUsed.y,
            Mathf.Clamp(cumulativeDelta.z, -1.0f, 1.0f) * currentRailsUsed.z);

        public override bool TryGetJoint(TrackedHandJoint joint, out MixedRealityPose pose)
        {
            return jointPoses.TryGetValue(joint, out pose);
        }

        public void UpdateState(SimulatedHandData handData)
        {
            for (int i = 0; i < jointCount; i++)
            {
                TrackedHandJoint handJoint = (TrackedHandJoint)i;

                if (!jointPoses.ContainsKey(handJoint))
                {
                    jointPoses.Add(handJoint, handData.Joints[i]);
                }
                else
                {
                    jointPoses[handJoint] = handData.Joints[i];
                }
            }

            CoreServices.InputSystem?.RaiseHandJointsUpdated(InputSource, ControllerHandedness, jointPoses);

            UpdateVelocity();

            UpdateInteractions(handData);
        }


        /// <inheritdoc />
        protected virtual void UpdateInteractions(SimulatedHandData handData)
        {
            EnsureProfileSettings();

            Vector3 lastPosition = currentPosition;
            currentPosition = jointPoses[TrackedHandJoint.IndexTip].Position;
            cumulativeDelta += currentPosition - lastPosition;

            for (int i = 0; i < Interactions?.Length; i++)
            {
                switch (Interactions[i].InputType)
                {
                    case DeviceInputType.Select:
                        Interactions[i].BoolData = handData.IsPinching;

                        if (Interactions[i].Changed)
                        {
                            if (Interactions[i].BoolData)
                            {
                                CoreServices.InputSystem?.RaiseOnInputDown(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction);

                                SelectDownStartTime = Time.time;
                                cumulativeDelta = Vector3.zero;
                            }
                            else
                            {
                                CoreServices.InputSystem?.RaiseOnInputUp(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction);

                                // Stop active gestures
                                TryCompleteSelect();
                                TryCompleteHold();
                                TryCompleteManipulation();
                                TryCompleteNavigation();
                            }
                        }
                        else if (Interactions[i].BoolData)
                        {
                            if (manipulationInProgress)
                            {
                                UpdateManipulation();
                            }
                            if (navigationInProgress)
                            {
                                UpdateNavigation();
                            }

                            if (cumulativeDelta.magnitude > navigationStartThreshold)
                            {
                                TryCancelHold();
                                TryStartNavigation();
                                TryStartManipulation();
                            }
                            else if (Time.time >= SelectDownStartTime + holdStartDuration)
                            {
                                TryStartHold();
                            }
                        }
                        break;
                }
            }
        }

        /// Lazy-init settings based on profile.
        /// This cannot happen in the constructor because the profile may not exist yet.
        protected void EnsureProfileSettings()
        {
            if (initializedFromProfile)
            {
                return;
            }
            initializedFromProfile = true;

            var gestureProfile = CoreServices.InputSystem?.InputSystemProfile?.GesturesProfile;
            if (gestureProfile != null)
            {
                for (int i = 0; i < gestureProfile.Gestures.Length; i++)
                {
                    var gesture = gestureProfile.Gestures[i];
                    switch (gesture.GestureType)
                    {
                        case GestureInputType.Hold:
                            holdAction = gesture.Action;
                            break;
                        case GestureInputType.Manipulation:
                            manipulationAction = gesture.Action;
                            break;
                        case GestureInputType.Navigation:
                            navigationAction = gesture.Action;
                            break;
                        case GestureInputType.Select:
                            selectAction = gesture.Action;
                            break;
                    }
                }

                useRailsNavigation = gestureProfile.UseRailsNavigation;
            }

            MixedRealityInputSimulationProfile inputSimProfile = null;
            if (CoreServices.InputSystem != null)
            {
                inputSimProfile = (CoreServices.InputSystem as IMixedRealityDataProviderAccess).GetDataProvider<IInputSimulationService>()?.InputSimulationProfile;
            }

            if (inputSimProfile != null)
            {
                holdStartDuration = inputSimProfile.HoldStartDuration;
                navigationStartThreshold = inputSimProfile.NavigationStartThreshold;
            }
        }

        private bool TryStartHold()
        {
            if (!holdInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureStarted(this, holdAction);
                holdInProgress = true;
                return true;
            }
            return false;
        }

        private bool TryCompleteHold()
        {
            if (holdInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCompleted(this, holdAction);
                holdInProgress = false;
                return true;
            }
            return false;
        }

        private bool TryCancelHold()
        {
            if (holdInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCanceled(this, holdAction);
                holdInProgress = false;
                return true;
            }
            return false;
        }

        private bool TryStartManipulation()
        {
            if (!manipulationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureStarted(this, manipulationAction);
                manipulationInProgress = true;
                return true;
            }
            return false;
        }

        private void UpdateManipulation()
        {
            if (manipulationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureUpdated(this, manipulationAction, cumulativeDelta);
            }
        }

        private bool TryCompleteManipulation()
        {
            if (manipulationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCompleted(this, manipulationAction, cumulativeDelta);
                manipulationInProgress = false;
                return true;
            }
            return false;
        }

        private bool TryCancelManipulation()
        {
            if (manipulationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCanceled(this, manipulationAction);
                manipulationInProgress = false;
                return true;
            }
            return false;
        }

        private bool TryCompleteSelect()
        {
            if (!manipulationInProgress && !holdInProgress && !navigationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCompleted(this, selectAction);
                return true;
            }
            return false;
        }

        private bool TryStartNavigation()
        {
            if (!navigationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureStarted(this, navigationAction);
                navigationInProgress = true;

                currentRailsUsed = Vector3.one;
                UpdateNavigationRails();
                return true;
            }
            return false;
        }

        private void UpdateNavigation()
        {
            if (navigationInProgress)
            {
                UpdateNavigationRails();
                CoreServices.InputSystem?.RaiseGestureUpdated(this, navigationAction, navigationDelta);
            }
        }

        private bool TryCompleteNavigation()
        {
            if (navigationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCompleted(this, navigationAction, navigationDelta);
                navigationInProgress = false;
                return true;
            }
            return false;
        }

        private bool TryCancelNavigation()
        {
            if (navigationInProgress)
            {
                CoreServices.InputSystem?.RaiseGestureCanceled(this, navigationAction);
                navigationInProgress = false;
                return true;
            }
            return false;
        }

        // If rails are used, test the delta for largest component and limit navigation to that axis
        private void UpdateNavigationRails()
        {
            if (useRailsNavigation && currentRailsUsed == Vector3.one)
            {
                if (Mathf.Abs(cumulativeDelta.x) >= navigationStartThreshold)
                {
                    currentRailsUsed = new Vector3(1, 0, 0);
                }
                else if (Mathf.Abs(cumulativeDelta.y) > navigationStartThreshold)
                {
                    currentRailsUsed = new Vector3(0, 1, 0);
                }
                else if (Mathf.Abs(cumulativeDelta.z) > navigationStartThreshold)
                {
                    currentRailsUsed = new Vector3(0, 0, 1);
                }
            }
        }
    }
}