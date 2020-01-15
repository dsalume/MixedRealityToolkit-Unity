// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    [MixedRealityController(
        SupportedControllerType.GGVHand,
        new[] { Handedness.Left, Handedness.Right })]
    public class SimulatedGestureHand : SimulatedHand
    {
        private MixedRealityPose currentGripPose = MixedRealityPose.ZeroIdentity;
     
        /// <inheritdoc />
        public override HandSimulationMode SimulationMode => HandSimulationMode.Gestures;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SimulatedGestureHand(
            TrackingState trackingState, 
            Handedness controllerHandedness, 
            IMixedRealityInputSource inputSource = null, 
            MixedRealityInteractionMapping[] interactions = null)
                : base(trackingState, controllerHandedness, inputSource, interactions)
        {
        }

        /// <summary>
        /// The GGV default interactions.
        /// </summary>
        /// <remarks>A single interaction mapping works for both left and right controllers.</remarks>
        public override MixedRealityInteractionMapping[] DefaultInteractions => new[]
        {
            new MixedRealityInteractionMapping(0, "Select", AxisType.Digital, DeviceInputType.Select, MixedRealityInputAction.None),
            new MixedRealityInteractionMapping(1, "Grip Pose", AxisType.SixDof, DeviceInputType.SpatialGrip, MixedRealityInputAction.None),
        };

        /// <inheritdoc />
        protected override void UpdateInteractions(SimulatedHandData handData)
        {
            Vector3 lastPosition = currentPosition;

            base.UpdateInteractions(handData);

            currentGripPose.Position = currentPosition;

            if (lastPosition != currentPosition)
            {
                CoreServices.InputSystem?.RaiseSourcePositionChanged(InputSource, this, currentPosition);
            }

            for (int i = 0; i < Interactions?.Length; i++)
            {
                switch (Interactions[i].InputType)
                {
                    case DeviceInputType.SpatialGrip:
                        Interactions[i].PoseData = currentGripPose;
                        if (Interactions[i].Changed)
                        {
                            CoreServices.InputSystem?.RaisePoseInputChanged(InputSource, ControllerHandedness, Interactions[i].MixedRealityInputAction, currentGripPose);
                        }
                        break;
                    case DeviceInputType.Select:
                        // Handled by the base class.
                        break;
                }
            }
        }
    }
}